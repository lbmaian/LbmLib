using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices; // for ConditionalWeakTable (.NET 4+) and MethodImpl
#if NET35
using System.Runtime.InteropServices; // for GCHandle
#endif

namespace LbmLib.Language.Experimental
{
	public static partial class MethodClosureExtensions
	{
		const bool ClosureMethodGenerateDebugDll = false;
	}

	public sealed class ClosureMethod : MethodInfo
	{
		public MethodInfo OriginalMethod { get; }

		readonly MethodAttributes methodAttributes;
		readonly ParameterInfo[] nonFixedParameterInfos;
		readonly MethodInfo genericMethodDefinition;

		public object FixedThisArgument { get; }

		internal object[] FixedArguments;

		public object[] GetFixedArguments() => FixedArguments.Copy();

		// Lazily computed.
		string methodName;

		internal ClosureMethod(MethodInfo originalMethod, MethodAttributes methodAttributes, ParameterInfo[] nonFixedParameterInfos,
			MethodInfo genericMethodDefinition, object fixedThisArgument, object[] fixedArguments)
		{
			OriginalMethod = originalMethod;
			this.methodAttributes = methodAttributes;
			this.nonFixedParameterInfos = nonFixedParameterInfos;
			this.genericMethodDefinition = genericMethodDefinition;
			FixedThisArgument = fixedThisArgument;
			FixedArguments = fixedArguments;
		}

		internal static readonly ClosureDelegateRegistry DelegateRegistry = new ClosureDelegateRegistry();
		// For easy access within the delegate dynamic method.
		internal static readonly List<object[]> DelegateClosures = DelegateRegistry.Closures;

		internal class ClosureDelegateRegistry
		{
			// Closures is the actual registry of fixedArguments arrays, with closureKey being an index into it.
			// It's an object[] rather than a custom struct so that other assemblies can more easily access it, as is required with DebugDynamicMethodBuilder,
			// since other assemblies can only access public structures, fields, etc.
			// (or else would require expensive reflection within the dynamically-generated method).
			// DebugDynamicMethodBuilder's build type will have its own field that stores a reference to this Closures object.
			internal readonly List<object[]> Closures = new List<object[]>();

#if NET35
			readonly List<GCHandle> WeakReferences = new List<GCHandle>();
#else
			readonly ConditionalWeakTable<Delegate, DeregisterUponFinalize> WeakReferences =
				new ConditionalWeakTable<Delegate, DeregisterUponFinalize>();
#endif

			int minimumFreeClosureKey = 0;

			public int ReserveNextFreeClosureKey()
			{
				// XXX: This is thread-locking and O(n) even with a minimum key optimization,
				// but it should suffice for now, so long as delegate closures aren't created in the thousands or so.
				lock (this)
				{
					var delegateClosuresCount = Closures.Count;
					while (minimumFreeClosureKey < delegateClosuresCount)
					{
						var closureKey = minimumFreeClosureKey;
						minimumFreeClosureKey++;
						if (Closures[closureKey] is null)
							return closureKey;
					}
					minimumFreeClosureKey++;
					return delegateClosuresCount;
				}
			}

			public void Register(int closureKey, object[] closure, Delegate closureDelegate)
			{
				lock (this)
				{
					var closureCount = Closures.Count;
					if (closureKey < closureCount)
					{
						Closures[closureKey] = closure;
#if NET35
						var gcHandle = WeakReferences[closureKey];
						gcHandle.Target = closureDelegate;
#endif
					}
					else
					{
						while (closureKey > closureCount)
						{
							Closures.Add(null);
#if NET35
							WeakReferences.Add(GCHandle.Alloc(null, GCHandleType.Weak));
#endif
							closureCount++;
						}
						Closures.Add(closure);
#if NET35
						WeakReferences.Add(GCHandle.Alloc(closureDelegate, GCHandleType.Weak));
#endif
					}
#if !NET35
					WeakReferences.Add(closureDelegate, new DeregisterUponFinalize(closureKey));
#endif
				}
				Logging.Log($"DEBUG Register(closureKey={closureKey}, closure={closure.ToDebugString()} closureDelegate={closureDelegate.Method.ToDebugString()})");
#if NET35
				GCMonitor.EnsureStarted();
#endif
			}

