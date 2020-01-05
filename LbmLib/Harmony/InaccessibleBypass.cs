#if DEBUG
#define TRACE_LOGGING
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using Harmony;
using LbmLib.Language;
#if NET35
using System.Threading; // for ReaderWriterLockSlim
#else
using System.Collections.Concurrent; // for ConcurrentDictionary
#endif

namespace LbmLib.Harmony
{
	public class InaccessibleBypass
	{
		sealed class LocalVarEntry : IDisposable
		{
			public readonly LocalBuilder LocalVar;
			public bool IsFree = false;

			public LocalVarEntry(LocalBuilder localVar) => LocalVar = localVar;

			public void Dispose() => IsFree = true;

			public override string ToString() => $"LocalVarEntry({LocalVar}, IsFree: {IsFree})";

			public static LocalVarEntry None = new LocalVarEntry(null);

			public static implicit operator LocalBuilder(LocalVarEntry entry) => entry.LocalVar;
		}

		sealed class LocalVarEntries : IDisposable
		{
			readonly IEnumerable<LocalVarEntry> entries;

			public LocalVarEntries(params LocalVarEntry[] entries) => this.entries = entries;

			public LocalVarEntries(IEnumerable<LocalVarEntry> entries) => this.entries = entries;

			public void Dispose()
			{
				foreach (var entry in entries)
					entry.Dispose();
			}
		}

		// .NET Framework 3.5 doesn't have ConcurrentDictionary, so this Cache class is a very basic implementation that wraps a Dictionary
		// and guards access to it with a ReaderWriterSlimLock.
		// In .NET versions with ConcurrentDictionary, this Cache class is a simple shim over ConcurrentDictionary.
		class Cache<K, V>
		{
#if NET35
			readonly Dictionary<K, V> cache = new Dictionary<K, V>();
			readonly ReaderWriterLockSlim cacheLock = new ReaderWriterLockSlim();
#else
			readonly ConcurrentDictionary<K, V> cache = new ConcurrentDictionary<K, V>();
#endif

			public V GetOrAdd(K key, Func<K, V> valueFactory)
			{
#if NET35
				// Assumption: valueFactory calculation is idempotent per key, so no need for an upgradeable read lock.
				V value;
				cacheLock.EnterReadLock();
				try
				{
					if (cache.TryGetValue(key, out value))
						return value;
				}
				finally
				{
					cacheLock.ExitReadLock();
				}
				value = valueFactory(key);
				cacheLock.EnterWriteLock();
				try
				{
					cache[key] = value;
				}
				finally
				{
					cacheLock.ExitWriteLock();
				}
				return value;
#else
				return cache.GetOrAdd(key, valueFactory);
#endif
			}
		}

		readonly MemberInfoAccessibility accessibility;
		readonly MethodBase sourceMethod;
		readonly ILGenerator ilGenerator;
		readonly Dictionary<Type, List<LocalVarEntry>> localVarPool = new Dictionary<Type, List<LocalVarEntry>>();

		public InaccessibleBypass(MethodBase sourceMethod, ILGenerator ilGenerator, Func<Type, bool> declaringTypeFilter = null)
		{
			accessibility = new MemberInfoAccessibility(sourceMethod, declaringTypeFilter);
			this.sourceMethod = sourceMethod;
			this.ilGenerator = ilGenerator;
		}

		LocalVarEntry GetLocalVar(Type type, bool isPinned)
		{
			if (!localVarPool.TryGetValue(type, out var entries))
			{
				entries = new List<LocalVarEntry>();
				localVarPool[type] = entries;
			}
			else
			{
				var foundEntry = entries.Find(entry => entry.IsFree && entry.LocalVar.LocalType == type && entry.LocalVar.IsPinned == isPinned);
				if (!(foundEntry is null))
				{
					foundEntry.IsFree = false;
					Trace($"GetLocalVar => reuse {foundEntry}");
					return foundEntry;
				}
			}
			var newEntry = new LocalVarEntry(ilGenerator.DeclareLocal(type, isPinned));
			Trace($"GetLocalVar => declare {newEntry}");
			entries.Add(newEntry);
			return newEntry;
		}

		public bool IsAccessible(OpCode opcode, MemberInfo member)
		{
			// Following instructions are apparently exempt from this accessibility check.
			if (opcode == OpCodes.Constrained ||
				opcode == OpCodes.Ldtoken ||
				opcode == OpCodes.Initobj ||
				opcode == OpCodes.Ldobj ||
				opcode == OpCodes.Stobj ||
				opcode == OpCodes.Ldelem ||
				opcode == OpCodes.Stelem ||
				opcode == OpCodes.Sizeof)
			{
				return true;
			}
			else
			{
				return accessibility.IsAccessible(member);
			}
		}

		public IEnumerable<CodeInstruction> ForOpCode(OpCode opcode, MemberInfo member)
		{
			// TODO: Keep track of prefix instructions that would no longer be valid with replaced instructions: tailcall/constrained.
			if (opcode == OpCodes.Call)
				return Call((MethodInfo)member);
			else if (opcode == OpCodes.Callvirt)
				return Callvirt((MethodInfo)member);
			else if (opcode == OpCodes.Newobj)
				return Newobj((ConstructorInfo)member);
			else if (opcode == OpCodes.Newarr)
				return Newarr((Type)member);
			else if (opcode == OpCodes.Ldftn)
				return Ldftn((MethodBase)member);
			else if (opcode == OpCodes.Ldvirtftn)
				return Ldvirtftn((MethodBase)member);
			else if (opcode == OpCodes.Ldfld)
				return Ldfld((FieldInfo)member);
			else if (opcode == OpCodes.Ldsfld)
				return Ldsfld((FieldInfo)member);
			else if (opcode == OpCodes.Stfld)
				return Stfld((FieldInfo)member);
			else if (opcode == OpCodes.Stsfld)
				return Stsfld((FieldInfo)member);
			else if (opcode == OpCodes.Ldflda)
				return Ldflda((FieldInfo)member);
			else if (opcode == OpCodes.Ldsflda)
				return Ldsflda((FieldInfo)member);
			else if (opcode == OpCodes.Box)
				return Box((Type)member);
			else if (opcode == OpCodes.Unbox)
				return Unbox((Type)member);
			else if (opcode == OpCodes.Unbox_Any)
				return UnboxAny((Type)member);
			else if (opcode == OpCodes.Castclass)
				return Castclass((Type)member);
			else if (opcode == OpCodes.Isinst)
				return Isinst((Type)member);
			else if (opcode == OpCodes.Mkrefany)
				return Mkrefany((Type)member);
			else if (opcode == OpCodes.Refanyval)
				return Refanyval((Type)member);
			return null;
		}

		public IEnumerable<CodeInstruction> Call(MethodInfo method)
		{
			Trace($"Call({MemberToString(method)})");
			// TODO: Check for correct behavior if boxed value type.
			var parameters = method.GetParameters();
			var parameterCount = parameters.Length;
			var isInstance = !method.IsStatic;
			var returnType = method.ReturnType;
			var parameterLocalVars = parameters.Select(parameter => GetLocalVar(parameter.ParameterType, isPinned: false)).ToArray();
			using (new LocalVarEntries(parameterLocalVars))
			{
				using var argsLocalVar = GetLocalVar(typeof_object_array, isPinned: false);
				using var instanceLocalVar = isInstance ? GetLocalVar(typeof_object, isPinned: false) : LocalVarEntry.None;
				for (var i = parameterCount - 1; i >= 0; i--)
					yield return Stloc(parameterLocalVars[i]);
				if (isInstance)
					yield return Stloc(instanceLocalVar);
				foreach (var instruction in GetMethodInfoFromToken(method))
					yield return instruction;
				yield return LdcI(parameterCount);
				yield return new CodeInstruction(OpCodes.Newarr, typeof_object);
				for (var i = 1; i < parameterCount; i++)
					yield return new CodeInstruction(OpCodes.Dup);
				for (var i = 0; i < parameterCount; i++)
				{
					yield return LdcI(i);
					var parameterType = parameterLocalVars[i].LocalVar.LocalType;
					if (parameterType.IsByRef)
					{
						parameterType = parameterType.GetElementType();
						// TODO
					}
				}
			}
		}

		public IEnumerable<CodeInstruction> Callvirt(MethodInfo method)
		{
			//Trace($"Callvirt({MemberToString(method)})");
			// TODO: Check for correct behavior if boxed value type.
			// TODO
			return null;
		}

