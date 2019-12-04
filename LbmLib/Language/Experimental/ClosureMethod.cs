//#define GENERATE_DEBUG_DLL
//#define GENERATE_DEBUG_LOGGING
//#define GENERATE_DEBUG_REGISTRY_LOGGING
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
	public enum ClosureMethodType
	{
		// ClosureMethod is static method, ClosureMethod.OriginalMethod is static method.
		// staticMethod.PartialApply(...) still results in a Static-type method.
		Static,

		// ClosureMethod is instance method, ClosureMethod.OriginalMethod is instance method,
		// instance is passed as obj argument in Invoke call (or target argument in CreateDelegate call).
		// instanceMethod.PartialApply(...) still results in an Instance-type method.
		Instance,

		// ClosureMethod is static method, ClosureMethod.OriginalMethod is instance method,
		// instance is passed as additional first element in parameters argument in Invoke call
		// (or additional first parameter in delegate call).
		// instanceMethod.AsStatic() results in an InstanceAsStatic-type method.
		InstanceAsStatic,

		// ClosureMethod is static method, ClosureMethod.OriginalMethod is instance method,
		// instance is ClosureMethod.FixedThisArgument.
		// instanceMethod.Bind(target) results in a BoundInstance-type method, where target becomes FixedThisArgument.
		// instanceAsStaticMethod.PartialApply(target, otherArguments...) also results in a BoundInstance-type method,
		// where instanceAsStaticMethod is an InstanceAsStatic-type method, and target becomes FixedThisArgument
		// (and otherArguments... is appended to FixedArguments).
		BoundInstance,
	}

	public sealed class ClosureMethod : MethodInfo
	{
		public MethodInfo OriginalMethod { get; }

		readonly MethodAttributes methodAttributes;
		readonly MethodInfo genericMethodDefinition;
		readonly ParameterInfo[] originalParameterInfos;

		public ClosureMethodType MethodType { get; }

		public object FixedThisArgument { get; }

		public IRefList<object> FixedArguments { get; }

		// Lazily computed.
		ParameterInfo[] nonFixedParameterInfos;
		string methodName;

		internal ClosureMethod(ClosureMethodType methodType, MethodInfo originalMethod, MethodInfo genericMethodDefinition,
			MethodAttributes methodAttributes, ParameterInfo[] originalParameterInfos,
			object fixedThisArgument, IRefList<object> fixedArguments)
		{
			MethodType = methodType;
			OriginalMethod = originalMethod;
			this.genericMethodDefinition = originalMethod == genericMethodDefinition ? this : genericMethodDefinition;
			this.methodAttributes = methodAttributes;
			this.originalParameterInfos = originalParameterInfos;
			FixedThisArgument = fixedThisArgument;
			FixedArguments = fixedArguments;
		}

		internal ClosureMethod(ClosureMethod closureMethod, ClosureMethodType methodType, MethodInfo genericMethodDefinition,
			MethodAttributes methodAttributes, object fixedThisArgument, IRefList<object> fixedArguments)
		{
			MethodType = methodType;
			OriginalMethod = closureMethod.OriginalMethod;
			this.genericMethodDefinition = closureMethod == genericMethodDefinition ? this : genericMethodDefinition;
			this.methodAttributes = methodAttributes;
			originalParameterInfos = closureMethod.originalParameterInfos;
			FixedThisArgument = fixedThisArgument;
			FixedArguments = fixedArguments;
		}

		// Array.Empty<object>() is only available in .NET Framework 4.6+ and .NET Core.
		internal static readonly object[] EmptyObjectArray = new object[0];

		internal static readonly ClosureRegistry Registry = new ClosureRegistry();

		internal class ClosureRegistry
		{
			// This is the actual registry of fixedArguments IRefList (wrapped/chained array), with registryKey being an index into it.
			internal readonly List<IRefList<object>> FixedArgumentsRegistry = new List<IRefList<object>>();

#if NET35
			readonly List<WeakReference> OwnerWeakReferenceRegistry = new List<WeakReference>();
#else
			readonly ConditionalWeakTable<object, DeregisterUponFinalize> OwnerWeakReferenceRegistry =
				new ConditionalWeakTable<object, DeregisterUponFinalize>();
#endif

			internal int MinimumFreeRegistryKey = 0;

			// Placeholder reflist for reserving a registry key.
			readonly IRefList<object> placeholder = new object[] { "placeholder" }.AsRefList();

			public int Register()
			{
				// XXX: This is thread-locking and O(n) even with a minimum key optimization,
				// but it should suffice for now, so long as delegate closures aren't created in the thousands or so.
				int registryKey;
				lock (this)
				{
					var closureCount = FixedArgumentsRegistry.Count;
					while (MinimumFreeRegistryKey < closureCount)
					{
						registryKey = MinimumFreeRegistryKey;
						MinimumFreeRegistryKey++;
						if (FixedArgumentsRegistry[registryKey] is null)
						{
							FixedArgumentsRegistry[registryKey] = placeholder;
							goto End;
						}
					}
					MinimumFreeRegistryKey++;
					FixedArgumentsRegistry.Add(placeholder);
#if NET35
					OwnerWeakReferenceRegistry.Add(null);
#endif
					registryKey = closureCount;
				}
			End:
#if GENERATE_DEBUG_REGISTRY_LOGGING
				Logging.Log($"DEBUG ClosureRegistry.Register() => registryKey={registryKey}");
#endif
				return registryKey;
			}

			public void RegisterFixedArguments(int registryKey, IRefList<object> fixedArguments)
			{
				lock (this)
				{
					// Optimization: Don't register empty closures.
					// XXX: This also helps prevent odd edge case, at least on the Mono runtime on .NET Framework 4.7.2 in unit tests
					// (see MethodClosureExtensionsFixture.AssertClosureRegistryCountAfterFullGCFinalization), where it seems that very small dynamic
					// methods (e.g. one that only needs to call a static void parameter-less method with empty closure) can somehow not be listed in
					// ConditionalWeakTable.Keys, and thus the corresponding DeregisterUponFinalize is not Dispose'd in the DeregisterAll call,
					// and thus the empty closure is not deregistered (at least by the time of the assertion checks).
					// This is potentially due to the dynamic method no longer becoming reachable and thus the corresponding ephemeron in the
					// ConditionalWeakTable is removed, although the DeregisterUponFinalize finalizer apparently not firing despite the multiple forced
					// GCs AssertClosureRegistryCountAfterFullGCFinalization calls that hypothesis into question.
					// I don't really know what's causing that issue, but simply not storing empty closures helps avoid that headache.
					if (fixedArguments.Count == 0)
					{
						// Need to revert the changes done in Register().
						FixedArgumentsRegistry[registryKey] = null;
						if (MinimumFreeRegistryKey > registryKey)
							MinimumFreeRegistryKey = registryKey;
						return;
					}
					FixedArgumentsRegistry[registryKey] = fixedArguments;
				}
#if GENERATE_DEBUG_REGISTRY_LOGGING
				Logging.Log($"DEBUG ClosureRegistry.RegisterFixedArguments(registryKey={registryKey}, fixedArguments={fixedArguments.ToDebugString()})");
#endif
			}

			public void RegisterClosureOwner(int registryKey, object closureOwner)
			{
				lock (this)
				{
					// Optimization: Empty closures aren't registered, so don't register closure owner in this case.
					if (FixedArgumentsRegistry[registryKey] is null)
						return;
#if NET35
					OwnerWeakReferenceRegistry[registryKey] = new WeakReference(closureOwner);
#else
					OwnerWeakReferenceRegistry.Add(closureOwner, new DeregisterUponFinalize(registryKey));
#endif
				}
#if GENERATE_DEBUG_REGISTRY_LOGGING
				Logging.Log($"DEBUG ClosureRegistry.RegisterClosureOwner(registryKey={registryKey}, closureOwner={closureOwner.ToDebugString()})");
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
#if GENERATE_DEBUG_REGISTRY_LOGGING
				Logging.Log($"DEBUG ClosureRegistry.Deregister(registryKey={registryKey}) => minimumFreeRegistryKey={MinimumFreeRegistryKey}");
#endif
			}

#if !NET35
			// .NET Core 2.0+ has ConditionalWeakTable.Clear become public. Before that, it's internal.
			static readonly MethodInfo weakTableClearMethod = typeof(ConditionalWeakTable<object, DeregisterUponFinalize>)
				.GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			// .NET Core 2.0+ has ConditionalWeakTable.GetEnumerator() but there's no direct equivalent before that.
			// So just use its internal Values property.
			static readonly MethodInfo weakTableValuesGetMethod = typeof(ConditionalWeakTable<object, DeregisterUponFinalize>)
				.GetProperty("Values", BindingFlags.Instance | BindingFlags.NonPublic).GetGetMethod(true);
#endif

			internal void DeregisterAll(bool finalizedOnly)
			{
#if GENERATE_DEBUG_REGISTRY_LOGGING
				Logging.Log($"DEBUG ClosureRegistry.DeregisterAll(finalizedOnly: {finalizedOnly.ToDebugString()})");
#endif
				lock (this)
				{
#if NET35
					var closureCount = FixedArgumentsRegistry.Count;
					for (var registryKey = 0; registryKey < closureCount; registryKey++)
					{
						if (!(FixedArgumentsRegistry[registryKey] is null) && (!finalizedOnly || !OwnerWeakReferenceRegistry[registryKey].IsAlive))
						{
							Deregister(registryKey);
						}
					}
#else
					// For .NET Framework 4.0+, this method should never be called with true finalizedOnly.
					if (finalizedOnly)
						throw new NotSupportedException();
					var deregisterUponFinalizes = (ICollection<DeregisterUponFinalize>)weakTableValuesGetMethod.Invoke(OwnerWeakReferenceRegistry, EmptyObjectArray);
					foreach (var deregisterUponFinalize in deregisterUponFinalizes)
						deregisterUponFinalize.Dispose();
					weakTableClearMethod.Invoke(OwnerWeakReferenceRegistry, EmptyObjectArray);
#endif
				}
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
						Registry.DeregisterAll(finalizedOnly: true);
					}
					finally
					{
						if (!AppDomain.CurrentDomain.IsFinalizingForUnload() && !Environment.HasShutdownStarted)
							new GCMonitor();
					}
				}
			}