			public void Deregister(int closureKey)
			{
				lock (this)
				{
					Closures[closureKey] = null;
					if (minimumFreeClosureKey > closureKey)
						minimumFreeClosureKey = closureKey;
#if NET35
					WeakReferences[closureKey].Free();
#endif
				}
				Logging.Log($"DEBUG Deregister(closureKey={closureKey}), minimumFreeClosureKey={minimumFreeClosureKey}");
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
						Logging.Log("DEBUG ~GCMonitor()");
						lock (DelegateRegistry)
						{
							var closures = DelegateRegistry.Closures;
							var weakReferences = DelegateRegistry.WeakReferences;
							var closuresCount = closures.Count;
							for (var closureKey = 0; closureKey < closuresCount; closureKey++)
							{
								if (!(closures[closureKey] is null) && weakReferences[closureKey].Target is null)
								{
									DelegateRegistry.Deregister(closureKey);
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

			// In case the ClosureMethod class is somehow unloaded, clean up all GCHandle's.
			~ClosureDelegateRegistry()
			{
				foreach (var weakReference in WeakReferences)
				{
					if (weakReference.IsAllocated)
						weakReference.Free();
				}
			}
#else
			sealed class DeregisterUponFinalize
			{
				readonly int closureKey;

				internal DeregisterUponFinalize(int closureKey)
				{
					this.closureKey = closureKey;
				}

				~DeregisterUponFinalize()
				{
					DelegateRegistry.Deregister(closureKey);
				}

				public override string ToString() => $"DeregisterUponFinalize{{{closureKey}}}";
			}
#endif

			public override string ToString()
			{
				lock (this)
				{
					return $"ClosureDelegateRegistry.Closures:\n\t{Closures.ToDebugString("\n\t", false)}\n" +
						$"ClosureDelegateRegistry.WeakReferences\n\t{WeakReferences.ToDebugString("\n\t", false)}";
				}
			}
		}

		public override string ToString() =>
			ReturnType.Name + " " + Name +
			(IsGenericMethod ? "[" + GetGenericArguments().Join(",") + "]" : "") +
			"(" + GetParameters().Join(", ") + ")";

		public string ToDebugString(bool includeNamespace = true, bool includeDeclaringType = true)
		{
			return (IsStatic ? "static " : FixedThisArgument is null ? "" : "#" + FixedThisArgument.ToDebugString() + "#.") +
				ReturnType.ToDebugString(includeNamespace, includeDeclaringType) + " " +
				(!includeDeclaringType || DeclaringType is null ? "" : DeclaringType.ToDebugString(includeNamespace, includeDeclaringType) + "::") +
				OriginalMethod.Name + "(" + Enumerable.Concat(
					FixedArguments.Select(argument => "#" + argument.ToDebugString() + "#"),
					GetParameters().Select(parameter => parameter.ToDebugString(includeNamespace, includeDeclaringType))).Join() + ")";
		}

		public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
		{
			// TODO: Could lazily optimize this with a delegate that accepts as parameters: fixedArguments, parameters.
			var combinedArguments = FixedArguments.Append(parameters);
			var returnValue = OriginalMethod.Invoke(FixedThisArgument ?? obj, invokeAttr, binder, combinedArguments, culture);
			// In case any of the parameters are by-ref, copy back from combinedArguments to parameters.
			Array.Copy(combinedArguments, FixedArguments.Length, parameters, 0, parameters.Length);
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
					if (FixedArguments.Length > 0)
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
			return new ClosureMethod(genericMethod, methodAttributes, genericMethod.GetParameters().CopyToEnd(FixedArguments.Length),
				this, FixedThisArgument, FixedArguments);
		}

#if !NET35
		override
#endif
		public Delegate CreateDelegate(Type delegateType)
		{
			if (!IsStatic || !typeof(Delegate).IsAssignableFrom(delegateType) || ContainsGenericParameters)
				throw new ArgumentException("Cannot bind to the target method because its signature or security transparency is not " +
					"compatible with that of the delegate type.");
			return MethodClosureExtensions.CreateClosureDelegate(delegateType, OriginalMethod, FixedThisArgument, FixedArguments);
		}

#if !NET35
		override
#endif
		public Delegate CreateDelegate(Type delegateType, object target)
		{
			if (target is null)
				throw new ArgumentNullException(nameof(target));
			if (IsStatic || !typeof(Delegate).IsAssignableFrom(delegateType) || ContainsGenericParameters)
				throw new ArgumentException("Cannot bind to the target method because its signature or security transparency is not " +
					"compatible with that of the delegate type.");
			return MethodClosureExtensions.CreateClosureDelegate(delegateType, OriginalMethod, target, FixedArguments);
		}
	}

	public static partial class MethodClosureExtensions
	{
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

		// More convenient generic overloads of CreateDelegate.

		[MethodImpl(256)] // AggressiveInlining
		public static T CreateDelegate<T>(this MethodInfo method) where T : Delegate => (T)method.CreateDelegate(typeof(T));

		[MethodImpl(256)] // AggressiveInlining
		public static T CreateDelegate<T>(this MethodInfo method, object target) where T : Delegate => (T)method.CreateDelegate(typeof(T), target);

		public static ClosureMethod Bind(this MethodInfo method, object target)
		{
			if (target is null)
				throw new ArgumentNullException(nameof(target));
			if (method.IsStatic)
				throw new ArgumentException($"method {method.ToDebugString()} cannot be a static method");
			if (!method.DeclaringType.IsAssignableFrom(target.GetType()))
				throw new ArgumentException($"target's type ({target.GetType().ToDebugString()}) " +
					$"must be assignable to method.DeclaringType ({method.DeclaringType.ToDebugString()})");
			var genericMethodDefinition = method.IsGenericMethod ? method.GetGenericMethodDefinition() : null;
			if (method is ClosureMethod closureMethod)
				return new ClosureMethod(closureMethod.OriginalMethod, closureMethod.Attributes | MethodAttributes.Static, closureMethod.GetParameters(),
					genericMethodDefinition, target, closureMethod.FixedArguments.Copy());
			else
				return new ClosureMethod(method, method.Attributes | MethodAttributes.Static, method.GetParameters(),
					genericMethodDefinition, target, new object[0]);
		}

		static readonly MethodInfo GetCurrentMethodMethod = typeof(MethodBase).GetMethod(nameof(MethodBase.GetCurrentMethod));
		static readonly MethodInfo ListItemGetMethod = typeof(List<object[]>).GetProperty("Item").GetGetMethod();
		static readonly ConstructorInfo InvalidOperationExceptionConstructor = typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) });
		static readonly MethodInfo MethodBaseToDebugStringMethod = typeof(DebugExtensions).GetMethod(nameof(DebugExtensions.ToDebugString), new[] { typeof(MethodBase) });
		static readonly MethodInfo StringFormat2Method = typeof(string).GetMethod(nameof(string.Format), new[] { typeof(string), typeof(object), typeof(object) });

