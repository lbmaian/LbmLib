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

		public static ClosureMethod Bind(this MethodInfo method, object target)
		{
			if (target is null)
				throw new ArgumentNullException(nameof(target));
			if (method.IsStatic)
				throw new ArgumentException($"method {method.ToDebugString()} cannot be a static method");
			if (!method.DeclaringType.IsAssignableFrom(target.GetType()))
				throw new ArgumentException($"target's type ({target.GetType().ToDebugString()}) " +
					$"is not compatible with method.DeclaringType ({method.DeclaringType.ToDebugString()})");
			var genericMethodDefinition = method.IsGenericMethod ? method.GetGenericMethodDefinition() : null;
			if (method is ClosureMethod closureMethod)
				return new ClosureMethod(closureMethod.OriginalMethod, closureMethod.Attributes | MethodAttributes.Static, closureMethod.GetParameters(),
					genericMethodDefinition, target, closureMethod.FixedArguments);
			else
				return new ClosureMethod(method, method.Attributes | MethodAttributes.Static, method.GetParameters(),
					genericMethodDefinition, target, EmptyRefList);
		}

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
				if (parameterType.IsByRef)
					parameterType = parameterType.GetElementType();
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
					genericMethodDefinition, closureMethod.FixedThisArgument, closureMethod.FixedArguments.ChainAppend(fixedArguments));
			else
				return new ClosureMethod(method, method.Attributes, nonFixedParameterInfos, genericMethodDefinition, null, fixedArguments.AsRefList());
		}
	}
}