		public IEnumerable<CodeInstruction> Newobj(ConstructorInfo constructor)
		{
			//Trace($"Newobj({MemberToString(method)})");
			// TODO
			return null;
		}

		public IEnumerable<CodeInstruction> Newarr(Type type)
		{
			Trace($"Newarr({MemberToString(type)})");
			// TODO: Handle generic parameter type by looking at type of instance on top of the CIL stack.
			using var sizeLocalVar = GetLocalVar(typeof_int, isPinned: false);
			yield return Stloc(sizeLocalVar);
			foreach (var instruction in GetTypeFromToken(type))
				yield return instruction;
			yield return Ldloc(sizeLocalVar);
			yield return new CodeInstruction(OpCodes.Call, methodof_Array_CreateInstance);
		}

		public IEnumerable<CodeInstruction> Ldftn(MethodBase methodBase)
		{
			Trace($"Ldftn({MemberToString(methodBase)})");
			yield return new CodeInstruction(OpCodes.Ldtoken, methodBase);
			// box + unbox is a hacky way to get the address of a value type on the CIL stack without needing a local var.
			yield return new CodeInstruction(OpCodes.Box, typeof_RuntimeMethodHandle);
			yield return new CodeInstruction(OpCodes.Unbox, typeof_RuntimeMethodHandle);
			yield return new CodeInstruction(OpCodes.Call, methodof_RuntimeMethodHandle_GetFunctionPointer);
		}

		public IEnumerable<CodeInstruction> Ldvirtftn(MethodBase methodBase)
		{
			Trace($"Ldvirtftn({MemberToString(methodBase)})");
			// There's an additional object on the stack, and at runtime, we need to get the actual type of the object to get the
			// actual virtual method.
			foreach (var instruction in GetVirtualMethodInfoFromInstance(methodBase))
				yield return instruction;
			yield return new CodeInstruction(OpCodes.Callvirt, methodof_MethodBase_get_MethodHandle);
			// box + unbox is a hacky way to get the address of a value type on the CIL stack without needing a local var.
			yield return new CodeInstruction(OpCodes.Box, typeof_RuntimeMethodHandle);
			yield return new CodeInstruction(OpCodes.Unbox, typeof_RuntimeMethodHandle);
			yield return new CodeInstruction(OpCodes.Call, methodof_RuntimeMethodHandle_GetFunctionPointer);
		}

		// Precondition: Object reference or value type instance (NOT pointer) is on top of the CIL stack.
		// XXX: ldfld technically can operate on pointers, but in practice I'm not sure if it's actually done,
		// and I haven't yet figured out a way to peek the type of CIL stack's top.
		// TODO: Try manually tracking such CIL types across all the instructions in the method.
		// From various tests, it looks like the edge case where there could be conditional branches before the ldfld,
		// with some providing addresses and others providing instances, leads to either VerificationException, InvalidProgramException,
		// crash (mono), or otherwise undefined behavior, depending on framework/runtime/platform.
		// It's probably safe to assume that this case never happens in working code (and of course, compilers should never generate it).
		public IEnumerable<CodeInstruction> Ldfld(FieldInfo field)
		{
			// TODO: Handle null instance as ldsfld.
			if (field.DeclaringType.IsValueType)
				return LdfldValueInstance(field);
			else
				return LdfldObjectReference(field);
		}

		IEnumerable<CodeInstruction> LdfldObjectReference(FieldInfo field)
		{
			Trace($"Ldfld$ObjectRef({MemberToString(field)})");
			var declaringType = field.DeclaringType;
			var fieldType = field.FieldType;
			using var instanceLocalVar = GetLocalVar(declaringType, isPinned: false);
			yield return new CodeInstruction(OpCodes.Dup);
			yield return Stloc(instanceLocalVar);
			var afterThrowLabel = ilGenerator.DefineLabel();
			// Note: Cannot determine whether short-form (conditional) branch instructions would suffice.
			// That can only be determined right before finalizing the method being transpiled.
			// So using long-form (conditional) branch instructions.
			yield return new CodeInstruction(OpCodes.Brtrue, afterThrowLabel);
			yield return new CodeInstruction(OpCodes.Newobj, methodof_NullReferenceException_ctor0);
			yield return new CodeInstruction(OpCodes.Throw);
			var instructions = GetFieldInfoFromToken(field).ToArray();
			instructions[0].labels.Add(afterThrowLabel);
			foreach (var instruction in instructions)
				yield return instruction;
			yield return Ldloc(instanceLocalVar);
			yield return new CodeInstruction(OpCodes.Callvirt, methodof_FieldInfo_GetValue);
			if (fieldType.IsValueType)
			{
				foreach (var instruction in UnboxAnyValueInstanceCheckAccessibility(fieldType))
					yield return instruction;
			}
		}

		IEnumerable<CodeInstruction> LdfldValueInstance(FieldInfo field)
		{
			Trace($"Ldfld$ValueInstance({MemberToString(field)})");
			// TODO: Use FieldInfo.GetValueDirect if available? Need mkrefany though.
			// TODO: Check for correct behavior if boxed value type.
			var declaringType = field.DeclaringType;
			var fieldType = field.FieldType;
			using var instanceLocalVar = GetLocalVar(declaringType, isPinned: false);
			yield return Stloc(instanceLocalVar);
			foreach (var instruction in GetFieldInfoFromToken(field))
				yield return instruction;
			foreach (var instruction in LdlocAndBoxValueInstanceCheckAccessibility(declaringType, instanceLocalVar, valueTypeIsPointer: false))
				yield return instruction;
			yield return new CodeInstruction(OpCodes.Callvirt, methodof_FieldInfo_GetValue);
			if (fieldType.IsValueType)
			{
				foreach (var instruction in UnboxAnyValueInstanceCheckAccessibility(fieldType))
					yield return instruction;
			}
		}

		public IEnumerable<CodeInstruction> Ldsfld(FieldInfo field)
		{
			Trace($"Ldsfld({MemberToString(field)})");
			var fieldType = field.FieldType;
			foreach (var instruction in GetFieldInfoFromToken(field))
				yield return instruction;
			yield return new CodeInstruction(OpCodes.Ldnull);
			yield return new CodeInstruction(OpCodes.Callvirt, methodof_FieldInfo_GetValue);
			if (fieldType.IsValueType)
			{
				foreach (var instruction in UnboxAnyValueInstanceCheckAccessibility(fieldType))
					yield return instruction;
			}
		}

		// Precondition: Object reference or value type pointer (NOT value type instance) is on top of the CIL stack.
		public IEnumerable<CodeInstruction> Stfld(FieldInfo field)
		{
			// TODO: Handle null instance as stsfld.
			if (field.DeclaringType.IsValueType)
				return StfldValuePointer(field);
			else
				return StfldObjectReference(field);
		}

		IEnumerable<CodeInstruction> StfldObjectReference(FieldInfo field)
		{
			Trace($"Stfld$ObjectReference({MemberToString(field)})");
			var declaringType = field.DeclaringType;
			var fieldType = field.FieldType;
			using var instanceLocalVar = GetLocalVar(declaringType, isPinned: false);
			using var valueLocalVar = GetLocalVar(fieldType, isPinned: false);
			yield return Stloc(valueLocalVar);
			yield return new CodeInstruction(OpCodes.Dup);
			yield return Stloc(instanceLocalVar);
			var afterThrowLabel = ilGenerator.DefineLabel();
			// Note: Cannot determine whether short-form (conditional) branch instructions would suffice.
			// That can only be determined right before finalizing the method being transpiled.
			// So using long-form (conditional) branch instructions.
			yield return new CodeInstruction(OpCodes.Brtrue, afterThrowLabel);
			yield return new CodeInstruction(OpCodes.Newobj, methodof_NullReferenceException_ctor0);
			yield return new CodeInstruction(OpCodes.Throw);
			var instructions = GetFieldInfoFromToken(field).ToArray();
			instructions[0].labels.Add(afterThrowLabel);
			foreach (var instruction in instructions)
				yield return instruction;
			yield return Ldloc(instanceLocalVar);
			foreach (var instruction in LdlocAndBoxCheckAccessibility(fieldType, valueLocalVar, valueTypeIsPointer: false))
				yield return instruction;
			yield return new CodeInstruction(OpCodes.Callvirt, methodof_FieldInfo_SetValue);
		}