#else
			sealed class DeregisterUponFinalize : IDisposable
			{
				readonly int registryKey;
				bool disposed = false;

				internal DeregisterUponFinalize(int registryKey) => this.registryKey = registryKey;

				~DeregisterUponFinalize() => Dispose(false);

				public void Dispose() => Dispose(true);

				void Dispose(bool disposing)
				{
					if (disposed)
						return;
					Registry.Deregister(registryKey);
					disposed = true;
					if (disposing)
						GC.SuppressFinalize(this);
				}

				public override string ToString() => $"ClosureRegistry.DeregisterUponFinalize{{{registryKey}}}";
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
			return (IsStatic ? "static " : MethodType is ClosureMethodType.BoundInstance ? "#" + FixedThisArgument.ToDebugString() + "#." : "") +
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
			var parameterStartIndex = 0;
			// If this ClosureMethod is the result of a Bind or AsStatic.
			if (MethodType == ClosureMethodType.BoundInstance)
			{
				obj = FixedThisArgument;
			}
			else if (MethodType == ClosureMethodType.InstanceAsStatic)
			{
				obj = parameters[0];
				parameterStartIndex++;
				parameterCount--;
			}
			var combinedArguments = new object[fixedArgumentCount + parameterCount];
			FixedArguments.CopyTo(combinedArguments, 0);
			Array.Copy(parameters, parameterStartIndex, combinedArguments, fixedArgumentCount, parameterCount);

			var returnValue = OriginalMethod.Invoke(obj, invokeAttr, binder, combinedArguments, culture);

			// In case any of the parameters are by-ref, copy back from combinedArguments to parameters.
			// TODO: Don't need to do this if there are no by-ref parameters.
			for (var index = 0; index < fixedArgumentCount; index++)
			{
				FixedArguments[index] = combinedArguments[index];
			}
			Array.Copy(combinedArguments, fixedArgumentCount, parameters, parameterStartIndex, parameterCount);

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
				// This is idempotent, so no need for locking. Not using System.Lazy<T> since it requires .NET Framework 4.0+.
				var methodName = this.methodName;
				if (methodName is null)
				{
					methodName = OriginalMethod.Name + "_" + MethodType;
					if (MethodType == ClosureMethodType.BoundInstance)
						methodName += "_" + ReplaceInvalidNameCharactersWithUnderscores(FixedThisArgument);
					if (FixedArguments.Count > 0)
						methodName += "_" + FixedArguments.Select(ReplaceInvalidNameCharactersWithUnderscores).Join("_");
					// Only assign to field once fully initialized to avoid potentially exposing uninitialized object to other threads.
					this.methodName = methodName;
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

		public override object[] GetCustomAttributes(Type attributeType, bool inherit) =>
			OriginalMethod.GetCustomAttributes(attributeType, inherit);

		public override MethodImplAttributes GetMethodImplementationFlags() => OriginalMethod.GetMethodImplementationFlags();

		public override ParameterInfo[] GetParameters()
		{
			// This is idempotent, so no need for locking. Not using System.Lazy<T> since it requires .NET Framework 4.0+.
			var nonFixedParameterInfos = this.nonFixedParameterInfos;
			if (nonFixedParameterInfos is null)
			{
				var fixedArgumentCount = FixedArguments.Count;
				var nonFixedParameterCount = originalParameterInfos.Length - fixedArgumentCount;
				var prefixParameterCount = MethodType == ClosureMethodType.InstanceAsStatic ? 1 : 0;
				nonFixedParameterInfos = new ParameterInfo[nonFixedParameterCount + prefixParameterCount];
				if (prefixParameterCount == 1)
					nonFixedParameterInfos[fixedArgumentCount] =
						new SimpleParameterInfo(member: this, position: 0, DeclaringType, name: "$this", defaultValue: null,
						ParameterAttributes.None);
				for (var position = 0; position < nonFixedParameterCount; position++)
					nonFixedParameterInfos[prefixParameterCount + position] =
						new WrapperParameterInfo(member: this, position: prefixParameterCount + position,
							originalParameterInfos[fixedArgumentCount + position]);
				// Only assign to field once fully initialized to avoid potentially exposing uninitialized object to other threads.
				this.nonFixedParameterInfos = nonFixedParameterInfos;
			}
			return nonFixedParameterInfos;
		}

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
			return new ClosureMethod(MethodType, genericMethod, this, methodAttributes, genericMethod.GetParameters(),
				FixedThisArgument, FixedArguments);
		}

#if !NET35
		override
#endif
		public Delegate CreateDelegate(Type delegateType)
		{
			if (delegateType is null)
				throw new ArgumentNullException(nameof(delegateType));
			if (!IsStatic || ContainsGenericParameters || !typeof(Delegate).IsAssignableFrom(delegateType))
				throw new ArgumentException("Cannot bind to the target method because its signature or security transparency is not " +
					"compatible with that of the delegate type.");
			return CreateClosureDelegate(delegateType, FixedThisArgument);
		}

#if !NET35
		override
#endif
		public Delegate CreateDelegate(Type delegateType, object target)
		{
			if (delegateType is null)
				throw new ArgumentNullException(nameof(delegateType));
			if (ContainsGenericParameters || !typeof(Delegate).IsAssignableFrom(delegateType))
				throw new ArgumentException("Cannot bind to the target method because its signature or security transparency is not " +
					"compatible with that of the delegate type.");
			// To match Delegate.CreateDelegate behavior, allow null target.
			// For any type of static ClosureMethod (ClosureMethodType.Static/InstanceAsStatic/BoundInstance), this is valid since target is ignored.
			// (In the BoundInstance case, FixedThisArgument always supersedes target, even if null.)
			// For an instance ClosureMethod (ClosureMethodType.Instance), don't throw ArgumentNullException, since method logic may either never use
			// the instance (i.e. "this" not accessed, whether directly or indirectly) or does use the instance in a null-safe way.
			// If the method logic does use the instance in a null-unsafe way, it will throw an ArgumentNullException
			// (wrapped in a TargetInvocationException due to delegate invocation).
			if (MethodType == ClosureMethodType.Instance)
			{
				if (!(target is null || DeclaringType.IsAssignableFrom(target.GetType())))
					throw new ArgumentException("Cannot bind to the target method because its signature or security transparency is not " +
						"compatible with that of the delegate type.");
			}
			else
			{
				// FixedThisArgument is always null if method type is not BoundInstance, which is fine,
				// since target is ignored for Static and InstanceAsStatic method types.
				target = FixedThisArgument;
			}
			return CreateClosureDelegate(delegateType, target);
		}

		// For easy access within the delegate dynamic method.
		static readonly List<IRefList<object>> FixedArgumentsRegistry = Registry.FixedArgumentsRegistry;

		static readonly FieldInfo FixedArgumentsRegistryField = typeof(ClosureMethod).GetField(nameof(FixedArgumentsRegistry),
			BindingFlags.Static | BindingFlags.NonPublic);
		static readonly MethodInfo GetCurrentMethodMethod = typeof(MethodBase).GetMethod(nameof(MethodBase.GetCurrentMethod));
		static readonly MethodInfo ListOfIRefListOfObjectItemGetMethod = typeof(List<IRefList<object>>).GetProperty("Item").GetGetMethod();
		static readonly MethodInfo IRefListOfObjectItemGetMethod = typeof(IRefList<object>).GetProperty("Item").GetGetMethod();
		static readonly MethodInfo IRefListOfObjectGetItemRefMethod = typeof(IRefList<object>).GetMethod(nameof(IRefList<object>.ItemRef));
		static readonly ConstructorInfo InvalidOperationExceptionConstructor =
			typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) });
		static readonly MethodInfo MethodBaseToDebugStringMethod =
			typeof(DebugExtensions).GetMethod(nameof(DebugExtensions.ToDebugString), new[] { typeof(MethodBase) });
		static readonly MethodInfo StringFormat2Method =
			typeof(string).GetMethod(nameof(string.Format), new[] { typeof(string), typeof(object), typeof(object) });

		Delegate CreateClosureDelegate(Type delegateType, object target)
		{
			var registryKey = Registry.Register();
			try
			{
				var dynamicMethod = CreateClosureDynamicMethod(target, registryKey);
				var closureDelegate = dynamicMethod.CreateDelegate(delegateType);
				// The closure "owner" is not the delegate itself, since if the last usage of the delegate is to call it (and its contained dynamic method),
				// the delegate object can become finalizable right as its being invoked and hands control over to the dynamic method's contained code.
				// The dynamic method itself doesn't seem to be finalizable during execution of its contained code
				// (or at least to the point that the fixedArguments are loaded in it).
				// Note: ClosureRegistry.ToString() will need to use reflection APIs on this dynamic method,
				// and Mono's DynamicMethod can sometimes throw NotSupportedException for certain reflection APIs.
				// This problem is handled in DebugExtension.ToDebugString(this MethodBase method).
				// XXX: An earlier implementation tried to use dynamicMethod.CreateDelegate(delegateType).Method,
				// but that could cause inexplicable crashes in Mono during reflection.
				// Apparently the RuntimeMethodInfo that Delegate.Method refers to could hold some sort of invalid function pointer or invalid metadata
				// (I'm not sure of the details).
				Registry.RegisterClosureOwner(registryKey, dynamicMethod);
				return closureDelegate;
			}
			catch (Exception)
			{
				// If anything went wrong, deregister the closure, then rethrow the exception.
				Registry.Deregister(registryKey);
				throw;
			}
		}

		DynamicMethod CreateClosureDynamicMethod(object target, int registryKey)
		{
#if GENERATE_DEBUG_DLL
			// XXX: While we could get a built method from DebugDynamicMethodBuilder (via TypeBuilder.GetMethod(methodBuilder.Name, parameterTypes)),
			// it seems that the using such a built method as a closure owner doesn't work - the built method apparently can become finalizable
			// (and thus could free the closure fixedArguments) before the delegate created from it is even called.
			// Workaround is to just use the non-debug DynamicMethodBuilder for both the closure owner and to create the delegate from,
			// while the DebugDynamicMethodBuilder's only purpose is to save a debug assembly dll.
			// I also tried just using a dummy DynamicMethod that simply passes through arguments to a TypeBuilder.GetMethod()-built method,
			// but that still suffered the same premature finalization issue.
			CreateClosureDynamicMethod(target, registryKey, new DebugDynamicMethodBuilder.Factory());
#endif

			return CreateClosureDynamicMethod(target, registryKey, new DynamicMethodBuilder.Factory());
		}

		DynamicMethod CreateClosureDynamicMethod(object target, int registryKey, IDynamicMethodBuilderFactory dynamicMethodBuilderFactory)
		{
			var methodType = MethodType;
			var originalMethod = OriginalMethod;
			var originalMethodIsStatic = originalMethod.IsStatic;
			var declaringType = originalMethod.DeclaringType;
			var returnType = originalMethod.ReturnType;

			var nonFixedParameterInfos = GetParameters();
			var nonFixedArgumentCount = nonFixedParameterInfos.Length;
			var nonFixedParameterTypes = new Type[nonFixedArgumentCount];
			for (var index = 0; index < nonFixedArgumentCount; index++)
				nonFixedParameterTypes[index] = nonFixedParameterInfos[index].ParameterType;

			var methodBuilder = dynamicMethodBuilderFactory.Create(
				originalMethod.Name + "_Closure_" + registryKey,
				// Some (all?) .NET implementations only accept the following MethodAttributes/CallingConventions for DynamicMethod.
				MethodAttributes.Public | MethodAttributes.Static,
				CallingConventions.Standard,
				declaringType,
				returnType,
				nonFixedParameterTypes);
			for (var index = 0; index < nonFixedArgumentCount; index++)
			{
				var parameter = nonFixedParameterInfos[index];
				// DefineParameter(0,...) refers to the return value, so for actual parameters, it's practically 1-based rather than 0-based.
				var parameterBuilder = methodBuilder.DefineParameter(index + 1, parameter.Attributes, parameter.Name);
				if ((parameter.Attributes & ParameterAttributes.HasDefault) == ParameterAttributes.HasDefault)
					parameterBuilder.SetConstant(parameter.DefaultValue);
				// XXX: Do any custom attributes like ParamArrayAttribute need to be copied too?
				// There's no good generic way to copy attributes as far as I can tell, since CustomAttributeBuilder is very cumbersome to use.
			}
			var ilGenerator = methodBuilder.GetILGenerator();

			// Since DynamicMethod can only be created as a static method, if original method is an instance method:
			// If type is Instance, prepend the closure fixed arguments with target (target passed to CreateDelegate).
			// If type is BoundInstance, prepend the closure fixed arguments with target (ClosureMethod.FixedThisArgument).
			// If type is InstanceAsStatic, don't need to prepend anything, since instance is passed as the first argument upon method invocation.
			// If type is Static, don't need to prepend anything, since there's no instance involved.
			var prefixArgumentCount = 0;
			var fixedArguments = FixedArguments;
			var fixedArgumentCount = fixedArguments.Count;
			if (methodType == ClosureMethodType.Instance || methodType == ClosureMethodType.BoundInstance)
			{
				fixedArguments = fixedArguments.ChainPrepend(target);
				prefixArgumentCount = 1;
				fixedArgumentCount++;
			}

			Registry.RegisterFixedArguments(registryKey, fixedArguments);

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
					ilGenerator.Emit(OpCodes.Call, typeof(GC).GetMethod(nameof(GC.Collect), Type.EmptyTypes));
					ilGenerator.Emit(OpCodes.Call, typeof(GC).GetMethod(nameof(GC.WaitForPendingFinalizers), Type.EmptyTypes));
				}