		public static ClosureMethod PartialApply(this MethodInfo method, params object[] fixedArguments)
		{
			if (fixedArguments is null)
				throw new ArgumentNullException(nameof(fixedArguments));
			var parameterInfos = method.GetParameters();
			var fixedArgumentCount = fixedArguments.Length;
			if (fixedArgumentCount > parameterInfos.Length)
				throw new ArgumentOutOfRangeException($"fixedArguments.Length ({fixedArgumentCount}) cannot be > method.GetParameters().Length " +
					$"({parameterInfos.Length})");
			var containsGenericParameters = method.ContainsGenericParameters;
			for (var index = 0; index < fixedArgumentCount; index++)
			{
				var parameterInfo = parameterInfos[index];
				if (containsGenericParameters && parameterInfo.ParameterType.ContainsGenericParameters)
					throw new ArgumentException($"method.GetParameters()[{index}] ({parameterInfo.ToDebugString()}) " +
						"contains generic parameters and cannot have a fixed argument");
				var fixedArgument = fixedArguments[index];
				var parameterType = parameterInfo.ParameterType;
				if (fixedArgument is null)
				{
					if (parameterType.IsValueType)
						throw new ArgumentException($"method.GetParameters()[{index}] ({parameterInfo.ToDebugString()}) " +
							"is a value type and cannot be null");
				}
				else if (!parameterType.IsAssignableFrom(fixedArgument.GetType()))
					throw new ArgumentException($"method.GetParameters()[{index}] ({parameterInfo.ToDebugString()}) " +
						$"is not compatible with fixedArguments[{index}] ({fixedArgument.ToDebugString()})");
			}

			var genericMethodDefinition = default(MethodInfo);
			if (method.IsGenericMethod)
			{
				genericMethodDefinition = method.GetGenericMethodDefinition();
				genericMethodDefinition = PartialApplyInternal(genericMethodDefinition, genericMethodDefinition,
					genericMethodDefinition.GetParameters(), fixedArguments);
			}
			return PartialApplyInternal(method, genericMethodDefinition, parameterInfos, fixedArguments);
		}