		IEnumerable<CodeInstruction> StfldValuePointer(FieldInfo field)
		{
			Trace($"Stfld$ValuePointer({MemberToString(field)})");
			// TODO: Use FieldInfo.SetValueDirect if available? Need mkrefany though.
			// TODO: Check for correct behavior if boxed value type.
			var declaringType = field.DeclaringType;
			var fieldType = field.FieldType;
			using var instancePtrLocalVar = GetLocalVar(declaringType.MakeByRefType(), isPinned: false);
			using var valueLocalVar = GetLocalVar(fieldType, isPinned: false);
			yield return Stloc(valueLocalVar);
			yield return Stloc(instancePtrLocalVar);

			//yield return new CodeInstruction(OpCodes.Ldstr, "field set new value: {0}");
			//yield return Ldloc(valueLocalVar);
			//if (fieldType.IsValueType)
			//	yield return new CodeInstruction(OpCodes.Box, fieldType);
			//yield return new CodeInstruction(OpCodes.Call, typeof(string).GetMethod(nameof(string.Format), new[] { typeof(string), typeof(object) }));
			//yield return new CodeInstruction(OpCodes.Call, typeof(Language.Logging).GetMethod(nameof(Language.Logging.StringLog)));
			//yield return new CodeInstruction(OpCodes.Ldstr, "value type address: 0x{0:x16}");
			//yield return Ldloc(instancePtrLocalVar);
			//yield return new CodeInstruction(OpCodes.Conv_I8);
			//yield return new CodeInstruction(OpCodes.Box, typeof(long));
			//yield return new CodeInstruction(OpCodes.Call, typeof(string).GetMethod(nameof(string.Format), new[] { typeof(string), typeof(object) }));
			//yield return new CodeInstruction(OpCodes.Call, typeof(Language.Logging).GetMethod(nameof(Language.Logging.StringLog)));

			foreach (var instruction in GetFieldInfoFromToken(field))
				yield return instruction;

			//using var fieldInfoVar = GetLocalVar(typeof_FieldInfo, isPinned: false);
			//yield return Stloc(fieldInfoVar);
			//yield return new CodeInstruction(OpCodes.Ldstr, "field info: {0}.{1}");
			//yield return Ldloc(fieldInfoVar);
			//yield return new CodeInstruction(OpCodes.Callvirt, typeof(MemberInfo).GetProperty(nameof(MemberInfo.DeclaringType)).GetGetMethod());
			//yield return new CodeInstruction(OpCodes.Callvirt, typeof_Type.GetProperty(nameof(Type.FullName)).GetGetMethod());
			//yield return Ldloc(fieldInfoVar);
			//yield return new CodeInstruction(OpCodes.Callvirt, typeof(MemberInfo).GetProperty(nameof(MemberInfo.Name)).GetGetMethod());
			//yield return new CodeInstruction(OpCodes.Call, typeof(string).GetMethod(nameof(string.Format), new[] { typeof(string), typeof(object), typeof(object) }));
			//yield return new CodeInstruction(OpCodes.Call, typeof(Language.Logging).GetMethod(nameof(Language.Logging.StringLog)));
			//yield return Ldloc(fieldInfoVar);

			foreach (var instruction in LdlocAndBoxValueInstanceCheckAccessibility(declaringType, instancePtrLocalVar, valueTypeIsPointer: true))
				yield return instruction;
			using var boxLocalVar = GetLocalVar(typeof_object, isPinned: true);
			yield return new CodeInstruction(OpCodes.Dup);
			yield return Stloc(boxLocalVar);

			//yield return new CodeInstruction(OpCodes.Ldstr, "box address: 0x{0:x16}");
			//yield return Ldloca(boxLocalVar);
			//yield return new CodeInstruction(OpCodes.Ldind_I);
			//yield return new CodeInstruction(OpCodes.Conv_I8);
			//yield return new CodeInstruction(OpCodes.Box, typeof(long));
			//yield return new CodeInstruction(OpCodes.Call, typeof(string).GetMethod(nameof(string.Format), new[] { typeof(string), typeof(object) }));
			//yield return new CodeInstruction(OpCodes.Call, typeof(Language.Logging).GetMethod(nameof(Language.Logging.StringLog)));

			foreach (var instruction in LdlocAndBoxCheckAccessibility(fieldType, valueLocalVar, valueTypeIsPointer: false))
				yield return instruction;
			yield return new CodeInstruction(OpCodes.Callvirt, methodof_FieldInfo_SetValue);

			//yield return new CodeInstruction(OpCodes.Break);

			// Copy updated boxed value type's data to original value type.
			yield return Ldloc(instancePtrLocalVar);
			foreach (var instruction in LdlocAndUnboxAnyValueInstanceCheckAccessibility(declaringType, boxLocalVar))
				yield return instruction;
			yield return new CodeInstruction(OpCodes.Stobj, declaringType);

			//yield return new CodeInstruction(OpCodes.Break);

			// "Free" the pinned object var.
			yield return new CodeInstruction(OpCodes.Ldnull);
			yield return Stloc(boxLocalVar);
		}

		public IEnumerable<CodeInstruction> Stsfld(FieldInfo field)
		{
			Trace($"Stsfld({MemberToString(field)})");
			var fieldType = field.FieldType;
			using var valueLocalVar = GetLocalVar(fieldType, isPinned: false);
			yield return Stloc(valueLocalVar);
			foreach (var instruction in GetFieldInfoFromToken(field))
				yield return instruction;
			yield return new CodeInstruction(OpCodes.Ldnull);
			foreach (var instruction in LdlocAndBoxCheckAccessibility(fieldType, valueLocalVar, valueTypeIsPointer: false))
				yield return instruction;
			yield return new CodeInstruction(OpCodes.Callvirt, methodof_FieldInfo_SetValue);
		}

		public IEnumerable<CodeInstruction> Ldflda(FieldInfo field)
		{
			//Trace($"Ldflda({MemberToString(field)})");
			// TODO
			return null;
		}

		public IEnumerable<CodeInstruction> Ldsflda(FieldInfo field)
		{
			//Trace($"Ldsflda({MemberToString(field)})");
			// TODO
			return null;
		}

		public IEnumerable<CodeInstruction> Box(Type type)
		{
			Trace($"Box({MemberToString(type)})");
			// TODO: Handle nullable value types: if it contains no value, ldnull, otherwise, box the contained value.
			// TODO: Handle generic parameter type by looking at type of instance on top of the CIL stack.
			if (type.IsValueType)
			{
				using var valueLocalVar = GetLocalVar(type, isPinned: false);
				yield return Stloc(valueLocalVar);
				yield return Ldloca(valueLocalVar);
				yield return new CodeInstruction(OpCodes.Constrained, type);
				yield return new CodeInstruction(OpCodes.Callvirt, methodof_object_MemberwiseClone);
			}
		}

		IEnumerable<CodeInstruction> LdlocAndBoxCheckAccessibility(Type type, LocalBuilder localVar, bool valueTypeIsPointer)
		{
			if (type.IsValueType)
				return LdlocAndBoxValueInstanceCheckAccessibility(type, localVar, valueTypeIsPointer);
			else
				return new CodeInstruction[] { Ldloc(localVar) };
		}

		IEnumerable<CodeInstruction> LdlocAndBoxValueInstanceCheckAccessibility(Type type, LocalBuilder localVar, bool valueTypeIsPointer)
		{
			if (accessibility.IsAccessible(type))
			{
				yield return Ldloc(localVar);
				if (valueTypeIsPointer)
					yield return new CodeInstruction(OpCodes.Ldobj, type);
				yield return new CodeInstruction(OpCodes.Box, type);
			}
			else
			{
				if (valueTypeIsPointer)
					yield return Ldloc(localVar);
				else
					yield return Ldloca(localVar);
				yield return new CodeInstruction(OpCodes.Constrained, type);
				yield return new CodeInstruction(OpCodes.Callvirt, methodof_object_MemberwiseClone);
			}
		}

		public IEnumerable<CodeInstruction> Unbox(Type valueType)
		{
			Trace($"Unbox({MemberToString(valueType)})");
			// TODO: Handle nullable value types by creating a new Nullable<T>, and returning the address of it.
			// Note: Need to store in valueLocalVar and return its address to avoid a perpetually pinned object variable.
			using var boxLocalVar = GetLocalVar(typeof_object, isPinned: true);
			using var valueLocalVar = GetLocalVar(valueType, isPinned: false);
			yield return Stloc(boxLocalVar);
			foreach (var instruction in LdlocAndUnbox(valueType, boxLocalVar))
				yield return instruction;
			yield return new CodeInstruction(OpCodes.Ldobj, valueType);
			yield return Stloc(valueLocalVar);
			// "Free" the pinned object var.
			yield return new CodeInstruction(OpCodes.Ldnull);
			yield return Stloc(boxLocalVar);
			yield return Ldloca(valueLocalVar);
		}