#if GENERATE_DEBUG_LOGGING
				ilGenerator.Emit(OpCodes.Ldstr, "DEBUG CreateClosureDelegate: registryKey={0} inside method:\n{1}");
				ilGenerator.EmitLdcI4(registryKey);
				ilGenerator.Emit(OpCodes.Box, typeof(int));
				ilGenerator.Emit(OpCodes.Ldsfld, typeof(ClosureMethod).GetField(nameof(Registry), BindingFlags.Static | BindingFlags.NonPublic));
				ilGenerator.Emit(OpCodes.Call, typeof(DebugExtensions).GetMethod(nameof(DebugExtensions.ToDebugString), new[] { typeof(object) }));
				ilGenerator.Emit(OpCodes.Call, StringFormat2Method);
				ilGenerator.Emit(OpCodes.Call, typeof(Logging).GetMethod(nameof(Logging.StringLog)));
#endif
#endif

				// Emit the code that loads the fixed arguments from ClosureMethod.FixedArgumentsRegistry into a local variable.
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
			// and target (whose type must be the declaring type) must be unboxed into an address for the call opcode.
			// Otherwise, we'll be using a callvirt opcode. If target is a value type, then the method's declaring type is either
			// Object/ValueType/Enum or an interface, and target needs to be boxed.
			var useCallOpcode = originalMethodIsStatic || !originalMethod.IsVirtual || originalMethod.IsFinal;

			switch (methodType)
			{
			// Instance => target passed to CreateDelegate is passed as target to this method.
			case ClosureMethodType.Instance:
			// BoundInstance => ClosureMethod.FixedThisArgument is passed as target to this method.
			case ClosureMethodType.BoundInstance:
				// Instance, specified as fixedThisArgument, is either hard-coded in as a constant...
				if (!ilGenerator.TryEmitConstant(target))
				{
					// ... or is the first element of the closure fixed arguments.
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
						// If declaringType (the type of the target) is a value type, target needs to be boxed.
						// Fortunately, since it's in the closure fixedArguments (of type IRefList<object>), it's already boxed,
						// so we don't need to do anything extra.
					}
				}
				break;
			case ClosureMethodType.InstanceAsStatic:
				// Instance is passed as the first argument.
				ilGenerator.Emit(OpCodes.Ldarg_0);
				break;
			case ClosureMethodType.Static:
				// No instance needs to be loaded first.
				break;
			default:
				throw new NotImplementedException($"Unrecognized ClosureMethodType: {methodType}");
			}

			// Emit fixed arguments, some that can be hard-coded in as constants, others needing the closure reflist.
			for (var index = prefixArgumentCount; index < fixedArgumentCount; index++)
			{
				var fixedArgument = fixedArguments[index];
				var fixedArgumentType = originalParameterInfos[index - prefixArgumentCount].ParameterType;
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
			ilGenerator.Emit(useCallOpcode ? OpCodes.Call : OpCodes.Callvirt, originalMethod);

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

	class WrapperParameterInfo : ParameterInfo
	{
		readonly ParameterInfo parameter;

		internal WrapperParameterInfo(MemberInfo member, int position, ParameterInfo parameter)
		{
			this.parameter = parameter;
			// Reusing MemberImpl and PositionImpl legacy protected fields instead of defining our own private fields for them.
			// These aren't guaranteed to be used in the ParameterInfo implementations of the corresponding public properties,
			// so we'll have to also override those to explicitly defer to these protected fields.
			MemberImpl = member;
			PositionImpl = position;
		}

		public override MemberInfo Member => MemberImpl;

		public override int Position => PositionImpl;

		public override Type ParameterType => parameter.ParameterType;

		public override string Name => parameter.Name;

		public override object DefaultValue => parameter.DefaultValue;

#if MONO && NET35
		// Apparently Mono .NET Framework 3.5's implementation of ParameterInfo.RawDefaultValue is non-virtual
		// and simply delegates to DefaultValue (or DefaultValueImpl in even older versions?), so don't try overriding it in that case.
#else
		public override object RawDefaultValue => parameter.RawDefaultValue;
#endif

		public override ParameterAttributes Attributes => parameter.Attributes;

#if !NET35
		public override bool HasDefaultValue => parameter.HasDefaultValue;

		public override int MetadataToken => parameter.MetadataToken;

		public override IEnumerable<CustomAttributeData> CustomAttributes => parameter.CustomAttributes;
#endif

		public override object[] GetCustomAttributes(bool inherit) => parameter.GetCustomAttributes(inherit);

		public override object[] GetCustomAttributes(Type attributeType, bool inherit) => parameter.GetCustomAttributes(attributeType, inherit);

		public override Type[] GetOptionalCustomModifiers() => parameter.GetOptionalCustomModifiers();

		public override Type[] GetRequiredCustomModifiers() => parameter.GetRequiredCustomModifiers();

		public override bool IsDefined(Type attributeType, bool inherit) => parameter.IsDefined(attributeType, inherit);

		public override string ToString() => parameter.ToString();
	}

	class SimpleParameterInfo : ParameterInfo
	{
		internal SimpleParameterInfo(MemberInfo member, int position, Type parameterType, string name, object defaultValue,
			ParameterAttributes attributes)
		{
			// Reusing legacy protected fields instead of defining our own private fields for them.
			// These aren't guaranteed to be used in the ParameterInfo implementations of the corresponding public properties,
			// so we'll have to also override those to explicitly defer to these protected fields.
			MemberImpl = member;
			PositionImpl = position;
			ClassImpl = parameterType;
			NameImpl = name;
			DefaultValueImpl = defaultValue;
			AttrsImpl = attributes;
		}

		public override MemberInfo Member => MemberImpl;

		public override int Position => PositionImpl;

		public override Type ParameterType => ClassImpl;

		public override string Name => NameImpl;

		public override object DefaultValue => DefaultValueImpl;

#if MONO && NET35
		// Apparently Mono .NET Framework 3.5's implementation of ParameterInfo.RawDefaultValue is non-virtual
		// and simply delegates to DefaultValue (or DefaultValueImpl in even older versions?), so don't try overriding it in that case.
#else
		public override object RawDefaultValue => DefaultValueImpl;
#endif

		public override ParameterAttributes Attributes => AttrsImpl;

#if !NET35
		public override bool HasDefaultValue => (AttrsImpl & ParameterAttributes.HasDefault) == ParameterAttributes.HasDefault;

		public override int MetadataToken => MemberImpl.MetadataToken;

		public override IEnumerable<CustomAttributeData> CustomAttributes => Enumerable.Empty<CustomAttributeData>();
#endif

		public override object[] GetCustomAttributes(bool inherit) => ClosureMethod.EmptyObjectArray;

		public override object[] GetCustomAttributes(Type attributeType, bool inherit) => ClosureMethod.EmptyObjectArray;

		public override Type[] GetOptionalCustomModifiers() => Type.EmptyTypes;

		public override Type[] GetRequiredCustomModifiers() => Type.EmptyTypes;

		public override bool IsDefined(Type attributeType, bool inherit) => false;

		public override string ToString()
		{
			// Based off mono's ParameterInfo.ToString code.
			var type = ClassImpl;
			while (type.HasElementType)
				type = type.GetElementType();
			var useFullName = !type.IsPrimitive && ClassImpl != typeof(void) && !(ClassImpl.Namespace == MemberImpl.DeclaringType.Namespace);
			var text = useFullName ? ClassImpl.FullName : ClassImpl.Name;
			if (!IsRetval)
				text += ' ' + NameImpl;
			return text;
		}
	}
}
