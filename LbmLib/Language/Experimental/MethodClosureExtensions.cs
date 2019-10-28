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

		sealed class ClosureMethod : MethodInfo
		{
			readonly MethodInfo method;
			readonly RuntimeMethodHandle methodHandle;
			readonly MethodAttributes methodAttributes;
			readonly ParameterInfo[] nonFixedParameterInfos;
			readonly object fixedThisObject;

			// Closures is the actual registry of fixedNonConstantArguments arrays, with closureKey being an index into it.
			// It's an object[] rather than a custom struct so that other assemblies can more easily access it, as is required with DebugDynamicMethodBuilder,
			// since other assemblies can only access public structures, fields, etc. (or else would require expensive reflection within the dynamically-generated method).
			// DebugDynamicMethodBuilder's build type will have its own field that stores a reference to this closures.
			readonly int closureKey;
			internal static readonly List<object[]> Closures = new List<object[]>();
			static int MinimumFreeClosureKey = 0;

			internal ClosureMethod(int closureKey, MethodInfo method, RuntimeMethodHandle methodHandle,
				MethodAttributes methodAttributes, ParameterInfo[] nonFixedParameterInfos, object fixedThisObject, object[] fixedNonConstantArguments)
			{
				this.closureKey = closureKey;
				this.method = method;
				this.methodHandle = methodHandle;
				this.methodAttributes = methodAttributes;
				this.nonFixedParameterInfos = nonFixedParameterInfos;
				// Assertion: if fixedThisObject is non-null, methodAttributes must include MethodAttributes.Static.
				this.fixedThisObject = fixedThisObject;

				// Even if fixedNonConstantArguments is empty and thus technically isn't even accessed within the dynamic method, register it,
				// since we also store the this object (in DynamicBind) there, and we've already reserved the closure key for it anyway.
				Closures[closureKey] = fixedNonConstantArguments;
			}

			internal ClosureMethod Bind(int closureKey, object fixedThisObject)
			{
				return new ClosureMethod(closureKey, method, methodHandle, methodAttributes, nonFixedParameterInfos, fixedThisObject, Closures[closureKey]);
			}

			~ClosureMethod()
			{
				lock (Closures)
				{
					Closures[closureKey] = null;
					if (closureKey < MinimumFreeClosureKey)
					{
						MinimumFreeClosureKey = closureKey;
					}
				}
				Logging.Log($"DEBUG FreeClosureKey: closureKey={closureKey}, minimumFreeClosureKey={MinimumFreeClosureKey}");
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
				$"ClosureMethod{{closureKey={closureKey}, method={method.ToDebugString()}, methodHandle={methodHandle}, attributes={methodAttributes}, " +
				$"fixedThisObject={fixedThisObject.ToDebugString()}, fixedNonConstantArguments={Closures[closureKey].ToDebugString()}}}";

			public string ToDebugString() => ToString();

			public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
			{
				if (IsStatic)
				{
					// Assertion: obj is null (static method requires null obj, and constructors aren't supported here yet).
					var actualParameters = parameters;
					if (!(fixedThisObject is null))
					{
						// To support DynamicBind, we replace the first argument with FixedThisObject,
						// but in a new array to avoid mutating an input.
						var parameterCount = parameters.Length;
						actualParameters = new object[parameterCount];
						actualParameters[0] = fixedThisObject;
						Array.Copy(parameters, 1, actualParameters, 1, parameterCount - 1);
					}
					return method.Invoke(null, invokeAttr, binder, actualParameters, culture);
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

			public override RuntimeMethodHandle MethodHandle => methodHandle;

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
		}

		// TODO: CreateDelegate extension methods that would be needed pre .NET 4.5?

		public static MethodInfo DynamicBind(this MethodInfo method, object target)
		{
			if (target is null)
				throw new ArgumentNullException(nameof(target));
			if (method.IsStatic)
				throw new ArgumentException("Cannot call DynamicBind on a static method");
			if (method is ClosureMethod closureMethod)
				return closureMethod.Bind(ClosureMethod.ReserveNextFreeClosureKey(), target);
			return CreateClosureMethod(method, target, new object[0]);
		}

		static readonly MethodInfo GetCurrentMethodMethod = typeof(MethodBase).GetMethod(nameof(MethodBase.GetCurrentMethod));
		static readonly MethodInfo ListItemGetMethod = typeof(List<object[]>).GetProperty("Item").GetGetMethod();
		static readonly ConstructorInfo InvalidOperationExceptionConstructor = typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) });
		static readonly MethodInfo MethodBaseToDebugStringMethod = typeof(DebugExtensions).GetMethod(nameof(DebugExtensions.ToDebugString), new[] { typeof(MethodBase) });
		static readonly MethodInfo StringFormat2Method = typeof(string).GetMethod(nameof(string.Format), new[] { typeof(string), typeof(object), typeof(object) });

		public static MethodInfo DynamicPartialApply(this MethodInfo method, params object[] fixedArguments)
		{
			return CreateClosureMethod(method, null, fixedArguments);
		}

		static MethodInfo CreateClosureMethod(MethodInfo method, object fixedThisObject, object[] fixedArguments)
		{
			var parameters = method.GetParameters();
			var totalArgumentCount = parameters.Length;
			var fixedArgumentCount = fixedArguments.Length;
			var isStatic = method.IsStatic;
			var declaringType = method.DeclaringType;
			var returnType = method.ReturnType;

			// Since DynamicMethod can only be created as a static method, if instance method, prepend an additional non-fixed argument.
			// But don't do this for the ParameterInfo array that is passed to ClosureMethod at the end.
			var nonFixedParameterInfos = new ParameterInfo[totalArgumentCount - fixedArgumentCount];
			var prefixArgumentCount = isStatic ? 0 : 1;
			totalArgumentCount += prefixArgumentCount;
			var nonFixedArgumentCount = totalArgumentCount - fixedArgumentCount;
			var nonFixedParameterTypes = new Type[nonFixedArgumentCount];

			for (var index = 0; index < fixedArgumentCount; index++)
			{
				var parameterType = parameters[index].ParameterType;
				if (parameterType.IsByRef)
					throw new ArgumentException("Cannot partial apply with a fixed argument passed by reference (including in and out arguments): " + parameters[index]);
			}
			// See above - need to prepend an additional non-fixed argument for instance methods.
			if (!isStatic)
				nonFixedParameterTypes[0] = declaringType;
			for (var index = prefixArgumentCount; index < nonFixedArgumentCount; index++)
			{
				var parameter = parameters[fixedArgumentCount + index];
				nonFixedParameterInfos[index - prefixArgumentCount] = parameter;
				nonFixedParameterTypes[index] = parameter.ParameterType;
			}

			var closureKey = ClosureMethod.ReserveNextFreeClosureKey();
			var methodBuilder = (ClosureMethodGenerateDebugDll ?
				(IDynamicMethodBuilderFactory)new DebugDynamicMethodBuilder.Factory() : new DynamicMethodBuilder.Factory()).Create(
				method.Name + "_Closure_" + closureKey,
				// Some .NET implementations only accept the following MethodAttributes/CallingConventions for DynamicMethod.
				MethodAttributes.Public | MethodAttributes.Static,
				CallingConventions.Standard,
				declaringType,
				returnType,
				nonFixedParameterTypes);
			if (!isStatic)
				methodBuilder.DefineParameter(0, ParameterAttributes.None, "instance");
			for (int index = prefixArgumentCount; index < nonFixedArgumentCount; index++)
			{
				var parameter = parameters[fixedArgumentCount + index];
				// DefineParameter(0,...) refers to the return value, so for actual parameters, it's practically 1-based rather than 0-based.
				methodBuilder.DefineParameter(index + 1, parameter.Attributes, parameter.Name);
				// XXX: Do any custom attributes like ParamArrayAttribute need to be copied too?
				// There's no good generic way to copy attributes as far as I can tell, since CustomAttributeBuilder is very cumbersome to use.
			}
			var ilGenerator = methodBuilder.GetILGenerator();

			// Store fixed non-constant arguments for the ClosureMethod that's created at the end of this method,
			// while emitting instructions to fixed non-constant arguments from said ClosureMethod.
			var fixedNonConstantArgumentsVar = default(LocalBuilder);
			var fixedNonConstantArguments = fixedArguments.Where(fixedArgument => ilGenerator.CanEmitConstant(fixedArgument)).ToArray();
			if (fixedNonConstantArguments.Length > 0)
			{
				//ilGenerator.Emit(OpCodes.Ldstr, "DEBUG ClosureMethod: closureKey={0} inside method=\"{1}\"");
				//ilGenerator.EmitLdcI4(closureKey);
				//ilGenerator.Emit(OpCodes.Box, typeof(int));
				//ilGenerator.Emit(OpCodes.Call, GetCurrentMethodMethod);
				//ilGenerator.Emit(OpCodes.Call, MethodBaseToDebugStringMethod);
				//ilGenerator.Emit(OpCodes.Call, StringFormat2Method);
				//ilGenerator.Emit(OpCodes.Call, typeof(Logging).GetMethod(nameof(Logging.StringLog)));

				// Emit the code that loads the closure from ClosureMethod.ClosuresField, and then its FixedNonConstantArguments into a local variable.
				fixedNonConstantArgumentsVar = ilGenerator.DeclareLocal(typeof(object[]));
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
				ilGenerator.EmitStloc(fixedNonConstantArgumentsVar);

				//ilGenerator.Emit(OpCodes.Ldstr, "DEBUG ClosureMethod: closureKey={0} inside method: closure=\"{1}\"");
				//ilGenerator.EmitLdcI4(closureKey);
				//ilGenerator.Emit(OpCodes.Box, typeof(int));
				//ilGenerator.EmitLdloc(fixedNonConstantArgumentsVar);
				//ilGenerator.Emit(OpCodes.Call, typeof(DebugExtensions).GetMethod(nameof(DebugExtensions.ToDebugString), new[] { typeof(object) }));
				//ilGenerator.Emit(OpCodes.Call, StringFormat2Method);
				//ilGenerator.Emit(OpCodes.Call, typeof(Logging).GetMethod(nameof(Logging.StringLog)));
			}

			if (!isStatic)
				ilGenerator.Emit(OpCodes.Ldarg_0);

			var fixedNonConstantArgumentIndex = 0;
			for (var index = 0; index < fixedArgumentCount; index++)
			{
				var fixedArgument = fixedArguments[index];
				if (!ilGenerator.TryEmitConstant(fixedArgument))
				{
					// Need the closure at this point to obtain the fixed non-constant arguments.
					var fixedArgumentType = fixedArgument.GetType();
					ilGenerator.EmitLdloc(fixedNonConstantArgumentsVar);
					ilGenerator.EmitLdcI4(fixedNonConstantArgumentIndex);
					// fixedNonConstantArguments is an array of objects, so each element of it needs to be accessed by reference,
					// and if argument is supposed to be a value type, unbox it.
					ilGenerator.Emit(OpCodes.Ldelem_Ref);
					if (fixedArgumentType.IsValueType)
						ilGenerator.Emit(OpCodes.Unbox_Any, fixedArgumentType);
					fixedNonConstantArgumentIndex++;
				}
			}

			for (var index = (short)0; index < nonFixedArgumentCount; index++)
			{
				ilGenerator.EmitLdarg(index);
			}

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

			if (returnType != typeof(void) && returnType.IsValueType)
			{
				ilGenerator.Emit(OpCodes.Box, returnType);
			}
			ilGenerator.Emit(OpCodes.Ret);

			var closureMethod = methodBuilder.GetMethod();
			Logging.Log($"DEBUG ClosureMethod: closureKey={closureKey} created closureMethod={closureMethod.ToDebugString()}");

			return new ClosureMethod(closureKey, closureMethod, methodBuilder.GetMethodHandle(closureMethod), method.Attributes,
				nonFixedParameterInfos, fixedThisObject, fixedNonConstantArguments);
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

			RuntimeMethodHandle GetMethodHandle(MethodInfo method);

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

			// For MS .NET implementations
			static readonly MethodInfo GetMethodDescriptorMethod = typeof(DynamicMethod).GetMethod("GetMethodDescriptor", BindingFlags.Instance | BindingFlags.NonPublic);

			// For Mono implementations
			static readonly MethodInfo CreateDynMethodMethod = typeof(DynamicMethod).GetMethod("CreateDynMethod", BindingFlags.Instance | BindingFlags.NonPublic);
			static readonly FieldInfo InternalHandleField = typeof(DynamicMethod).GetField("mhandle", BindingFlags.Instance | BindingFlags.NonPublic);

			public RuntimeMethodHandle GetMethodHandle(MethodInfo method)
			{
				// Handle MS .NET implementation of DynamicMethod.
				if (!(GetMethodDescriptorMethod is null))
				{
					return (RuntimeMethodHandle)GetMethodDescriptorMethod.Invoke(dynamicMethod, Type.EmptyTypes);
				}
				// Mono .NET implementation of DynamicMethod.
				if (!(CreateDynMethodMethod is null))
				{
					CreateDynMethodMethod.Invoke(dynamicMethod, Type.EmptyTypes);
					return (RuntimeMethodHandle)InternalHandleField.GetValue(dynamicMethod);
				}
				throw new NotSupportedException("Could not create RuntimeMethodHandle");
			}

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
					var dirPath = Path.Combine(Directory.GetCurrentDirectory(), "DebugAssemby");
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
				// MethodBuilder doesn't have a RuntimeMethodHandle, so get the built method from typeBuilder.
				return typeBuilder.GetMethod(methodBuilder.Name, parameterTypes);
			}

			public RuntimeMethodHandle GetMethodHandle(MethodInfo method) => method.MethodHandle;

			public FieldInfo GetClosuresField() => closuresHolderType.GetField(nameof(ClosureMethod.Closures), BindingFlags.Static | BindingFlags.NonPublic);
		}
	}
}