		public IEnumerable<CodeInstruction> UnboxAny(Type type)
		{
			Trace($"UnboxAny({MemberToString(type)})");
			// TODO: Handle nullable value types by creating a new Nullable<T>, and returning it.
			// TODO: Handle generic parameter type by looking at type of instance on top of the CIL stack.
			if (type.IsValueType)
				return UnboxAnyValueInstance(type);
			else
				return Castclass(type);
		}

		IEnumerable<CodeInstruction> UnboxAnyValueInstanceCheckAccessibility(Type valueType)
		{
			if (accessibility.IsAccessible(valueType))
				return new[] { new CodeInstruction(OpCodes.Unbox_Any, valueType) };
			else
				return UnboxAnyValueInstance(valueType);
		}

		IEnumerable<CodeInstruction> UnboxAnyValueInstance(Type valueType)
		{
			using var boxLocalVar = GetLocalVar(typeof_object, isPinned: true);
			yield return Stloc(boxLocalVar);
			foreach (var instruction in LdlocAndUnboxAnyValueInstance(valueType, boxLocalVar))
				yield return instruction;
			// "Free" the pinned object var.
			yield return new CodeInstruction(OpCodes.Ldnull);
			yield return Stloc(boxLocalVar);
		}

		IEnumerable<CodeInstruction> LdlocAndUnboxAny(Type type, LocalBuilder localVar)
		{
			if (type.IsValueType)
				return LdlocAndUnboxAnyValueInstance(type, localVar);
			else
				return new CodeInstruction[] { Ldloc(localVar) };
		}

		IEnumerable<CodeInstruction> LdlocAndUnboxAnyValueInstanceCheckAccessibility(Type type, LocalBuilder localVar)
		{
			if (accessibility.IsAccessible(type))
				return new[]
				{
					Ldloc(localVar),
					new CodeInstruction(OpCodes.Unbox_Any, type),
				};
			else
				return LdlocAndUnboxAnyValueInstance(type, localVar);
		}

		IEnumerable<CodeInstruction> LdlocAndUnboxAnyValueInstance(Type type, LocalBuilder localVar)
		{
			foreach (var instruction in LdlocAndUnbox(type, localVar))
				yield return instruction;
			yield return new CodeInstruction(OpCodes.Ldobj, type);
		}

		// Helper for Unbox and LdlocAndUnboxAnyValueInstance.
		IEnumerable<CodeInstruction> LdlocAndUnbox(Type valueType, LocalBuilder boxLocalVar)
		{
			yield return Ldloca(boxLocalVar);
			//yield return new CodeInstruction(OpCodes.Conv_U);
			yield return new CodeInstruction(OpCodes.Ldind_I);
			yield return LdcI(GetBoxValuePtrOffset(valueType));
			yield return new CodeInstruction(OpCodes.Add);
			//using var ptrVar = GetLocalVar(valueType.MakePointerType(), isPinned: false);
			//yield return Stloc(ptrVar);
			//yield return new CodeInstruction(OpCodes.Ldstr, "unbox address: 0x{0:x16}");
			//yield return Ldloc(ptrVar);
			//yield return new CodeInstruction(OpCodes.Conv_I8);
			//yield return new CodeInstruction(OpCodes.Box, typeof(long));
			//yield return new CodeInstruction(OpCodes.Call, typeof(string).GetMethod(nameof(string.Format), new[] { typeof(string), typeof(object) }));
			//yield return new CodeInstruction(OpCodes.Call, typeof(Language.Logging).GetMethod(nameof(Language.Logging.StringLog)));
			//yield return Ldloc(ptrVar);
		}

		static readonly Cache<Type, int> boxValuePtrOffsetCache = new Cache<Type, int>();

		static unsafe int GetBoxValuePtrOffset(Type valueType)
		{
			return boxValuePtrOffsetCache.GetOrAdd(valueType, valueType =>
			{
				var sampleBox = Activator.CreateInstance(valueType);
				var sampleHandle = GCHandle.Alloc(sampleBox, GCHandleType.Pinned);
				try
				{
					// Note: This TypedReference represents a reference to the variable sampleObj itself, not the object that sampleObj references.
					// TypedReference's internal value needs to be dereferenced twice to get the actual object.
					var typedRefBoxRef = __makeref(sampleBox);
					// In MS .NET, TypedReference IntPtr field for the referenced object is the first field.
					// In Mono .NET, that field is the second field, after a RuntimeTypeHandle field.
					var typedRefValueFieldOffset = isMono ? sizeof_RuntimeTypeHandle : 0;
					// Following boxPtr initialization is equivalent to:
					//TypedReference* typedRefBoxRefAddr = &typedRefBoxRef;
					//void* typedRefBoxRefIntPtrAddr = (byte*)typedRefBoxRefAddr + typedRefValueFieldOffset;
					//IntPtr typedRefBoxRefIntPtr = *(IntPtr*)typedRefBoxRefIntPtrAddr;
					//void* boxRefAddr = typedRefBoxRefIntPtr.ToPointer();
					//// Choice of pointer type is technically arbitrary, but IntPtr is the "safe" version of a pointer,
					//// which GCHandle.AddrOfPinnedObject() also returns.
					//IntPtr boxAddr = *(IntPtr*)boxRefAddr;
					var boxPtr = **(IntPtr**)((byte*)&typedRefBoxRef + typedRefValueFieldOffset);
					var boxDataPtr = sampleHandle.AddrOfPinnedObject();
					var boxValuePtrOffset = (int)(boxDataPtr.ToInt64() - boxPtr.ToInt64());
					Trace($"GetBoxValuePtrOffset({MemberToString(valueType)}) => {boxValuePtrOffset}");
					return boxValuePtrOffset;
				}
				finally
				{
					sampleHandle.Free();
				}
			});
		}

		IEnumerable<CodeInstruction> Castclass(Type type)
		{
			Trace($"Castclass({MemberToString(type)})");
			// TODO: Handle generic parameter type by looking at type of instance on top of the CIL stack.
			// TODO: Ensure case when type is a value type (including nullable value type) is handled.
			using var localVar = GetLocalVar(typeof_object, isPinned: false);
			var afterCastLabel = ilGenerator.DefineLabel();
			foreach (var instruction in IsInstance(type, localVar, afterCastLabel))
				yield return instruction;
			// If top of CIL stack is true, skip the exception throwing.
			yield return new CodeInstruction(OpCodes.Brtrue, afterCastLabel);
			yield return new CodeInstruction(OpCodes.Ldstr, "Unable to cast object of type '{0}' to type '{1}'.");
			yield return Ldloc(localVar);
			yield return new CodeInstruction(OpCodes.Callvirt, methodof_object_GetType);
			yield return new CodeInstruction(OpCodes.Callvirt, methodof_Type_get_FullName);
			foreach (var instruction in GetTypeFromToken(type))
				yield return instruction;
			yield return new CodeInstruction(OpCodes.Call, methodof_string_Format3);
			yield return new CodeInstruction(OpCodes.Newobj, methodof_InvalidCastException_ctor1);
			yield return new CodeInstruction(OpCodes.Throw);
			var afterCastInstruction = Ldloc(localVar);
			afterCastInstruction.labels.Add(afterCastLabel);
			yield return afterCastInstruction;
			// Assume that all uses of the object being casted are going to replaced with reflection equivalents in a calling transpiler,
			// so no need for any further instructions.
		}

		IEnumerable<CodeInstruction> Isinst(Type type)
		{
			Trace($"Isinst({MemberToString(type)})");
			// TODO: Handle generic parameter type by looking at type of instance on top of the CIL stack.
			// TODO: Ensure case when type is a value type (including nullable value type) is handled.
			using var localVar = GetLocalVar(typeof_object, isPinned: false);
			var afterCastLabel = ilGenerator.DefineLabel();
			foreach (var instruction in IsInstance(type, localVar, afterCastLabel))
				yield return instruction;
			// If top of CIL stack is true, don't need the null defaulting.
			yield return new CodeInstruction(OpCodes.Brtrue, afterCastLabel);
			yield return new CodeInstruction(OpCodes.Ldnull);
			yield return Stloc(localVar);
			var afterCastInstruction = Ldloc(localVar);
			afterCastInstruction.labels.Add(afterCastLabel);
			yield return afterCastInstruction;
			// Assume that all uses of the object being casted are going to replaced with reflection equivalents in a calling transpiler,
			// so no need for any further instructions.
		}