		static ClosureMethod PartialApplyInternal(MethodInfo method, MethodInfo genericMethodDefinition, ParameterInfo[] parameterInfos,
			object[] fixedArguments)
		{
			var nonFixedParameterInfos = parameterInfos.CopyToEnd(fixedArguments.Length);
			if (method is ClosureMethod closureMethod)
				return new ClosureMethod(closureMethod.OriginalMethod, closureMethod.Attributes, nonFixedParameterInfos,
					genericMethodDefinition, closureMethod.FixedThisArgument, closureMethod.FixedArguments.Append(fixedArguments));
			else
				return new ClosureMethod(method, method.Attributes, nonFixedParameterInfos, genericMethodDefinition, null, fixedArguments.Copy());
		}

		// TODO: Move into ClosureMethod.CreateDelegate?
		internal static Delegate CreateClosureDelegate(Type delegateType, MethodInfo method, object fixedThisArgument, object[] fixedArguments)
		{
			var parameters = method.GetParameters();
			var fixedArgumentCount = fixedArguments.Length;
			var nonFixedArgumentCount = parameters.Length - fixedArgumentCount;
			var isStatic = method.IsStatic;
			var declaringType = method.DeclaringType;
			var returnType = method.ReturnType;

			// Ref, in, and out parameters aren't allowed to be fixed arguments.
			// TODO: Somehow support this - such fixed arguments would need to be stored in a temp variable and then passed via ldloca or ldelema?
			for (var index = 0; index < fixedArgumentCount; index++)
			{
				var parameter = parameters[index];
				if (parameter.ParameterType.IsByRef)
					throw new ArgumentException("Cannot partial apply with a fixed argument that is a ref, in, or out parameter: " + parameter);
			}

			// Keep a copy of fixedArgumentCount/nonFixedArgumentCount values for parameter indexing, since the *ArgumentCount values can change.
			var fixedParameterCount = fixedArgumentCount;
			var nonFixedParameterCount = nonFixedArgumentCount;

			// Since DynamicMethod can only be created as a static method, if method is an instance method and fixedThisArgument is null
			// (and thus will be a non-bound instance method), we will need to prepend an additional non-fixed argument for the instance
			// to the dynamic method's parameter type array.
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
					fixedArguments = fixedArguments.Prepend(fixedThisArgument);
				}
			}

			// See above - need to prepend an additional non-fixed argument for non-bound instance methods to the dynamic method's parameter type array.
			var nonFixedParameterTypes = new Type[nonFixedArgumentCount];
			if (prefixArgumentCount == 1)
				nonFixedParameterTypes[0] = declaringType;
			for (var index = 0; index < nonFixedParameterCount; index++)
			{
				nonFixedParameterTypes[prefixArgumentCount + index] = parameters[fixedParameterCount + index].ParameterType;
			}

			var closureKey = ClosureMethod.DelegateRegistry.ReserveNextFreeClosureKey();
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
			for (int index = 0; index < nonFixedParameterCount; index++)
			{
				var parameter = parameters[fixedParameterCount + index];
				// DefineParameter(0,...) refers to the return value, so for actual parameters, it's practically 1-based rather than 0-based.
				methodBuilder.DefineParameter(prefixArgumentCount + index + 1, parameter.Attributes, parameter.Name);
				// XXX: Do any custom attributes like ParamArrayAttribute need to be copied too?
				// There's no good generic way to copy attributes as far as I can tell, since CustomAttributeBuilder is very cumbersome to use.
			}
			var ilGenerator = methodBuilder.GetILGenerator();

