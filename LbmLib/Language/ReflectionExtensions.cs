using System;
using System.Collections.Generic;

namespace LbmLib.Language
{
	public static class ReflectionExtensions
	{
		public static IEnumerable<Type> GetParentTypes(this Type type, bool includeThisType = false)
		{
			if (type is null)
				yield break;
			if (includeThisType)
				yield return type;
			foreach (var @interface in type.GetInterfaces())
				yield return @interface;
			type = type.BaseType;
			while (!(type is null))
			{
				yield return type;
				type = type.BaseType;
			}
		}
	}
}
