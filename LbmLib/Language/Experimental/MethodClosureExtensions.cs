using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace LbmLib.Language.Experimental
{
	public static class MethodClosureExtensions
	{
		const bool ClosureMethodGenerateDebugDll = false;

		// TODO: Make this a top-level class since it's public.
		public sealed class ClosureMethod : MethodInfo
		{
			readonly MethodInfo method;
			readonly MethodAttributes methodAttributes;
			readonly ParameterInfo[] nonFixedParameterInfos;

			// Closures is the actual registry of fixedArguments arrays, with closureKey being an index into it.
			// It's an object[] rather than a custom struct so that other assemblies can more easily access it, as is required with DebugDynamicMethodBuilder,
			// since other assemblies can only access public structures, fields, etc. (or else would require expensive reflection within the dynamically-generated method).
			// DebugDynamicMethodBuilder's build type will have its own field that stores a reference to this closures.
			readonly int closureKey;
			internal static readonly List<object[]> Closures = new List<object[]>();
			static int MinimumFreeClosureKey = 0;

			public MethodInfo OriginalMethod { get; }

			public bool IsBoundInstanceMethod => IsStatic && !OriginalMethod.IsStatic;

			internal object[] FixedArguments
			{
				get => Closures[closureKey];
				set => Closures[closureKey] = value;
			}

			public object[] GetFixedArguments()
			{
				var fixedArguments = FixedArguments;
				var fixedArgumentCount = fixedArguments.Length;
				var fixedArgumentsCopy = new object[fixedArgumentCount];
				Array.Copy(fixedArguments, fixedArgumentsCopy, fixedArgumentCount);
				return fixedArgumentsCopy;
			}

			internal ClosureMethod(int closureKey, MethodInfo method, MethodAttributes methodAttributes, ParameterInfo[] nonFixedParameterInfos,
				MethodInfo originalMethod, object[] fixedArguments)
			{
				this.closureKey = closureKey;
				this.method = method;
				this.methodAttributes = methodAttributes;
				this.nonFixedParameterInfos = nonFixedParameterInfos;
				OriginalMethod = originalMethod;
				FixedArguments = fixedArguments;
			}

			~ClosureMethod()
			{
				lock (Closures)
				{
					FixedArguments = null;
					if (closureKey < MinimumFreeClosureKey)
					{
						MinimumFreeClosureKey = closureKey;
					}
				}
				//Logging.Log($"DEBUG FreeClosureKey: closureKey={closureKey}, minimumFreeClosureKey={MinimumFreeClosureKey}");
			}

			internal static int ReserveNextFreeClosureKey()
			{
				// XXX: This is O(n) even with an minimum key optimization, but it should suffice as long as closures aren't created in the thousands or so.
				lock (Closures)
				{
					var closuresCount = Closures.Count;
					for (var closureKey = MinimumFreeClosureKey; closureKey < closuresCount; closureKey++)
					{
						if (Closures[closureKey] is null)
						{
							MinimumFreeClosureKey = closureKey + 1;
							return closureKey;
						}
					}
					MinimumFreeClosureKey = closuresCount + 1;
					Closures.Add(null);
					return closuresCount;
				}
			}

			public override string ToString() =>
				$"ClosureMethod{{closureKey={closureKey}, method={method.ToDebugString()}, attributes={methodAttributes}, " +
				$"originalMethod={OriginalMethod.ToDebugString()}, fixedArguments={FixedArguments.ToDebugString()}}}";

			public string ToDebugString() => ToString();

			public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
			{
				if (IsStatic)
				{
					// We don't support constructors, so obj must be null.
					if (!(obj is null))
						throw new ArgumentNullException(nameof(obj));
					return method.Invoke(null, invokeAttr, binder, parameters, culture);
				}
				else
				{
					// method is always a static method, so if our Attributes say that we're an instance method, fake it.
					var parameterCount = parameters.Length;
					var actualParameters = new object[parameterCount + 1];
					actualParameters[0] = obj;
					Array.Copy(parameters, 0, actualParameters, 1, parameterCount);
					return method.Invoke(null, invokeAttr, binder, actualParameters, culture);
				}
			}

			// Note: There's no way to ensure that MethodBase.GetMethodFromHandle(closureMethod.MethodHandle) == closureMethod,
			// so just don't support MethodHandle.
			public override RuntimeMethodHandle MethodHandle =>
				throw new InvalidOperationException("The requested operation is invalid for " + nameof(ClosureMethod));

			public override ICustomAttributeProvider ReturnTypeCustomAttributes => method.ReturnTypeCustomAttributes;

			public override MethodAttributes Attributes => methodAttributes;

			public override Type DeclaringType => method.DeclaringType;

			public override string Name => method.Name;

			public override Type ReflectedType => method.ReflectedType;

			public override MethodInfo GetBaseDefinition() => this;

			public override object[] GetCustomAttributes(bool inherit) => method.GetCustomAttributes(inherit);

			public override object[] GetCustomAttributes(Type attributeType, bool inherit) => method.GetCustomAttributes(attributeType, inherit);

			public override MethodImplAttributes GetMethodImplementationFlags() => method.GetMethodImplementationFlags();

			public override ParameterInfo[] GetParameters() => nonFixedParameterInfos;

			public override bool IsDefined(Type attributeType, bool inherit) => method.IsDefined(attributeType, inherit);

#if !NET35
			override
#endif
			public Delegate CreateDelegate(Type delegateType)
			{
				if (!IsStatic)
					throw new ArgumentException("Cannot bind to the target method because its signature or security transparency is not compatible with that of the delegate type.");
				if (method is DynamicMethod dynamicMethod)
					return dynamicMethod.CreateDelegate(delegateType);
				else
					return Delegate.CreateDelegate(delegateType, method);
			}

#if !NET35
			override
#endif
			public Delegate CreateDelegate(Type delegateType, object target)
			{
				if (target is null)
					throw new ArgumentNullException(nameof(target));
				if (IsStatic)
					throw new ArgumentException("Cannot bind to the target method because its signature or security transparency is not compatible with that of the delegate type.");
				return method.DynamicBind(target).CreateDelegate(delegateType);
			}
		}

#if NET35
		public static Delegate CreateDelegate(this MethodInfo method, Type delegateType)
		{
			switch (method)
			{
			case DynamicMethod dynamicMethod:
				return dynamicMethod.CreateDelegate(delegateType);
			case ClosureMethod closureMethod:
				return closureMethod.CreateDelegate(delegateType);
			default:
				return Delegate.CreateDelegate(delegateType, method);
			}
		}

		public static Delegate CreateDelegate(this MethodInfo method, Type delegateType, object target)
		{
			switch (method)
			{
			case DynamicMethod dynamicMethod:
				return dynamicMethod.CreateDelegate(delegateType, target);
			case ClosureMethod closureMethod:
				return closureMethod.CreateDelegate(delegateType, target);
			default:
				return Delegate.CreateDelegate(delegateType, target, method);
			}
		}
#endif

		public static ClosureMethod DynamicBind(this MethodInfo method, object target)
		{
			if (target is null)
				throw new ArgumentNullException(nameof(target));
			if (method.IsStatic)
				throw new ArgumentException($"method {method.ToDebugString()} cannot be a static method");
			if (!method.DeclaringType.IsAssignableFrom(target.GetType()))
				throw new ArgumentException($"target's type ({target.GetType().ToDebugString()}) " +
					$"must be assignable to method.DeclaringType ({method.DeclaringType.ToDebugString()})");
			if (method is ClosureMethod closureMethod)
				return CreateClosureMethod(closureMethod.OriginalMethod, target, closureMethod.FixedArguments);
			else
				return CreateClosureMethod(method, target, new object[0]);
		}

		static readonly MethodInfo GetCurrentMethodMethod = typeof(MethodBase).GetMethod(nameof(MethodBase.GetCurrentMethod));
		static readonly MethodInfo ListItemGetMethod = typeof(List<object[]>).GetProperty("Item").GetGetMethod();
		static readonly ConstructorInfo InvalidOperationExceptionConstructor = typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) });
		static readonly MethodInfo MethodBaseToDebugStringMethod = typeof(DebugExtensions).GetMethod(nameof(DebugExtensions.ToDebugString), new[] { typeof(MethodBase) });
		static readonly MethodInfo StringFormat2Method = typeof(string).GetMethod(nameof(string.Format), new[] { typeof(string), typeof(object), typeof(object) });

		public static ClosureMethod DynamicPartialApply(this MethodInfo method, params object[] fixedArguments)
		{
			if (fixedArguments is null)
				throw new ArgumentNullException(nameof(fixedArguments));
			if (method is ClosureMethod closureMethod)
			{
				var existingFixedArguments = closureMethod.FixedArguments;
				var existingFixedArgumentStartIndex = 0;
				var fixedThisArgument = default(object);
				if (closureMethod.IsBoundInstanceMethod)
				{
					fixedThisArgument = existingFixedArguments[0];
					existingFixedArgumentStartIndex++;
				}
				var existingFixedArgumentCount = existingFixedArguments.Length - existingFixedArgumentStartIndex;
				var fixedArgumentCount = fixedArguments.Length;
				var combinedFixedArguments = new object[existingFixedArgumentCount + fixedArgumentCount];
				Array.Copy(existingFixedArguments, existingFixedArgumentStartIndex, combinedFixedArguments, 0, existingFixedArgumentCount);
				Array.Copy(fixedArguments, 0, combinedFixedArguments, existingFixedArgumentCount, fixedArgumentCount);
				return CreateClosureMethod(closureMethod.OriginalMethod, fixedThisArgument, combinedFixedArguments);
			}
			else
			{
				return CreateClosureMethod(method, null, fixedArguments);
			}
		}

		static ClosureMethod CreateClosureMethod(MethodInfo method, object fixedThisArgument, object[] fixedArguments)
		{
			var parameters = method.GetParameters();
			var totalArgumentCount = parameters.Length;
			var fixedArgumentCount = fixedArguments.Length;
			var isStatic = method.IsStatic;
			var declaringType = method.DeclaringType;
			var returnType = method.ReturnType;
			var methodAttributes = method.Attributes;

			// Ref, in, and out parameters aren't allowed to be fixed arguments.
			// TODO: Somehow support this - such fixed arguments would need to be stored in a temp params and the passed via ldloca (ldelema for closure).
			for (var index = 0; index < fixedArgumentCount; index++)
			{
				var parameter = parameters[index];
				if (parameter.ParameterType.IsByRef)
					throw new ArgumentException("Cannot partial apply with a fixed argument that is a ref, in, or out parameter: " + parameter);
			}

			// The ParameterInfo array that will be returned from ClosureMethod.GetParameters() is simply the non-fixed parameters.
			var nonFixedArgumentCount = totalArgumentCount - fixedArgumentCount;
			var nonFixedParameterInfos = new ParameterInfo[nonFixedArgumentCount];
			for (var index = 0; index < nonFixedArgumentCount; index++)
			{
				nonFixedParameterInfos[index] = parameters[fixedArgumentCount + index];
			}

			// Since DynamicMethod can only be created as a static method, if method is an instance method and fixedThisArgument is null
			// (and thus will be a non-bound instance method), we will need to prepend an additional non-fixed argument for the instance
			// to the dynamic method's parameter type array (not to be confused with ClosureMethod.GetParameters() above).
			var prefixArgumentCount = 0;
			if (!isStatic)
			{
				if (fixedThisArgument is null)
				{
					prefixArgumentCount++;
					nonFixedArgumentCount++;
				}
				else
				{
					// If fixedThisArgument is not null (and thus will be a bound instance method), prepend the fixed arguments with fixedThisArgument.
					fixedArgumentCount++;
					var newFixedArguments = new object[fixedArgumentCount];
					newFixedArguments[0] = fixedThisArgument;
					Array.Copy(fixedArguments, 0, newFixedArguments, 1, fixedArguments.Length);
					fixedArguments = newFixedArguments;
					// A bound instance method will effectively be a static method from ClosureMethod's standpoint.
					methodAttributes |= MethodAttributes.Static;
				}
			}

			var nonFixedParameterTypes = new Type[nonFixedArgumentCount];
			// See above - need to prepend an additional non-fixed argument for non-bound instance methods to the dynamic method's parameter type array.
			if (prefixArgumentCount == 1)
				nonFixedParameterTypes[0] = declaringType;
			for (var index = prefixArgumentCount; index < nonFixedArgumentCount; index++)
			{
				nonFixedParameterTypes[index] = nonFixedParameterInfos[index - prefixArgumentCount].ParameterType;
			}

			var closureKey = ClosureMethod.ReserveNextFreeClosureKey();
			var methodBuilder = (ClosureMethodGenerateDebugDll ?
				(IDynamicMethodBuilderFactory)new DebugDynamicMethodBuilder.Factory() : new DynamicMethodBuilder.Factory()).Create(
				method.Name + "_Closure_" + closureKey,
				// Some (all?) .NET implementations only accept the following MethodAttributes/CallingConventions for DynamicMethod.
				MethodAttributes.Public | MethodAttributes.Static,
				CallingConventions.Standard,
				declaringType,
				returnType,
				nonFixedParameterTypes);
			if (prefixArgumentCount == 1)
				methodBuilder.DefineParameter(0, ParameterAttributes.None, "instance");
			for (int index = prefixArgumentCount; index < nonFixedArgumentCount; index++)
			{
				var parameter = nonFixedParameterInfos[index - prefixArgumentCount];
				// DefineParameter(0,...) refers to the return value, so for actual parameters, it's practically 1-based rather than 0-based.
				methodBuilder.DefineParameter(index + 1, parameter.Attributes, parameter.Name);
				// XXX: Do any custom attributes like ParamArrayAttribute need to be copied too?
				// There's no good generic way to copy attributes as far as I can tell, since CustomAttributeBuilder is very cumbersome to use.
			}
			var ilGenerator = methodBuilder.GetILGenerator();

			// Create the fixed arguments that are stored in the closure, and loaded within the dynamic method if needed.
			// Both constant and non-constant arguments are stored there, even if only the latter are needed within the dynamic method,
			// since DynamicBind and DynamicPartialApply need to the full array of fixed arguments if the method passed to them is already a ClosureMethod.
			var fixedArgumentsVar = default(LocalBuilder);
			if (fixedArguments.Any(fixedArgument => !ilGenerator.CanEmitConstant(fixedArgument)))
			{
				//ilGenerator.Emit(OpCodes.Ldstr, "DEBUG ClosureMethod: closureKey={0} inside method=\"{1}\"");
				//ilGenerator.EmitLdcI4(closureKey);
				//ilGenerator.Emit(OpCodes.Box, typeof(int));
				//ilGenerator.Emit(OpCodes.Call, GetCurrentMethodMethod);
				//ilGenerator.Emit(OpCodes.Call, MethodBaseToDebugStringMethod);
				//ilGenerator.Emit(OpCodes.Call, StringFormat2Method);
				//ilGenerator.Emit(OpCodes.Call, typeof(Logging).GetMethod(nameof(Logging.StringLog)));

				// Emit the code that loads the fixed arguments from ClosureMethod.ClosuresField into a local variable.
				fixedArgumentsVar = ilGenerator.DeclareLocal(typeof(object[]));
				ilGenerator.Emit(OpCodes.Ldsfld, methodBuilder.GetClosuresField());
				ilGenerator.EmitLdcI4(closureKey);
				ilGenerator.Emit(OpCodes.Call, ListItemGetMethod);
				ilGenerator.Emit(OpCodes.Dup);
				var foundClosureLabel = ilGenerator.DefineLabel();
				ilGenerator.Emit(OpCodes.Brtrue_S, foundClosureLabel);
				ilGenerator.Emit(OpCodes.Ldstr, "Unexpectedly did not find closure object for closureKey={0} inside method=\"{1}\"");
				ilGenerator.EmitLdcI4(closureKey);
				ilGenerator.Emit(OpCodes.Box, typeof(int));
				ilGenerator.Emit(OpCodes.Call, GetCurrentMethodMethod);
				ilGenerator.Emit(OpCodes.Call, MethodBaseToDebugStringMethod);
				ilGenerator.Emit(OpCodes.Call, StringFormat2Method);
				ilGenerator.Emit(OpCodes.Newobj, InvalidOperationExceptionConstructor);
				ilGenerator.Emit(OpCodes.Throw);
				ilGenerator.MarkLabel(foundClosureLabel);
				ilGenerator.EmitStloc(fixedArgumentsVar);

				//ilGenerator.Emit(OpCodes.Ldstr, "DEBUG ClosureMethod: closureKey={0} inside method: closure=\"{1}\"");
				//ilGenerator.EmitLdcI4(closureKey);
				//ilGenerator.Emit(OpCodes.Box, typeof(int));
				//ilGenerator.EmitLdloc(fixedArgumentsVar);
				//ilGenerator.Emit(OpCodes.Call, typeof(DebugExtensions).GetMethod(nameof(DebugExtensions.ToDebugString), new[] { typeof(object) }));
				//ilGenerator.Emit(OpCodes.Call, StringFormat2Method);
				//ilGenerator.Emit(OpCodes.Call, typeof(Logging).GetMethod(nameof(Logging.StringLog)));
			}

			// Emit the non-fixed instance that will be passed as 0th argument via ClosureMethod if needed.
			if (prefixArgumentCount == 1)
				ilGenerator.Emit(OpCodes.Ldarg_0);

			// Emit fixed arguments, some that can be hard-coded in as constants, others needing the closure array.
			for (var index = 0; index < fixedArgumentCount; index++)
			{
				var fixedArgument = fixedArguments[index];
				if (!ilGenerator.TryEmitConstant(fixedArgument))
				{
					// Need the closure at this point to obtain the fixed non-constant arguments.
					var fixedArgumentType = fixedArgument.GetType();
					ilGenerator.EmitLdloc(fixedArgumentsVar);
					ilGenerator.EmitLdcI4(index);
					// fixedArguments is an array of objects, so each element of it needs to be accessed by reference,
					// and if argument is supposed to be a value type, unbox it.
					ilGenerator.Emit(OpCodes.Ldelem_Ref);
					if (fixedArgumentType.IsValueType)
						ilGenerator.Emit(OpCodes.Unbox_Any, fixedArgumentType);
				}
			}

			// Emit non-fixed arguments that will be passed via ClosureMethod.
			for (var index = (short)0; index < nonFixedArgumentCount; index++)
			{
				ilGenerator.EmitLdarg(index);
			}

			// Emit call to the original method, using all the arguments pushed to the CIL stack above.
			if (isStatic || method.IsFinal)
			{
				ilGenerator.Emit(OpCodes.Call, method);
			}
			else
			{
				// The constrained prefix instruction handles boxing for value types as needed.
				ilGenerator.Emit(OpCodes.Constrained);
				ilGenerator.Emit(OpCodes.Callvirt, method);
			}

			// Emit return, boxing the return value if needed.
			if (returnType != typeof(void) && returnType.IsValueType)
			{
				ilGenerator.Emit(OpCodes.Box, returnType);
			}
			ilGenerator.Emit(OpCodes.Ret);

			var closureMethod = methodBuilder.GetMethod();
			//Logging.Log($"DEBUG ClosureMethod: closureKey={closureKey} created closureMethod={closureMethod.ToDebugString()}");
			return new ClosureMethod(closureKey, closureMethod, methodAttributes, nonFixedParameterInfos, method, fixedArguments);
		}

		interface IDynamicMethodBuilderFactory
		{
			IDynamicMethodBuilder Create(string name, MethodAttributes methodAttributes, CallingConventions callingConvention,
				Type declaringType, Type returnType, Type[] parameterTypes);
		}

		interface IDynamicMethodBuilder
		{
			ILGenerator GetILGenerator();

			ParameterBuilder DefineParameter(int position, ParameterAttributes attributes, string parameterName);

			MethodInfo GetMethod();

			FieldInfo GetClosuresField();
		}

		class DynamicMethodBuilder : IDynamicMethodBuilder
		{
			internal class Factory : IDynamicMethodBuilderFactory
			{
				public IDynamicMethodBuilder Create(string name, MethodAttributes methodAttributes, CallingConventions callingConvention,
					Type declaringType, Type returnType, Type[] parameterTypes)
				{
					return new DynamicMethodBuilder(new DynamicMethod(
						name,
						methodAttributes,
						callingConvention,
						returnType,
						parameterTypes,
						declaringType,
						skipVisibility: true));
				}
			}

			readonly DynamicMethod dynamicMethod;

			DynamicMethodBuilder(DynamicMethod dynamicMethod)
			{
				this.dynamicMethod = dynamicMethod;
			}

			public ParameterBuilder DefineParameter(int position, ParameterAttributes attributes, string parameterName) =>
				dynamicMethod.DefineParameter(position, attributes, parameterName);

			public ILGenerator GetILGenerator() => dynamicMethod.GetILGenerator();

			public MethodInfo GetMethod() => dynamicMethod;

			static readonly FieldInfo ClosuresField = typeof(ClosureMethod).GetField(nameof(ClosureMethod.Closures), BindingFlags.Static | BindingFlags.NonPublic);

			public FieldInfo GetClosuresField() => ClosuresField;
		}

		// This is based off Harmony.DynamicTools.CreateSaveableMethod/SaveMethod.
		class DebugDynamicMethodBuilder : IDynamicMethodBuilder
		{
			internal class Factory : IDynamicMethodBuilderFactory
			{
				public IDynamicMethodBuilder Create(string name, MethodAttributes methodAttributes, CallingConventions callingConvention,
					Type declaringType, Type returnType, Type[] parameterTypes)
				{
					var assemblyName = new AssemblyName("DebugAssembly_" + name);
					var dirPath = Path.Combine(Directory.GetCurrentDirectory(), "DebugAssembly");
					Directory.CreateDirectory(dirPath);
					var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave, dirPath);
					var moduleBuilder = assemblyBuilder.DefineDynamicModule(name, name + ".dll");

					var typeNamePrefix = string.IsNullOrEmpty(declaringType.Namespace) ? "Debug_" : declaringType.Namespace + ".Debug_";
					var typeBuilder = moduleBuilder.DefineType(typeNamePrefix + declaringType.Name, TypeAttributes.Public);
					var methodBuilder = typeBuilder.DefineMethod(
						name,
						methodAttributes,
						callingConvention,
						returnType,
						parameterTypes);

					var closuresHolderTypeBuilder = moduleBuilder.DefineType(typeNamePrefix + "ClosuresHolder");
					closuresHolderTypeBuilder.DefineField(nameof(ClosureMethod.Closures), typeof(List<object[]>), FieldAttributes.Static);
					var closuresHolderType = closuresHolderTypeBuilder.CreateType();
					closuresHolderType.GetField(nameof(ClosureMethod.Closures), BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, ClosureMethod.Closures);

					return new DebugDynamicMethodBuilder(dirPath, parameterTypes, assemblyBuilder, typeBuilder, methodBuilder, closuresHolderType);
				}
			}

			readonly string dirPath;
			readonly Type[] parameterTypes;
			readonly AssemblyBuilder assemblyBuilder;
			readonly TypeBuilder typeBuilder;
			readonly MethodBuilder methodBuilder;
			readonly Type closuresHolderType;

			DebugDynamicMethodBuilder(string dirPath, Type[] parameterTypes, AssemblyBuilder assemblyBuilder, TypeBuilder typeBuilder, MethodBuilder methodBuilder,
				Type closuresHolderType)
			{
				this.dirPath = dirPath;
				this.parameterTypes = parameterTypes;
				this.assemblyBuilder = assemblyBuilder;
				this.typeBuilder = typeBuilder;
				this.methodBuilder = methodBuilder;
				this.closuresHolderType = closuresHolderType;
			}

			public ParameterBuilder DefineParameter(int position, ParameterAttributes attributes, string parameterName) =>
				methodBuilder.DefineParameter(position, attributes, parameterName);

			public ILGenerator GetILGenerator() => methodBuilder.GetILGenerator();

			public MethodInfo GetMethod()
			{
				typeBuilder.CreateType();
				var fileName = methodBuilder.Name + ".dll";
				assemblyBuilder.Save(fileName);
				Logging.Log("Saved dynamically created partial applied method to " + Path.Combine(dirPath, fileName));
				// A MethodBuilder can't be Invoke'd (nor can its MethodHandle be obtained), so get the concrete method from the just-built type.
				return typeBuilder.GetMethod(methodBuilder.Name, parameterTypes);
			}

			public FieldInfo GetClosuresField() => closuresHolderType.GetField(nameof(ClosureMethod.Closures), BindingFlags.Static | BindingFlags.NonPublic);
		}
	}
}
