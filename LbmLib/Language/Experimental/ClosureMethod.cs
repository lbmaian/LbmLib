//#define GENERATE_DEBUG_DLL
//#define GENERATE_DEBUG_LOGGING
//#define GENERATE_DEBUG_AGGRESSIVE_GC

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
#if !NET35
using System.Runtime.CompilerServices; // for ConditionalWeakTable
#endif

namespace LbmLib.Language.Experimental
{
	public sealed class ClosureMethod : MethodInfo
	{
		public MethodInfo OriginalMethod { get; }

		readonly MethodAttributes methodAttributes;
		readonly ParameterInfo[] nonFixedParameterInfos;
		readonly MethodInfo genericMethodDefinition;

		public object FixedThisArgument { get; }

		public IRefList<object> FixedArguments { get; }

		// Lazily computed.
		string methodName;

		internal ClosureMethod(MethodInfo originalMethod, MethodAttributes methodAttributes, ParameterInfo[] nonFixedParameterInfos,
			MethodInfo genericMethodDefinition, object fixedThisArgument, IRefList<object> fixedArguments)
		{
			OriginalMethod = originalMethod;
			this.methodAttributes = methodAttributes;
			this.nonFixedParameterInfos = nonFixedParameterInfos;
			this.genericMethodDefinition = genericMethodDefinition;
			FixedThisArgument = fixedThisArgument;
			FixedArguments = fixedArguments;
		}

		internal static readonly ClosureRegistry Registry = new ClosureRegistry();

		internal class ClosureRegistry
		{
			// This is the actual registry of fixedArguments IRefList (wrapped/chained array), with registryKey being an index into it.
			// It's an IRefList<object> rather than a custom struct so that other assemblies can more easily access it,
			// as is required with DebugDynamicMethodBuilder, since other assemblies can only access public types and members
			// (or else would require expensive reflection within the dynamically-generated method).
			// DebugDynamicMethodBuilder's built type will have its own field that stores a reference to this object.
			internal readonly List<IRefList<object>> FixedArgumentsRegistry = new List<IRefList<object>>();

#if NET35
			readonly List<WeakReference> OwnerWeakReferenceRegistry = new List<WeakReference>();
#else
			readonly ConditionalWeakTable<object, DeregisterUponFinalize> OwnerWeakReferenceRegistry =
				new ConditionalWeakTable<object, DeregisterUponFinalize>();
#endif

			internal int MinimumFreeRegistryKey = 0;

			public int RegisterFixedArguments(IRefList<object> fixedArguments)
			{
				// XXX: This is thread-locking and O(n) even with a minimum key optimization,
				// but it should suffice for now, so long as delegate closures aren't created in the thousands or so.
				int registryKey;
				lock (this)
				{
					registryKey = RegisterFixedArgumentsInternal(fixedArguments);
				}
#if GENERATE_DEBUG_LOGGING
				Logging.Log($"DEBUG RegisterFixedArguments(fixedArguments={fixedArguments.ToDebugString()}) => registryKey={registryKey}");
#endif
				return registryKey;
			}

			int RegisterFixedArgumentsInternal(IRefList<object> closure)
			{
				var closureCount = FixedArgumentsRegistry.Count;
				while (MinimumFreeRegistryKey < closureCount)
				{
					var registryKey = MinimumFreeRegistryKey;
					MinimumFreeRegistryKey++;
					if (FixedArgumentsRegistry[registryKey] is null)
					{
						FixedArgumentsRegistry[registryKey] = closure;
						return registryKey;
					}
				}
				MinimumFreeRegistryKey++;
				FixedArgumentsRegistry.Add(closure);
#if NET35
				OwnerWeakReferenceRegistry.Add(null);
#endif
				return closureCount;
			}

			public void RegisterClosureOwner(int registryKey, object closureOwner)
			{
				lock (this)
				{
#if NET35
					OwnerWeakReferenceRegistry[registryKey] = new WeakReference(closureOwner);
#else
					OwnerWeakReferenceRegistry.Add(closureOwner, new DeregisterUponFinalize(registryKey));
#endif
				}
#if GENERATE_DEBUG_LOGGING
				Logging.Log($"DEBUG RegisterClosureOwner(registryKey={registryKey}, closureOwner={closureOwner.ToDebugString()})");
#endif
#if NET35
				GCMonitor.EnsureStarted();
#endif
			}