		// Helper for Castclass and Isinst.
		// Stores object on top of CIL stack into localVar (assumed already declared),
		// branches to afterCastLabel if that object is null,
		// and pushes bool result of type.IsInstanceOfType(object) onto the CIL stack.
		IEnumerable<CodeInstruction> IsInstance(Type type, LocalBuilder localVar, Label afterCastLabel)
		{
			yield return new CodeInstruction(OpCodes.Dup);
			yield return Stloc(localVar);
			// Check whether object ref at top of CIL stack is null; if so, the castclass/isinst is essentially skipped.
			// Note: Cannot determine whether short-form (conditional) branch instructions would suffice.
			// That can only be determined right before finalizing the method being transpiled.
			// So using long-form (conditional) branch instructions.
			yield return new CodeInstruction(OpCodes.Brfalse, afterCastLabel);
			foreach (var instruction in GetTypeFromToken(type))
				yield return instruction;
			yield return Ldloc(localVar);
			yield return new CodeInstruction(OpCodes.Callvirt, methodof_Type_IsInstanceOfType);
		}

		public IEnumerable<CodeInstruction> Mkrefany(Type type)
		{
			Trace($"Mkrefany({MemberToString(type)}");
			// TODO: Handle generic parameter type by looking at type of instance on top of the CIL stack.
			using var instancePtrLocalVar = GetLocalVar(type.MakePointerType(), isPinned: true);
			yield return Stloc(instancePtrLocalVar);
			foreach (var instruction in LdlocAndMkrefany(type, instancePtrLocalVar))
				yield return instruction;
		}

		static readonly Cache<Type, IntPtr> monoTypedReferenceKlassCache = new Cache<Type, IntPtr>();
		static readonly unsafe int sizeof_RuntimeTypeHandle = sizeof(RuntimeTypeHandle);

		IEnumerable<CodeInstruction> LdlocAndMkrefany(Type type, LocalBuilder instancePtrLocalVar)
		{
			// This TypedReference is instantiated on the method's stack frame, so don't need to pin it.
			using var typedRefLocalVar = GetLocalVar(typeof_TypedReference, isPinned: false);
			yield return Ldloca(typedRefLocalVar);
			yield return new CodeInstruction(OpCodes.Dup);
			yield return new CodeInstruction(OpCodes.Initobj, typeof_TypedReference);
			//yield return new CodeInstruction(OpCodes.Conv_U);
			if (isMono)
			{
				// In Mono .NET, a <mkrefany type>-created (__makeref) TypedReference struct has memory layout:
				// * RuntimeTypeHandle type = result of <ldtoken type> (type.TypeHandle)
				// * IntPtr value = pointer to data at top of CIL stack at <mkrefany type> instruction
				// * IntPtr klass = pointer to native (and not exposed to managed code until Mono 4.8+) MonoClass object
				yield return new CodeInstruction(OpCodes.Ldtoken, type);
				yield return new CodeInstruction(OpCodes.Stobj, typeof_RuntimeTypeHandle);
				yield return Ldloca(typedRefLocalVar);
				//yield return new CodeInstruction(OpCodes.Conv_U);
				yield return LdcI(sizeof_RuntimeTypeHandle);
				yield return new CodeInstruction(OpCodes.Add);
				yield return Ldloc(instancePtrLocalVar);
				yield return new CodeInstruction(OpCodes.Stind_I);
				yield return Ldloca(typedRefLocalVar);
				//yield return new CodeInstruction(OpCodes.Conv_U);
				yield return LdcI(sizeof_RuntimeTypeHandle + IntPtr.Size);
				yield return new CodeInstruction(OpCodes.Add);
				// The klass pointer's value cannot be reliably determined without calling using introspecting a result of mkrefany of
				// an exemplar instance of type. The only way we can reliably use mkrefany is within a DynamicMethod with skipVisibility=true.
				// Note: This klass pointer's value (address of the native MonoClass object) is only going to be valid in the same process,
				// so any (partially) copied assembly that uses mkrefany is definitely not going to be portable.
				// Also, TypedReference is a special struct that cannot be returned from a method, so we have to extract the klass pointer
				// within the DynamicMethod's instructions.
				var klass = monoTypedReferenceKlassCache.GetOrAdd(type, type =>
				{
					var dynMethod = new DynamicMethod("<Mkrefany>", typeof_IntPtr, Type.EmptyTypes, type, skipVisibility: true);
					var dynILGenerator = dynMethod.GetILGenerator();
					var dynExemplarLocalVar = dynILGenerator.DeclareLocal(type);
					// Don't need to initialize the local var of type to anything - just need to allocate space on the method's stack frame.
					// (Note that this is true even for object instances, since they're represented as object references, and object references
					// are effectively like a managed pointer to the actual object data on the heap.)
					dynILGenerator.Emit(OpCodes.Ldloca_S, dynExemplarLocalVar);
					dynILGenerator.Emit(OpCodes.Mkrefany, type);
					var dynTypedRefLocalVar = dynILGenerator.DeclareLocal(typeof_TypedReference);
					dynILGenerator.Emit(OpCodes.Stloc_1); // equivalent: Emit(OpCodes.Stloc_S, dynTypedRefLocalVar)
					dynILGenerator.Emit(OpCodes.Ldloca_S, dynTypedRefLocalVar);
					//dynILGenerator.Emit(OpCodes.Conv_U);
					dynILGenerator.Emit(OpCodes.Ldc_I4_S, sizeof_RuntimeTypeHandle + IntPtr.Size);
					dynILGenerator.Emit(OpCodes.Add);
					dynILGenerator.Emit(OpCodes.Ldind_I); // IL generation treats the native int type the same as the IntPtr type
					dynILGenerator.Emit(OpCodes.Ret);
					return (IntPtr)dynMethod.Invoke(null, new object[0]);
				});
				yield return LdcI(klass);
				yield return new CodeInstruction(OpCodes.Conv_U);
				yield return new CodeInstruction(OpCodes.Stind_I);
			}
			else
			{
				// In MS .NET, a <mkrefany type>-created (__makeref) TypedReference struct has memory layout:
				// * IntPtr Value = pointer to data at top of CIL stack at <mkrefany type> instruction
				// * IntPtr Type = pointer contained within result of <ldtoken type> (type.TypeHandle.Value)
				yield return Ldloc(instancePtrLocalVar);
				yield return new CodeInstruction(OpCodes.Stind_I);
				yield return Ldloca(typedRefLocalVar);
				//yield return new CodeInstruction(OpCodes.Conv_U);
				yield return LdcI(IntPtr.Size);
				yield return new CodeInstruction(OpCodes.Add);
				// Note: Although in .NET Framework 3.5, RuntimeTypeHandle only contains a single IntPtr field, by .NET 4.5 (and possibly earlier),
				// RuntimeTypeHandle instead contains a single RuntimeType reference field.
				// RuntimeType is the runtime implementation of the Type class, so we can't do any pointer deferencing tricks such as treating
				// an IntPtr like a RuntimeTypeHandle. We'll have to actually use the RuntimeTypeHandle.Value property.
				yield return new CodeInstruction(OpCodes.Ldtoken, type);
				// box + unbox is a hacky way to get the address of a value type on the CIL stack without needing a local var.
				yield return new CodeInstruction(OpCodes.Box, typeof_RuntimeTypeHandle);
				yield return new CodeInstruction(OpCodes.Unbox, typeof_RuntimeTypeHandle);
				yield return new CodeInstruction(OpCodes.Call, methodof_RuntimeTypeHandle_get_Value);
				yield return new CodeInstruction(OpCodes.Stind_I);
			}
			// "Free" the pinned pointer variable.
			// Note that a native int of 0 needs to be used, not null (via ldnull), even for managed pointers to object references.
			yield return new CodeInstruction(OpCodes.Ldc_I4_0);
			yield return new CodeInstruction(OpCodes.Conv_U);
			yield return Stloc(instancePtrLocalVar);
			yield return Ldloc(typedRefLocalVar);
		}

		IEnumerable<CodeInstruction> LdlocAndMkrefanyCheckAccessibility(Type type, LocalBuilder instancePtrLocalVar)
		{
			if (accessibility.IsAccessible(type))
				return new[]
				{
					Ldloc(instancePtrLocalVar),
					new CodeInstruction(OpCodes.Mkrefany, type),
				};
			else
				return LdlocAndMkrefany(type, instancePtrLocalVar);
		}

