using System;
using System.Reflection;
#if NET35
using System.Reflection.Emit;
#endif

namespace LbmLib.Language.Experimental
{
	public static class MethodClosureExtensions
	{
#if NET35
		// Polyfill of MethodInfo.CreateDelegate for .NET Framework 3.5 with support for ClosureMethod.
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

		// Polyfill of MethodInfo.CreateDelegate for .NET Framework 3.5 with support for ClosureMethod.
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

		public static T CreateDelegate<T>(this MethodInfo method) where T : Delegate => (T)method.CreateDelegate(typeof(T));

		public static T CreateDelegate<T>(this MethodInfo method, object target) where T : Delegate => (T)method.CreateDelegate(typeof(T), target);

		static readonly IRefList<object> EmptyRefList = new object[0].AsRefList();

		public static MethodInfo AsStatic(this MethodInfo method)
		{
			// Note: Bound methods (ClosureMethod with an assigned FixedThisArgument) are already static.
			if (method.IsStatic)
				return method;
			MethodInfo genericMethodDefinition = null;
			if (method.ContainsGenericParameters)
			{
				genericMethodDefinition = method.GetGenericMethodDefinition();
				if (method != genericMethodDefinition)
					genericMethodDefinition = AsStaticInternal(genericMethodDefinition, genericMethodDefinition);
			}
			return AsStaticInternal(method, genericMethodDefinition);
		}

		static MethodInfo AsStaticInternal(MethodInfo method, MethodInfo genericMethodDefinition)
		{
			if (method is ClosureMethod closureMethod)
				return new ClosureMethod(closureMethod, ClosureMethodType.InstanceAsStatic, genericMethodDefinition,
					closureMethod.Attributes | MethodAttributes.Static, fixedThisArgument: null, closureMethod.FixedArguments);
			else
				return new ClosureMethod(ClosureMethodType.InstanceAsStatic, method, genericMethodDefinition,
					method.Attributes | MethodAttributes.Static, method.GetParameters(), fixedThisArgument: null, EmptyRefList);
		}

		// Equivalent to: method.IsStatic ? throw new ArgumentException(...) : method.AsStatic().PartialApply(target)
		public static ClosureMethod Bind(this MethodInfo method, object target)
		{
			// Note: target is allowed by null, for parity with target in CreateDelegate(delegateType, target) also allowed to be null,
			// even for value types.
			if (method.IsStatic)
				throw new ArgumentException($"method {method.ToDebugString()} cannot be a static method");
			if (!(target is null) && !method.DeclaringType.IsAssignableFrom(target.GetType()))
				throw new ArgumentException($"target's type ({target.GetType().ToDebugString()}) " +
					$"is not compatible with method.DeclaringType ({method.DeclaringType.ToDebugString()})");
			var genericMethodDefinition = method.IsGenericMethod ? method.GetGenericMethodDefinition() : null;
			if (method is ClosureMethod closureMethod)
				return new ClosureMethod(closureMethod, ClosureMethodType.BoundInstance, genericMethodDefinition,
					closureMethod.Attributes | MethodAttributes.Static, target, closureMethod.FixedArguments);
			else
				return new ClosureMethod(ClosureMethodType.BoundInstance, method, genericMethodDefinition,
					method.Attributes | MethodAttributes.Static, method.GetParameters(), target, EmptyRefList);
		}

		public static ClosureMethod PartialApply(this MethodInfo method, params object[] fixedArguments)
		{
			if (fixedArguments is null)
				throw new ArgumentNullException(nameof(fixedArguments));
			var currentNonFixedParameterInfos = method.GetParameters();
			var fixedArgumentCount = fixedArguments.Length;
			if (fixedArgumentCount > currentNonFixedParameterInfos.Length)
				throw new ArgumentOutOfRangeException($"fixedArguments.Length ({fixedArgumentCount}) cannot be > method.GetParameters().Length " +
					$"({currentNonFixedParameterInfos.Length})");
			for (var index = 0; index < fixedArgumentCount; index++)
			{
				var parameterInfo = currentNonFixedParameterInfos[index];
				if (parameterInfo.ParameterType.ContainsGenericParameters)
					throw new ArgumentException($"method.GetParameters()[{index}] ({parameterInfo}) " +
						"contains generic parameters and cannot have a fixed argument");
				var fixedArgument = fixedArguments[index];
				var parameterType = parameterInfo.ParameterType;
				if (parameterType.IsByRef)
					parameterType = parameterType.GetElementType();
				if (fixedArgument is null)
				{
					if (parameterType.IsValueType)
						throw new ArgumentException($"method.GetParameters()[{index}] ({parameterInfo}) " +
							"is a value type and cannot be null");
				}
				else if (!parameterType.IsAssignableFrom(fixedArgument.GetType()))
					throw new ArgumentException($"method.GetParameters()[{index}] ({parameterInfo}) " +
						$"is not compatible with fixedArguments[{index}] ({fixedArgument.ToDebugString()})");
			}

			MethodInfo genericMethodDefinition = null;
			if (method.IsGenericMethod)
			{
				genericMethodDefinition = method.GetGenericMethodDefinition();
				if (method != genericMethodDefinition)
					genericMethodDefinition = PartialApplyInternal(genericMethodDefinition, genericMethodDefinition, fixedArguments);
			}
			return PartialApplyInternal(method, genericMethodDefinition, fixedArguments);
		}

		static ClosureMethod PartialApplyInternal(MethodInfo method, MethodInfo genericMethodDefinition, object[] fixedArguments)
		{
			var fixedArgumentRefList = fixedArguments.AsRefList();
			if (method is ClosureMethod closureMethod)
			{
				var fixedThisArgument = closureMethod.FixedThisArgument;
				var methodType = closureMethod.MethodType;
				// If method type is InstanceAsStatic, bind the first fixed argument as the FixedThisArgument.
				if (methodType is ClosureMethodType.InstanceAsStatic)
				{
					methodType = ClosureMethodType.BoundInstance;
					fixedThisArgument = fixedArguments[0];
					fixedArgumentRefList = fixedArgumentRefList.GetRangeView(1, fixedArguments.Length - 1);
				}
				return new ClosureMethod(closureMethod, methodType, genericMethodDefinition,
					closureMethod.Attributes, fixedThisArgument, closureMethod.FixedArguments.ChainConcat(fixedArgumentRefList));
			}
			else
			{
				var methodType = method.IsStatic ? ClosureMethodType.Static : ClosureMethodType.Instance;
				return new ClosureMethod(methodType, method, genericMethodDefinition,
					method.Attributes, method.GetParameters(), null, fixedArgumentRefList);
			}
		}
	}
}