			public void Deregister(int registryKey)
			{
				lock (this)
				{
					FixedArgumentsRegistry[registryKey] = null;
					if (MinimumFreeRegistryKey > registryKey)
						MinimumFreeRegistryKey = registryKey;
#if NET35
					OwnerWeakReferenceRegistry[registryKey] = null;
#endif
				}
#if GENERATE_DEBUG_LOGGING
				Logging.Log($"DEBUG Deregister(registryKey={registryKey}) => minimumFreeRegistryKey={MinimumFreeRegistryKey}");
#endif
			}

#if NET35
			// Workaround for lack of ConditionalWeakTable in .NET 3.5.
			// This class monitors for garbage collection events (specifically generation 0) and on each GC,
			// scans the weak references that hold finalizing (garbage collecting) delegates, and deregistering related closure object
			// (and the weak reference itself) for such delegates.
			// It works by creating a new unreferenced GCMonitor object that when finalized (which will happen in generation 0 garbage collection),
			// does the aforementioned scanning and deregistering, then creates a new unreferenced GCMonitor object, and so forth.
			sealed class GCMonitor
			{
				static GCMonitor()
				{
					new GCMonitor();
				}

				internal static void EnsureStarted()
				{
					// Just referencing GCMonitor will call the static GCMonitor() method, so this method doesn't need to do anything itself.
				}

				~GCMonitor()
				{
					try
					{
#if GENERATE_DEBUG_LOGGING
						Logging.Log("DEBUG ~GCMonitor()");
#endif
						lock (Registry)
						{
							var fixedArgumentsRegistry = Registry.FixedArgumentsRegistry;
							var ownerWeakReferencesRegistry = Registry.OwnerWeakReferenceRegistry;
							var closureCount = fixedArgumentsRegistry.Count;
							for (var registryKey = 0; registryKey < closureCount; registryKey++)
							{
								if (!(fixedArgumentsRegistry[registryKey] is null) && !ownerWeakReferencesRegistry[registryKey].IsAlive)
								{
									Registry.Deregister(registryKey);
								}
							}
						}
					}
					finally
					{
						if (!AppDomain.CurrentDomain.IsFinalizingForUnload() && !Environment.HasShutdownStarted)
							new GCMonitor();
					}
				}
			}
#else
			sealed class DeregisterUponFinalize
			{
				readonly int registryKey;

				internal DeregisterUponFinalize(int registryKey)
				{
					this.registryKey = registryKey;
				}

				~DeregisterUponFinalize()
				{
					Registry.Deregister(registryKey);
				}

				public override string ToString() => $"DeregisterUponFinalize{{{registryKey}}}";
			}
#endif

			public override string ToString()
			{
				lock (this)
				{
					return $"ClosureRegistry.FixedArgumentsRegistry:\n\t{FixedArgumentsRegistry.ToDebugString("\n\t", false)}\n" +
						$"ClosureRegistry.OwnerWeakReferenceRegistry\n\t{OwnerWeakReferenceRegistry.ToDebugString("\n\t", false)}";
				}
			}
		}

		public override string ToString() =>
			ReturnType.Name + " " + Name +
			(IsGenericMethod ? "[" + GetGenericArguments().Join(",") + "]" : "") +
			"(" + GetParameters().Join(", ") + ")";

		public string ToDebugString(bool includeNamespace = true, bool includeDeclaringType = true)
		{
			var originalParameters = OriginalMethod.GetParameters();
			return (IsStatic ? "static " : FixedThisArgument is null ? "" : "#" + FixedThisArgument.ToDebugString() + "#.") +
				ReturnType.ToDebugString(includeNamespace, includeDeclaringType) + " " +
				(!includeDeclaringType || DeclaringType is null ? "" : DeclaringType.ToDebugString(includeNamespace, includeDeclaringType) + ":") +
				OriginalMethod.Name + "(" + Enumerable.Concat(
					FixedArguments.Select((argument, index) =>
						originalParameters[index].ToDebugString(includeNamespace, includeDeclaringType) + ": #" + argument.ToDebugString() + "#"),
					GetParameters().Select(parameter => parameter.ToDebugString(includeNamespace, includeDeclaringType))).Join() + ")";
		}

