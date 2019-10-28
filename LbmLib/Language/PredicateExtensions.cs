using System;

namespace LbmLib.Language
{
	public static class PredicateExtensions
	{
		public static Func<T, bool> Negation<T>(this Func<T, bool> predicate)
		{
			return x => !predicate(x);
		}

		public static Func<T, bool> And<T>(this Func<T, bool> predicate1, Func<T, bool> predicate2)
		{
			return x => predicate1(x) && predicate2(x);
		}

		public static Func<T, bool> Or<T>(this Func<T, bool> predicate1, Func<T, bool> predicate2)
		{
			return x => predicate1(x) || predicate2(x);
		}
	}
}