		public IEnumerable<CodeInstruction> Refanyval(Type type)
		{
			Trace($"Refanyval({MemberToString(type)}");
			// TODO: Handle generic parameter type by looking at type of instance on top of the CIL stack.
			// Note: We cannot use the box + unbox trick for TypedReference, since TypedReference is a special value that cannot be boxed.
			// So we must use a TypedReference variable and load its address.
			using var typedRefLocalVar = GetLocalVar(typeof_TypedReference, isPinned: false);
			yield return Stloc(typedRefLocalVar);
			foreach (var instruction in LdlocaAndRefanyval(type, typedRefLocalVar))
				yield return instruction;
		}

		IEnumerable<CodeInstruction> LdlocaAndRefanyval(Type type, LocalBuilder typedRefLocalVar)
		{
			yield return Ldloca(typedRefLocalVar);
			//yield return new CodeInstruction(OpCodes.Conv_U);
			// See comments in LdlocAndMkrefany on TypedReference memory layout.
			if (isMono)
			{
				// Mono is strict about needing a managed pointer to a RuntimeTypeHandle (and not just a native int pointer)
				// for the call to RuntimeTypeHandle.get_Value. So ldobj, then use the hacky box + unbox trick to get the
				// address of the value type on the CIL stack without needing a local var.
				yield return new CodeInstruction(OpCodes.Ldobj, typeof_RuntimeTypeHandle);
				yield return new CodeInstruction(OpCodes.Box, typeof_RuntimeTypeHandle);
				yield return new CodeInstruction(OpCodes.Unbox, typeof_RuntimeTypeHandle);
				yield return new CodeInstruction(OpCodes.Call, methodof_RuntimeTypeHandle_get_Value);
			}
			else
			{
				yield return LdcI(IntPtr.Size);
				yield return new CodeInstruction(OpCodes.Add);
				yield return new CodeInstruction(OpCodes.Ldind_I);
			}
			yield return new CodeInstruction(OpCodes.Ldtoken, type);
			// box + unbox is a hacky way to get the address of a value type on the CIL stack without needing a local var.
			yield return new CodeInstruction(OpCodes.Box, typeof_RuntimeTypeHandle);
			yield return new CodeInstruction(OpCodes.Unbox, typeof_RuntimeTypeHandle);
			yield return new CodeInstruction(OpCodes.Call, methodof_RuntimeTypeHandle_get_Value);
			var afterThrowLabel = ilGenerator.DefineLabel();
			// Note: Cannot determine whether short-form (conditional) branch instructions would suffice.
			// That can only be determined right before finalizing the method being transpiled.
			// So using long-form (conditional) branch instructions.
			yield return new CodeInstruction(OpCodes.Beq, afterThrowLabel);
			yield return new CodeInstruction(OpCodes.Newobj, methodof_InvalidCastException_ctor0);
			yield return new CodeInstruction(OpCodes.Throw);
			var instruction = Ldloca(typedRefLocalVar);
			instruction.labels.Add(afterThrowLabel);
			yield return instruction;
			if (isMono)
			{
				yield return LdcI(IntPtr.Size);
				yield return new CodeInstruction(OpCodes.Add);
			}
			yield return new CodeInstruction(OpCodes.Ldind_I);
		}

		// Instructions for getting the Type at runtime, predetermined at compile time.
		static IEnumerable<CodeInstruction> GetTypeFromToken(Type type)
		{
			return new[]
			{
				new CodeInstruction(OpCodes.Ldtoken, type),
				new CodeInstruction(OpCodes.Call, methodof_Type_GetTypeFromHandle),
			};
		}

		// Instructions for getting the FieldInfo at runtime, predetermined at compile time.
		static IEnumerable<CodeInstruction> GetFieldInfoFromToken(FieldInfo field)
		{
			yield return new CodeInstruction(OpCodes.Ldtoken, field);
			var declaringType = field.DeclaringType;
			if (declaringType.IsGenericType)
			{
				yield return new CodeInstruction(OpCodes.Ldtoken, declaringType);
				yield return new CodeInstruction(OpCodes.Call, methodof_FieldInfo_GetFieldFromHandle2);
			}
			else
			{
				yield return new CodeInstruction(OpCodes.Call, methodof_FieldInfo_GetFieldFromHandle1);
			}
		}

		// Instructions for getting the MethodInfo at runtime, predetermined at compile time.
		static IEnumerable<CodeInstruction> GetMethodInfoFromToken(MethodBase methodBase)
		{
			yield return new CodeInstruction(OpCodes.Ldtoken, methodBase);
			var declaringType = methodBase.DeclaringType;
			if (declaringType.IsGenericType)
			{
				yield return new CodeInstruction(OpCodes.Ldtoken, declaringType);
				yield return new CodeInstruction(OpCodes.Call, methodof_MethodBase_GetMethodFromHandle2);
			}
			else
			{
				yield return new CodeInstruction(OpCodes.Call, methodof_MethodBase_GetMethodFromHandle1);
			}
		}

		// Instructions for getting the MethodInfo at runtime, resolving virtual methods to the actual method from an instance.
		// Precondition: an object is on top of the CIL stack, methodBase is the MethodBase operand of the current instruction.
		static IEnumerable<CodeInstruction> GetVirtualMethodInfoFromInstance(MethodBase methodBase)
		{
			// Equivalent at runtime to: obj.GetType().GetMethod(
			// name: <method name>,
			// bindingAttr: BindingFlags.Instance | <BindingFlags.Public or BindingFlags.NonPublic>,
			// binder: null,
			// types: new[] { <parameter type 1>, ... },
			// modifiers: null);
			// Allow this to NRE if the object is null.
			yield return new CodeInstruction(OpCodes.Call, methodof_object_GetType);
			yield return new CodeInstruction(OpCodes.Ldstr, methodBase.Name);
			var bindingFlags = BindingFlags.Instance | (methodBase.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic);
			yield return LdcI((int)bindingFlags);
			yield return new CodeInstruction(OpCodes.Ldnull); // binder
			var parameters = methodBase.GetParameters();
			yield return LdcI(parameters.Length);
			yield return new CodeInstruction(OpCodes.Newarr, typeof_Type);
			for (var i = 0; i < parameters.Length; i++)
			{
				yield return new CodeInstruction(OpCodes.Dup);
				yield return LdcI(i);
				foreach (var instruction in GetTypeFromToken(parameters[i].ParameterType))
					yield return instruction;
				yield return new CodeInstruction(OpCodes.Stelem_Ref);
			}
			yield return new CodeInstruction(OpCodes.Ldnull); // modifiers
			yield return new CodeInstruction(OpCodes.Call, methodof_Type_GetMethod);
		}

		// TODO: Move to another class as an extension method.
		static CodeInstruction LdcI(IntPtr intPtr)
		{
			// Note: There are no ldc.i.* instructions, so caller should use conv.i or conv.u afterwards to get an actual (unsigned) native int.
			if (IntPtr.Size is 4)
				return LdcI(intPtr.ToInt32());
			else
				return LdcI(intPtr.ToInt64());
		}