		public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
		{
			// TODO: Could lazily optimize this with a delegate that accepts as parameters: fixedArguments, parameters.
			var fixedArgumentCount = FixedArguments.Count;
			var parameterCount = parameters.Length;
			var combinedArguments = new object[fixedArgumentCount + parameterCount];
			FixedArguments.CopyTo(combinedArguments, 0);
			parameters.CopyTo(combinedArguments, fixedArgumentCount);
			var returnValue = OriginalMethod.Invoke(FixedThisArgument ?? obj, invokeAttr, binder, combinedArguments, culture);
			// In case any of the parameters are by-ref, copy back from combinedArguments to parameters.
			// TODO: Don't need to do this if there are no by-ref parameters.
			for (var index = 0; index < fixedArgumentCount; index++)
			{
				FixedArguments[index] = combinedArguments[index];
			}
			Array.Copy(combinedArguments, fixedArgumentCount, parameters, 0, parameterCount);
			return returnValue;
		}

		// Note: There's no way to ensure that MethodBase.GetMethodFromHandle(closureMethod.MethodHandle) == closureMethod,
		// so just don't support MethodHandle.
		public override RuntimeMethodHandle MethodHandle =>
			throw new InvalidOperationException("The requested operation is invalid for " + nameof(ClosureMethod));

		public override MethodAttributes Attributes => methodAttributes;

		public override Type DeclaringType => OriginalMethod.DeclaringType;

		public override string Name
		{
			get
			{
				// This is idempotent, so no need for locking.
				if (methodName is null)
				{
					methodName = OriginalMethod.Name + "_" +
						(FixedThisArgument is null ? "unbound" : ReplaceInvalidNameCharactersWithUnderscores(FixedThisArgument));
					if (FixedArguments.Count > 0)
						methodName += "_" + FixedArguments.Select(ReplaceInvalidNameCharactersWithUnderscores).Join("_");
				}
				return methodName;
			}
		}

		static string ReplaceInvalidNameCharactersWithUnderscores(object obj) =>
			obj is null ? "null" : obj.ToString().Where(c => char.IsLetterOrDigit(c)).Join(null);

		public override Type ReflectedType => OriginalMethod.ReflectedType;

		public override Module Module => OriginalMethod.Module;

		public override CallingConventions CallingConvention => IsStatic ? CallingConventions.Standard : CallingConventions.HasThis;

		public override Type ReturnType => OriginalMethod.ReturnType;

		public override ParameterInfo ReturnParameter => OriginalMethod.ReturnParameter;

		public override ICustomAttributeProvider ReturnTypeCustomAttributes => OriginalMethod.ReturnTypeCustomAttributes;

		public override bool IsGenericMethodDefinition => OriginalMethod.IsGenericMethodDefinition;

		public override bool ContainsGenericParameters => OriginalMethod.ContainsGenericParameters;

		public override bool IsGenericMethod => OriginalMethod.IsGenericMethod;

		public override MethodInfo GetBaseDefinition() => this;

		public override object[] GetCustomAttributes(bool inherit) => OriginalMethod.GetCustomAttributes(inherit);

		public override object[] GetCustomAttributes(Type attributeType, bool inherit) => OriginalMethod.GetCustomAttributes(attributeType, inherit);

		public override MethodImplAttributes GetMethodImplementationFlags() => OriginalMethod.GetMethodImplementationFlags();

		public override ParameterInfo[] GetParameters() => nonFixedParameterInfos;

		public override bool IsDefined(Type attributeType, bool inherit) => OriginalMethod.IsDefined(attributeType, inherit);

		public override Type[] GetGenericArguments() => OriginalMethod.GetGenericArguments();

		public override MethodInfo GetGenericMethodDefinition()
		{
			if (!IsGenericMethod)
				throw new InvalidOperationException();
			return genericMethodDefinition;
		}