			// Create the non-constant fixed arguments that are stored in the closure, and loaded within the dynamic method if needed.
			var fixedNonConstantArgumentsVar = default(LocalBuilder);
			var fixedNonConstantArguments = fixedArguments.Where(fixedArgument => !ilGenerator.CanEmitConstant(fixedArgument)).ToArray();
			if (fixedNonConstantArguments.Length > 0)
			{
				//ilGenerator.Emit(OpCodes.Ldstr, "DEBUG CreateClosureDelegate: closureKey={0} inside method=\"{1}\"");
				//ilGenerator.EmitLdcI4(closureKey);
				//ilGenerator.Emit(OpCodes.Box, typeof(int));
				//ilGenerator.Emit(OpCodes.Call, GetCurrentMethodMethod);
				//ilGenerator.Emit(OpCodes.Call, MethodBaseToDebugStringMethod);
				//ilGenerator.Emit(OpCodes.Call, StringFormat2Method);
				//ilGenerator.Emit(OpCodes.Call, typeof(Logging).GetMethod(nameof(Logging.StringLog)));

				// Emit the code that loads the fixed arguments from ClosureMethod.ClosuresField into a local variable.
				fixedNonConstantArgumentsVar = ilGenerator.DeclareLocal(typeof(object[]));
				ilGenerator.Emit(OpCodes.Ldsfld, methodBuilder.GetDelegateClosuresField());
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

				//ilGenerator.Emit(OpCodes.Ldstr, "DEBUG CreateClosureDelegate: closureKey={0} inside method: closure=\"{1}\"");
				//ilGenerator.EmitLdcI4(closureKey);
				//ilGenerator.Emit(OpCodes.Box, typeof(int));
				//ilGenerator.EmitLdloc(fixedNonConstantArgumentsVar);
				//ilGenerator.Emit(OpCodes.Call, typeof(DebugExtensions).GetMethod(nameof(DebugExtensions.ToDebugString), new[] { typeof(object) }));
				//ilGenerator.Emit(OpCodes.Call, StringFormat2Method);
				//ilGenerator.Emit(OpCodes.Call, typeof(Logging).GetMethod(nameof(Logging.StringLog)));
			}

			// Emit the non-fixed instance that will be passed as 0th argument via ClosureMethod if needed.
			if (prefixArgumentCount == 1)
				ilGenerator.Emit(OpCodes.Ldarg_0);

			// Emit fixed arguments, some that can be hard-coded in as constants, others needing the closure array.
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

			var closureDelegate = methodBuilder.CreateDelegate(delegateType);
			ClosureMethod.DelegateRegistry.Register(closureKey, fixedNonConstantArguments, closureDelegate);
			return closureDelegate;
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

			Delegate CreateDelegate(Type delegateType);

			FieldInfo GetDelegateClosuresField();
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

			public Delegate CreateDelegate(Type delegateType) => dynamicMethod.CreateDelegate(delegateType);

			static readonly FieldInfo DelegateClosuresField =
				typeof(ClosureMethod).GetField(nameof(ClosureMethod.DelegateClosures), BindingFlags.Static | BindingFlags.NonPublic);

			public FieldInfo GetDelegateClosuresField() => DelegateClosuresField;
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

					var delegateClosuresHolderTypeBuilder = moduleBuilder.DefineType(typeNamePrefix + "DelegateClosuresHolder");
					delegateClosuresHolderTypeBuilder.DefineField(nameof(ClosureMethod.DelegateClosures), typeof(List<object[]>), FieldAttributes.Static);
					var delegateClosuresHolderType = delegateClosuresHolderTypeBuilder.CreateType();
					var delegateClosuresField = delegateClosuresHolderType.GetField(nameof(ClosureMethod.DelegateClosures),
						BindingFlags.Static | BindingFlags.NonPublic);
					delegateClosuresField.SetValue(null, ClosureMethod.DelegateClosures);

					return new DebugDynamicMethodBuilder(dirPath, parameterTypes, assemblyBuilder, typeBuilder, methodBuilder, delegateClosuresHolderType);
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

			public Delegate CreateDelegate(Type delegateType)
			{
				typeBuilder.CreateType();
				var fileName = methodBuilder.Name + ".dll";
				assemblyBuilder.Save(fileName);
				Logging.Log("Saved dynamically created partial applied method to " + Path.Combine(dirPath, fileName));
				// A MethodBuilder can't be Invoke'd (nor can its MethodHandle be obtained), so get the concrete method from the just-built type.
				var method = typeBuilder.GetMethod(methodBuilder.Name, parameterTypes);
				return Delegate.CreateDelegate(delegateType, method);
			}

			public FieldInfo GetDelegateClosuresField() => closuresHolderType.GetField(nameof(ClosureMethod.DelegateClosures),
				BindingFlags.Static | BindingFlags.NonPublic);
		}
	}
}