		// TODO: Move to another class as an extension method.
		static CodeInstruction LdcI(long value)
		{
			// Note: There are no ldc.i8.* instructions, so caller should use conv.i8 afterwards to get an actual long.
			return value switch
			{
				-1 => new CodeInstruction(OpCodes.Ldc_I4_M1),
				0 => new CodeInstruction(OpCodes.Ldc_I4_0),
				1 => new CodeInstruction(OpCodes.Ldc_I4_1),
				2 => new CodeInstruction(OpCodes.Ldc_I4_2),
				3 => new CodeInstruction(OpCodes.Ldc_I4_3),
				4 => new CodeInstruction(OpCodes.Ldc_I4_4),
				5 => new CodeInstruction(OpCodes.Ldc_I4_5),
				6 => new CodeInstruction(OpCodes.Ldc_I4_6),
				7 => new CodeInstruction(OpCodes.Ldc_I4_7),
				8 => new CodeInstruction(OpCodes.Ldc_I4_8),
				_ when value >= sbyte.MinValue && value <= sbyte.MaxValue => new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)value),
				_ when value >= int.MinValue && value <= int.MaxValue => new CodeInstruction(OpCodes.Ldc_I4, (int)value),
				_ => new CodeInstruction(OpCodes.Ldc_I8, value),
			};
		}

		// TODO: Move to another class as an extension method.
		static CodeInstruction LdcI(int value)
		{
			return value switch
			{
				-1 => new CodeInstruction(OpCodes.Ldc_I4_M1),
				0 => new CodeInstruction(OpCodes.Ldc_I4_0),
				1 => new CodeInstruction(OpCodes.Ldc_I4_1),
				2 => new CodeInstruction(OpCodes.Ldc_I4_2),
				3 => new CodeInstruction(OpCodes.Ldc_I4_3),
				4 => new CodeInstruction(OpCodes.Ldc_I4_4),
				5 => new CodeInstruction(OpCodes.Ldc_I4_5),
				6 => new CodeInstruction(OpCodes.Ldc_I4_6),
				7 => new CodeInstruction(OpCodes.Ldc_I4_7),
				8 => new CodeInstruction(OpCodes.Ldc_I4_8),
				_ when value >= sbyte.MinValue && value <= sbyte.MaxValue => new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)value),
				_ => new CodeInstruction(OpCodes.Ldc_I4, value),
			};
		}

		// TODO: Move to another class as an extension method.
		static CodeInstruction Ldloc(LocalBuilder localVar)
		{
			var localIndex = localVar.LocalIndex;
			return localIndex switch
			{
				0 => new CodeInstruction(OpCodes.Ldloc_0),
				1 => new CodeInstruction(OpCodes.Ldloc_1),
				2 => new CodeInstruction(OpCodes.Ldloc_2),
				3 => new CodeInstruction(OpCodes.Ldloc_3),
				_ when localIndex <= byte.MaxValue => new CodeInstruction(OpCodes.Ldloc_S, localVar),
				_ => new CodeInstruction(OpCodes.Ldloc, localVar),
			};
		}

		// TODO: Move to another class as an extension method.
		static CodeInstruction Ldloca(LocalBuilder localVar)
		{
			if (localVar.LocalIndex <= byte.MaxValue)
				return new CodeInstruction(OpCodes.Ldloca_S, localVar);
			else
				return new CodeInstruction(OpCodes.Ldloca, localVar);
		}

		// TODO: Move to another class as an extension method.
		static CodeInstruction Stloc(LocalBuilder localVar)
		{
			var localIndex = localVar.LocalIndex;
			return localIndex switch
			{
				0 => new CodeInstruction(OpCodes.Stloc_0),
				1 => new CodeInstruction(OpCodes.Stloc_1),
				2 => new CodeInstruction(OpCodes.Stloc_2),
				3 => new CodeInstruction(OpCodes.Stloc_3),
				_ when localIndex <= byte.MaxValue => new CodeInstruction(OpCodes.Stloc_S, localVar),
				_ => new CodeInstruction(OpCodes.Stloc, localVar),
			};
		}

		// TODO: Move to another class as an extension method.
		static CodeInstruction Ldind(Type type)
		{
			return Type.GetTypeCode(type) switch
			{
				TypeCode.Boolean => new CodeInstruction(OpCodes.Ldind_I1),
				TypeCode.SByte => new CodeInstruction(OpCodes.Ldind_I1),
				TypeCode.Byte => new CodeInstruction(OpCodes.Ldind_U1),
				TypeCode.Int16 => new CodeInstruction(OpCodes.Ldind_I2),
				// Character is encoded in UTF-16 so effectively an unsigned 16-bit integer.
				TypeCode.Char => new CodeInstruction(OpCodes.Ldind_U2),
				TypeCode.UInt16 => new CodeInstruction(OpCodes.Ldind_U2),
				TypeCode.Int32 => new CodeInstruction(OpCodes.Ldind_I4),
				TypeCode.UInt32 => new CodeInstruction(OpCodes.Ldind_U4),
				TypeCode.Int64 => new CodeInstruction(OpCodes.Ldind_I8),
				// Note: There is no ldind.u8 instruction, so defaulting to ldind.i8.
				// Caller should use conv.u8 instruction afterwards to get an actual ulong.
				TypeCode.UInt64 => new CodeInstruction(OpCodes.Ldind_I8),
				TypeCode.Single => new CodeInstruction(OpCodes.Ldind_R4),
				TypeCode.Double => new CodeInstruction(OpCodes.Ldind_R8),
				// IntPtr is treated specially in CIL as native int, but Type.GetTypeCode(typeof(IntPtr)) returns TypeCode.Object.
				TypeCode.Object when type == typeof_IntPtr => new CodeInstruction(OpCodes.Ldind_I),
				// decimal and DateTime are value types.
				TypeCode.Decimal => new CodeInstruction(OpCodes.Ldobj, type),
				TypeCode.DateTime => new CodeInstruction(OpCodes.Ldobj, type),
				TypeCode.Object when type.IsValueType => new CodeInstruction(OpCodes.Ldobj, type),
				// Default case handles string, DBNull, null (should be impossible), and any other object type.
				_ => new CodeInstruction(OpCodes.Ldind_Ref),
			};
		}

		static CodeInstruction Stind(Type type)
		{
			// Note: There are no stind.u* instructions. The corresponding stind.i* instructions should work fine for unsigned integers.
			return Type.GetTypeCode(type) switch
			{
				TypeCode.Boolean => new CodeInstruction(OpCodes.Stind_I1),
				TypeCode.SByte => new CodeInstruction(OpCodes.Stind_I1),
				TypeCode.Byte => new CodeInstruction(OpCodes.Stind_I1),
				TypeCode.Int16 => new CodeInstruction(OpCodes.Stind_I2),
				// Character is encoded in UTF-16 so effectively an unsigned 16-bit integer.
				TypeCode.Char => new CodeInstruction(OpCodes.Stind_I2),
				TypeCode.UInt16 => new CodeInstruction(OpCodes.Stind_I2),
				TypeCode.Int32 => new CodeInstruction(OpCodes.Stind_I4),
				TypeCode.UInt32 => new CodeInstruction(OpCodes.Stind_I4),
				TypeCode.Int64 => new CodeInstruction(OpCodes.Stind_I8),
				TypeCode.UInt64 => new CodeInstruction(OpCodes.Stind_I8),
				TypeCode.Single => new CodeInstruction(OpCodes.Stind_R4),
				TypeCode.Double => new CodeInstruction(OpCodes.Stind_R8),
				// IntPtr is treated specially in CIL as native int, but Type.GetTypeCode(typeof(IntPtr)) returns TypeCode.Object.
				TypeCode.Object when type == typeof_IntPtr => new CodeInstruction(OpCodes.Stind_I),
				// decimal and DateTime are value types.
				TypeCode.Decimal => new CodeInstruction(OpCodes.Stobj, type),
				TypeCode.DateTime => new CodeInstruction(OpCodes.Stobj, type),
				TypeCode.Object when type.IsValueType => new CodeInstruction(OpCodes.Stobj, type),
				// Default case handles string, DBNull, null (should be impossible), and any other object type.
				_ => new CodeInstruction(OpCodes.Stind_Ref),
			};
		}

		// TODO: Move to another class as an extension method.
		static CodeInstruction Ldelem(Type type)
		{
			return Type.GetTypeCode(type) switch
			{
				TypeCode.Boolean => new CodeInstruction(OpCodes.Ldelem_I1),
				TypeCode.SByte => new CodeInstruction(OpCodes.Ldelem_I1),
				TypeCode.Byte => new CodeInstruction(OpCodes.Ldelem_U1),
				TypeCode.Int16 => new CodeInstruction(OpCodes.Ldelem_I2),
				// Character is encoded in UTF-16 so effectively an unsigned 16-bit integer.
				TypeCode.Char => new CodeInstruction(OpCodes.Ldelem_U2),
				TypeCode.UInt16 => new CodeInstruction(OpCodes.Ldelem_U2),
				TypeCode.Int32 => new CodeInstruction(OpCodes.Ldelem_I4),
				TypeCode.UInt32 => new CodeInstruction(OpCodes.Ldelem_U4),
				TypeCode.Int64 => new CodeInstruction(OpCodes.Ldelem_I8),
				// Note: There is no ldelem.u8 instruction, so defaulting to ldelem.i8.
				// Caller should use conv.u8 instruction afterwards to get an actual ulong.
				TypeCode.UInt64 => new CodeInstruction(OpCodes.Ldelem_I8),
				TypeCode.Single => new CodeInstruction(OpCodes.Ldelem_R4),
				TypeCode.Double => new CodeInstruction(OpCodes.Ldelem_R8),
				// IntPtr is treated specially in CIL as native int, but Type.GetTypeCode(typeof(IntPtr)) returns TypeCode.Object.
				TypeCode.Object when type == typeof_IntPtr => new CodeInstruction(OpCodes.Ldelem_I),
				// decimal and DateTime are value types.
				TypeCode.Decimal => new CodeInstruction(OpCodes.Ldelem, type),
				TypeCode.DateTime => new CodeInstruction(OpCodes.Ldelem, type),
				TypeCode.Object when type.IsValueType => new CodeInstruction(OpCodes.Ldelem, type),
				// Default case handles string, DBNull, null (should be impossible), and any other object type.
				_ => new CodeInstruction(OpCodes.Ldelem_Ref),
			};
		}

		static CodeInstruction Stelem(Type type)
		{
			// Note: There are no stelem.u* instructions. The corresponding stelem.i* instructions should work fine for unsigned integers.
			return Type.GetTypeCode(type) switch
			{
				TypeCode.Boolean => new CodeInstruction(OpCodes.Stelem_I1),
				TypeCode.SByte => new CodeInstruction(OpCodes.Stelem_I1),
				TypeCode.Byte => new CodeInstruction(OpCodes.Stelem_I1),
				TypeCode.Int16 => new CodeInstruction(OpCodes.Stelem_I2),
				// Character is encoded in UTF-16 so effectively an unsigned 16-bit integer.
				TypeCode.Char => new CodeInstruction(OpCodes.Stelem_I2),
				TypeCode.UInt16 => new CodeInstruction(OpCodes.Stelem_I2),
				TypeCode.Int32 => new CodeInstruction(OpCodes.Stelem_I4),
				TypeCode.UInt32 => new CodeInstruction(OpCodes.Stelem_I4),
				TypeCode.Int64 => new CodeInstruction(OpCodes.Stelem_I8),
				TypeCode.UInt64 => new CodeInstruction(OpCodes.Stelem_I8),
				TypeCode.Single => new CodeInstruction(OpCodes.Stelem_R4),
				TypeCode.Double => new CodeInstruction(OpCodes.Stelem_R8),
				// IntPtr is treated specially in CIL as native int, but Type.GetTypeCode(typeof(IntPtr)) returns TypeCode.Object.
				TypeCode.Object when type == typeof_IntPtr => new CodeInstruction(OpCodes.Stelem_I),
				// decimal and DateTime are value types.
				TypeCode.Decimal => new CodeInstruction(OpCodes.Stelem, type),
				TypeCode.DateTime => new CodeInstruction(OpCodes.Stelem, type),
				TypeCode.Object when type.IsValueType => new CodeInstruction(OpCodes.Stelem, type),
				// Default case handles string, DBNull, null (should be impossible), and any other object type.
				_ => new CodeInstruction(OpCodes.Stelem_Ref),
			};
		}

		static readonly Type typeof_object = typeof(object);
		static readonly Type typeof_object_array = typeof(object[]);
		static readonly Type typeof_int = typeof(int);
		static readonly Type typeof_string = typeof(string);
		static readonly Type typeof_Array = typeof(Array);
		static readonly Type typeof_Type = typeof(Type);
		static readonly Type typeof_MethodBase = typeof(MethodBase);
		static readonly Type typeof_FieldInfo = typeof(FieldInfo);
		static readonly Type typeof_RuntimeTypeHandle = typeof(RuntimeTypeHandle);
		static readonly Type typeof_RuntimeMethodHandle = typeof(RuntimeMethodHandle);
		static readonly Type typeof_MonoTODOAttribute = Type.GetType("System.MonoTODOAttribute");
		static readonly Type typeof_NullReferenceException = typeof(NullReferenceException);
		static readonly Type typeof_InvalidCastException = typeof(InvalidCastException);
		static readonly Type typeof_TypedReference = typeof(TypedReference);
		static readonly Type typeof_IntPtr = typeof(IntPtr);

		static readonly MethodInfo methodof_object_GetType = typeof_object.GetMethod(nameof(object.GetType));
		static readonly MethodInfo methodof_object_MemberwiseClone = typeof_object.GetMethod("MemberwiseClone", AccessTools.all);
		static readonly MethodInfo methodof_Array_CreateInstance =
			typeof_Array.GetMethod(nameof(Array.CreateInstance), new[] { typeof_Type, typeof_int });
		static readonly MethodInfo methodof_Type_GetTypeFromHandle = typeof_Type.GetMethod(nameof(Type.GetTypeFromHandle));
		static readonly MethodInfo methodof_Type_GetMethod = typeof_Type.GetMethod(nameof(Type.GetMethod),
			new[] { typeof(string), typeof(BindingFlags), typeof(Binder), typeof(Type[]), typeof(ParameterModifier[]) });
		static readonly MethodInfo methodof_Type_get_FullName = typeof_Type.GetProperty(nameof(Type.FullName)).GetGetMethod();
		static readonly MethodInfo methodof_Type_IsInstanceOfType = typeof_Type.GetMethod(nameof(Type.IsInstanceOfType));
		static readonly MethodInfo methodof_MethodBase_GetMethodFromHandle1 =
			typeof_MethodBase.GetMethod(nameof(MethodBase.GetMethodFromHandle), new[] { typeof(RuntimeMethodHandle) });
		static readonly MethodInfo methodof_MethodBase_GetMethodFromHandle2 =
			typeof_MethodBase.GetMethod(nameof(MethodBase.GetMethodFromHandle), new[] { typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle) });
		static readonly MethodInfo methodof_MethodBase_get_MethodHandle =
			typeof_MethodBase.GetProperty(nameof(MethodBase.MethodHandle)).GetGetMethod();
		static readonly MethodInfo methodof_MethodBase_Invoke =
			typeof_MethodBase.GetMethod(nameof(MethodBase.Invoke), new[] { typeof_object, typeof(object[]) });
		static readonly MethodInfo methodof_RuntimeTypeHandle_get_Value =
			typeof_RuntimeTypeHandle.GetProperty(nameof(RuntimeTypeHandle.Value)).GetGetMethod();
		static readonly MethodInfo methodof_RuntimeMethodHandle_GetFunctionPointer =
			typeof_RuntimeMethodHandle.GetMethod(nameof(RuntimeMethodHandle.GetFunctionPointer));
		static readonly MethodInfo methodof_FieldInfo_GetFieldFromHandle1 =
			typeof_FieldInfo.GetMethod(nameof(FieldInfo.GetFieldFromHandle), new[] { typeof(RuntimeFieldHandle) });
		static readonly MethodInfo methodof_FieldInfo_GetFieldFromHandle2 =
			typeof_FieldInfo.GetMethod(nameof(FieldInfo.GetFieldFromHandle), new[] { typeof(RuntimeFieldHandle), typeof(RuntimeTypeHandle) });
		static readonly MethodInfo methodof_FieldInfo_GetValue = typeof_FieldInfo.GetMethod(nameof(FieldInfo.GetValue));
		static readonly MethodInfo methodof_FieldInfo_SetValue =
			typeof_FieldInfo.GetMethod(nameof(FieldInfo.SetValue), new[] { typeof_object, typeof_object });
		static readonly MethodInfo methodof_FieldInfo_GetValueDirect =
			NullIfHasMonoTODOAttribute(typeof_FieldInfo.GetMethod(nameof(FieldInfo.GetValueDirect)));
		static readonly MethodInfo methodof_FieldInfo_SetValueDirect =
			NullIfHasMonoTODOAttribute(typeof_FieldInfo.GetMethod(nameof(FieldInfo.SetValueDirect)));
		static readonly MethodInfo methodof_string_Format3 =
			typeof_string.GetMethod(nameof(string.Format), new[] { typeof_string, typeof_object, typeof_object });
		static readonly ConstructorInfo methodof_NullReferenceException_ctor0 = typeof_NullReferenceException.GetConstructor(Type.EmptyTypes);
		static readonly ConstructorInfo methodof_InvalidCastException_ctor0 = typeof_InvalidCastException.GetConstructor(Type.EmptyTypes);
		static readonly ConstructorInfo methodof_InvalidCastException_ctor1 = typeof_InvalidCastException.GetConstructor(new[] { typeof_string });

		static readonly bool isMono = !(Type.GetType("Mono.Runtime") is null);

		static T NullIfHasMonoTODOAttribute<T>(T member) where T : MemberInfo
		{
			if (typeof_MonoTODOAttribute is null || !member.IsDefined(typeof_MonoTODOAttribute, inherit: false))
				return member;
			Trace($"{MemberToString(member)} has {typeof_MonoTODOAttribute} - avoiding usage of it");
			return null;
		}

		static string MemberToString(MemberInfo member) => TypeCopier.MemberToString(member);

		static string SafeToString(object obj) => obj?.ToString() ?? "null";

		[Conditional("TRACE_LOGGING")]
		static void Trace(string str) => Logging.Log(str);
	}
}