		public override MethodInfo MakeGenericMethod(params Type[] typeArguments)
		{
			if (!IsGenericMethodDefinition)
				throw new InvalidOperationException(ToString() + " is not a GenericMethodDefinition. " +
					"MakeGenericMethod may only be called on a method for which MethodBase.IsGenericMethodDefinition is true.");
			var genericMethod = OriginalMethod.MakeGenericMethod(typeArguments);
			return new ClosureMethod(genericMethod, methodAttributes, genericMethod.GetParameters().CopyToEnd(FixedArguments.Count),
				this, FixedThisArgument, FixedArguments);
		}

#if !NET35
		override
#endif
		public Delegate CreateDelegate(Type delegateType)
		{
			if (!IsStatic || ContainsGenericParameters || !typeof(Delegate).IsAssignableFrom(delegateType))
				throw new ArgumentException("Cannot bind to the target method because its signature or security transparency is not " +
					"compatible with that of the delegate type.");
			return CreateClosureDelegate(delegateType, OriginalMethod, FixedThisArgument, FixedArguments);
		}

#if !NET35
		override
#endif
		public Delegate CreateDelegate(Type delegateType, object target)
		{
			// To match Delegate.CreateDelegate behavior, allow null target.
			// For static methods, this is valid.
			// For bound methods (also a static method), FixedThisArgument will be passed for target.
			// For instance methods, don't throw ArgumentNullException; let delegate invocation throw a TargetInvocationException.
			if (ContainsGenericParameters || !typeof(Delegate).IsAssignableFrom(delegateType) ||
				!(target is null || DeclaringType.IsAssignableFrom(target.GetType())))
				throw new ArgumentException("Cannot bind to the target method because its signature or security transparency is not " +
					"compatible with that of the delegate type.");
			return CreateClosureDelegate(delegateType, OriginalMethod, target ?? FixedThisArgument, FixedArguments);
		}

		// For easy access within the delegate dynamic method. Public for access within DebugDynamicMethodBuilder-created assemblies.
		public static readonly List<IRefList<object>> FixedArgumentsRegistry = Registry.FixedArgumentsRegistry;

		static readonly FieldInfo FixedArgumentsRegistryField = typeof(ClosureMethod).GetField(nameof(FixedArgumentsRegistry));
		static readonly MethodInfo GetCurrentMethodMethod = typeof(MethodBase).GetMethod(nameof(MethodBase.GetCurrentMethod));
		static readonly MethodInfo ListOfIRefListOfObjectItemGetMethod = typeof(List<IRefList<object>>).GetProperty("Item").GetGetMethod();
		static readonly MethodInfo IRefListOfObjectItemGetMethod = typeof(IRefList<object>).GetProperty("Item").GetGetMethod();
		static readonly MethodInfo IRefListOfObjectGetItemRefMethod = typeof(IRefList<object>).GetMethod(nameof(IRefList<object>.ItemRef));
		static readonly ConstructorInfo InvalidOperationExceptionConstructor = typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) });
		static readonly MethodInfo MethodBaseToDebugStringMethod = typeof(DebugExtensions).GetMethod(nameof(DebugExtensions.ToDebugString), new[] { typeof(MethodBase) });
		static readonly MethodInfo StringFormat2Method = typeof(string).GetMethod(nameof(string.Format), new[] { typeof(string), typeof(object), typeof(object) });

		static Delegate CreateClosureDelegate(Type delegateType, MethodInfo method, object fixedThisArgument, IRefList<object> fixedArguments)
		{
			// Since DynamicMethod can only be created as a static method, if method is an instance method (and thus fixedThisArgument is asserted to be non-null)
			// we will need to prepend the fixed arguments with fixedThisArgument.
			if (!method.IsStatic)
			{
				fixedArguments = fixedArguments.ChainPrepend(fixedThisArgument);
			}

			var registryKey = Registry.RegisterFixedArguments(fixedArguments);
			try
			{
#if GENERATE_DEBUG_DLL
				// XXX: While we could get a built method from DebugDynamicMethodBuilder (via TypeBuilder.GetMethod(methodBuilder.Name, parameterTypes)),
				// it seems that the using such a built method as a closure owner doesn't work - the built method apparently can become finalizable
				// (and thus could free the closure fixedArguments) before the delegate created from it is even called.
				// Workaround is to just use the non-debug DynamicMethodBuilder for both the closure owner and to create the delegate from,
				// while the DebugDynamicMethodBuilder's only purpose is to save a debug assembly dll.
				// I also tried just using a dummy DynamicMethod that simply passes through arguments to a TypeBuilder.GetMethod()-built method,
				// but that still suffered the same premature finalization issue.
				CreateClosureDynamicMethod(delegateType, method, fixedThisArgument, fixedArguments, registryKey, new DebugDynamicMethodBuilder.Factory());
#endif

				var dynamicMethod = CreateClosureDynamicMethod(delegateType, method, fixedThisArgument, fixedArguments, registryKey, new DynamicMethodBuilder.Factory());
				var closureDelegate = dynamicMethod.CreateDelegate(delegateType);
				// The closure "owner" is not the delegate itself, since if the last usage of the delegate is to call it (and its contained dynamic method),
				// the delegate object can become finalizable right as its being invoked and hands control over to the dynamic method's contained code.
				// The dynamic method itself doesn't seem to be finalizable during execution of its contained code
				// (or at least to the point that the fixedArguments are loaded in it).
				Registry.RegisterClosureOwner(registryKey, dynamicMethod);
				return closureDelegate;
			}
			catch (Exception ex)
			{
				// If anything went wrong, deregister the closure, then rethrow the exception.
				Registry.Deregister(registryKey);
				throw ex;
			}
		}

		static DynamicMethod CreateClosureDynamicMethod(Type delegateType, MethodInfo method, object fixedThisArgument, IRefList<object> fixedArguments,
			int registryKey, IDynamicMethodBuilderFactory dynamicMethodBuilderFactory)
		{
			var isStatic = method.IsStatic;
			var declaringType = method.DeclaringType;
			var returnType = method.ReturnType;
			var parameters = method.GetParameters();
			// Note: If method isn't static, fixedThisArgument is already prepended to fixedArguments.
			var fixedArgumentCount = fixedArguments.Count;
			var prefixArgumentCount = isStatic ? 0 : 1;
			var nonFixedArgumentCount = parameters.Length - fixedArgumentCount + prefixArgumentCount;

			var nonFixedParameterTypes = new Type[nonFixedArgumentCount];
			for (var index = 0; index < nonFixedArgumentCount; index++)
				nonFixedParameterTypes[index] = parameters[fixedArgumentCount - prefixArgumentCount + index].ParameterType;

			var methodBuilder = dynamicMethodBuilderFactory.Create(
				method.Name + "_Closure_" + registryKey,
				// Some (all?) .NET implementations only accept the following MethodAttributes/CallingConventions for DynamicMethod.
				MethodAttributes.Public | MethodAttributes.Static,
				CallingConventions.Standard,
				declaringType,
				returnType,
				nonFixedParameterTypes);
			for (int index = 0; index < nonFixedArgumentCount; index++)
			{
				var parameter = parameters[fixedArgumentCount - prefixArgumentCount + index];
				// DefineParameter(0,...) refers to the return value, so for actual parameters, it's practically 1-based rather than 0-based.
				methodBuilder.DefineParameter(index + 1, parameter.Attributes, parameter.Name);
				// XXX: Do any custom attributes like ParamArrayAttribute need to be copied too?
				// There's no good generic way to copy attributes as far as I can tell, since CustomAttributeBuilder is very cumbersome to use.
			}
			var ilGenerator = methodBuilder.GetILGenerator();

			// Create the non-constant fixed arguments that are stored in the closure, and loaded within the dynamic method if needed.
			var fixedArgumentsVar = default(LocalBuilder);
			if (fixedArguments.Any(fixedArgument => !ilGenerator.CanEmitConstant(fixedArgument)))
			{
#if GENERATE_DEBUG_LOGGING
				ilGenerator.Emit(OpCodes.Ldstr, "DEBUG CreateClosureDelegate: registryKey={0} inside method=\"{1}\"");
				ilGenerator.EmitLdcI4(registryKey);
				ilGenerator.Emit(OpCodes.Box, typeof(int));
				ilGenerator.Emit(OpCodes.Call, GetCurrentMethodMethod);
				ilGenerator.Emit(OpCodes.Call, MethodBaseToDebugStringMethod);
				ilGenerator.Emit(OpCodes.Call, StringFormat2Method);
				ilGenerator.Emit(OpCodes.Call, typeof(Logging).GetMethod(nameof(Logging.StringLog)));
#endif
#if GENERATE_DEBUG_AGGRESSIVE_GC
				// Doing a couple full GC iterations, since finalizers themselves create objects that need finalization,
				// which in turn can be GC'ed and need finalizing themselves, and so forth.
				// This isn't fool-proof and is probably overkill, but it should suffice for testing purposes.
				for (var gcIter = 0; gcIter < 3; gcIter++)
				{
					ilGenerator.Emit(OpCodes.Ldc_I4_2);
					ilGenerator.Emit(OpCodes.Call, typeof(GC).GetMethod(nameof(GC.Collect), new[] { typeof(int) }));
					ilGenerator.Emit(OpCodes.Call, typeof(GC).GetMethod(nameof(GC.WaitForPendingFinalizers), Type.EmptyTypes));
				}
#if GENERATE_DEBUG_LOGGING
				ilGenerator.Emit(OpCodes.Ldstr, "DEBUG CreateClosureDelegate: registryKey={0} inside method: FixedArgumentsRegistry={1}");
				ilGenerator.EmitLdcI4(registryKey);
				ilGenerator.Emit(OpCodes.Box, typeof(int));
				ilGenerator.Emit(OpCodes.Ldsfld, FixedArgumentsRegistryField);
				ilGenerator.Emit(OpCodes.Call, typeof(DebugExtensions).GetMethod(nameof(DebugExtensions.ToDebugString), new[] { typeof(object) }));
				ilGenerator.Emit(OpCodes.Call, StringFormat2Method);
				ilGenerator.Emit(OpCodes.Call, typeof(Logging).GetMethod(nameof(Logging.StringLog)));
#endif
#endif

				// Emit the code that loads the fixed arguments from ClosureMethod.ClosuresField into a local variable.
				fixedArgumentsVar = ilGenerator.DeclareLocal(typeof(IRefList<object>));
				ilGenerator.Emit(OpCodes.Ldsfld, FixedArgumentsRegistryField);
				ilGenerator.EmitLdcI4(registryKey);
				ilGenerator.Emit(OpCodes.Call, ListOfIRefListOfObjectItemGetMethod);
				ilGenerator.Emit(OpCodes.Dup);
				var foundClosureLabel = ilGenerator.DefineLabel();
				ilGenerator.Emit(OpCodes.Brtrue_S, foundClosureLabel);
				ilGenerator.Emit(OpCodes.Ldstr, "Unexpectedly did not find closure object for registryKey={0} inside method=\"{1}\"");
				ilGenerator.EmitLdcI4(registryKey);
				ilGenerator.Emit(OpCodes.Box, typeof(int));
				ilGenerator.Emit(OpCodes.Call, GetCurrentMethodMethod);
				ilGenerator.Emit(OpCodes.Call, MethodBaseToDebugStringMethod);
				ilGenerator.Emit(OpCodes.Call, StringFormat2Method);
				ilGenerator.Emit(OpCodes.Newobj, InvalidOperationExceptionConstructor);
				ilGenerator.Emit(OpCodes.Throw);
				ilGenerator.MarkLabel(foundClosureLabel);
				ilGenerator.EmitStloc(fixedArgumentsVar);

#if GENERATE_DEBUG_LOGGING
				ilGenerator.Emit(OpCodes.Ldstr, "DEBUG CreateClosureDelegate: registryKey={0} inside method: fixedArguments=\"{1}\"");
				ilGenerator.EmitLdcI4(registryKey);
				ilGenerator.Emit(OpCodes.Box, typeof(int));
				ilGenerator.EmitLdloc(fixedArgumentsVar);
				ilGenerator.Emit(OpCodes.Call, typeof(DebugExtensions).GetMethod(nameof(DebugExtensions.ToDebugString), new[] { typeof(object) }));
				ilGenerator.Emit(OpCodes.Call, StringFormat2Method);
				ilGenerator.Emit(OpCodes.Call, typeof(Logging).GetMethod(nameof(Logging.StringLog)));
#endif
			}

			// We'll be using a call opcode if the method is either (a) static, OR (b) instance AND the method is non-overrideable.
			// This latter (b) case includes the situation where the method's declaring type is a value type (since the method would be implicitly sealed),
			// and fixedThisArgument (which must be of a value type) must be unboxed into an address for the call opcode.
			// Otherwise, we'll be using a callvirt opcode. If fixedThisArgument is a value type, then the method's declaring type is either
			// Object/ValueType/Enum or an interface, and fixedThisArgument needs to be boxed.
			var useCallOpcode = isStatic || !method.IsVirtual || method.IsFinal;

			// For instance methods, emit the first item in the closure array, which represents the fixedThisArgument.
			if (!isStatic)
			{
				if (fixedThisArgument is null)
				{
					// If fixedThisArgument is null, fixedArgumentsVar can be null (implying closure isn't needed), so just ldnull.
					ilGenerator.Emit(OpCodes.Ldnull);
				}
				else
				{
					ilGenerator.EmitLdloc(fixedArgumentsVar);
					ilGenerator.EmitLdcI4(0);
					ilGenerator.Emit(OpCodes.Callvirt, IRefListOfObjectItemGetMethod);
					if (useCallOpcode)
					{
						if (declaringType.IsValueType)
							ilGenerator.Emit(OpCodes.Unbox, declaringType);
					}
					else
					{
						// If declaringType is a value type, fixedThisArgument needs to be boxed.
						// Fortunately, since it's in the closures array, it's already boxed, so we don't need to do anything extra.
					}
				}
			}

			// Emit fixed arguments, some that can be hard-coded in as constants, others needing the closure array.
			for (var index = prefixArgumentCount; index < fixedArgumentCount; index++)
			{
				var fixedArgument = fixedArguments[index];
				var fixedArgumentType = parameters[index - prefixArgumentCount].ParameterType;
				var fixedArgumentIsByRef = fixedArgumentType.IsByRef;
				if (fixedArgumentIsByRef || !ilGenerator.TryEmitConstant(fixedArgument))
				{
					// Need the closure at this point to obtain the fixed arguments.
					ilGenerator.EmitLdloc(fixedArgumentsVar);
					ilGenerator.EmitLdcI4(index);
					if (fixedArgumentIsByRef)
					{
						fixedArgumentType = fixedArgumentType.GetElementType();
						if (fixedArgumentType.IsValueType)
						{
							// This is the IRefList equivalent of the ldelem.ref instruction.
							ilGenerator.Emit(OpCodes.Callvirt, IRefListOfObjectItemGetMethod);
							// We have a boxed value, and we need a managed pointer to the value contained within the boxed value object.
							ilGenerator.Emit(OpCodes.Unbox, fixedArgumentType);
						}
						else
						{
							// This is the IRefList equivalent of the ldelema instruction.
							ilGenerator.Emit(OpCodes.Callvirt, IRefListOfObjectGetItemRefMethod);
						}
					}
					else
					{
						// This is the IRefList equivalent of the ldelem.ref instruction.
						ilGenerator.Emit(OpCodes.Callvirt, IRefListOfObjectItemGetMethod);
						// If value type, we have a boxed value, so unbox it.
						if (fixedArgumentType.IsValueType)
							ilGenerator.Emit(OpCodes.Unbox_Any, fixedArgumentType);
					}
				}
			}

			// Emit non-fixed arguments that will be passed via delegate invocation.
			for (var index = (short)0; index < nonFixedArgumentCount; index++)
			{
				ilGenerator.EmitLdarg(index);
			}

			// Emit call to the original method, using all the arguments pushed to the CIL stack above.
			ilGenerator.Emit(useCallOpcode ? OpCodes.Call : OpCodes.Callvirt, method);

			// Emit return. Note that if returning a value type, boxing the return value isn't needed
			// since the method return type would be a value type in this case (rather than object).
			ilGenerator.Emit(OpCodes.Ret);

			return methodBuilder.GetDynamicMethod();
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

			DynamicMethod GetDynamicMethod();
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

			public DynamicMethod GetDynamicMethod() => dynamicMethod;
		}

		// This is based off Harmony's DynamicTools.CreateSaveableMethod/SaveMethod.
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

					return new DebugDynamicMethodBuilder(dirPath, assemblyBuilder, typeBuilder, methodBuilder);
				}
			}

			readonly string dirPath;
			readonly AssemblyBuilder assemblyBuilder;
			readonly TypeBuilder typeBuilder;
			readonly MethodBuilder methodBuilder;

			DebugDynamicMethodBuilder(string dirPath, AssemblyBuilder assemblyBuilder, TypeBuilder typeBuilder, MethodBuilder methodBuilder)
			{
				this.dirPath = dirPath;
				this.assemblyBuilder = assemblyBuilder;
				this.typeBuilder = typeBuilder;
				this.methodBuilder = methodBuilder;
			}

			public ParameterBuilder DefineParameter(int position, ParameterAttributes attributes, string parameterName) =>
				methodBuilder.DefineParameter(position, attributes, parameterName);

			public ILGenerator GetILGenerator() => methodBuilder.GetILGenerator();

			public DynamicMethod GetDynamicMethod()
			{
				typeBuilder.CreateType();
				var fileName = methodBuilder.Name + ".dll";
				assemblyBuilder.Save(fileName);
				Logging.Log("DEBUG Saved dynamically created method for ClosureMethod delegate to " + Path.Combine(dirPath, fileName));
				// Return value won't be used, so just return a dummy null.
				return null;
			}
		}
	}
}
