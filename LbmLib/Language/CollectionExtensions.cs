using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LbmLib.Language
{
	public static class CollectionExtensions
	{
		public static List<T> AsList<T>(this IEnumerable<T> enumerable)
		{
			return enumerable as List<T> ?? new List<T>(enumerable);
		}

		public static List<T> AsList<T>(this IEnumerable enumerable)
		{
			return enumerable as List<T> ?? new List<T>(enumerable.Cast<T>());
		}

		// XXX: Needs a better name.
		public static List<T> PopAll<T>(this ICollection<T> collection)
		{
			var collectionCopy = new List<T>(collection);
			collection.Clear();
			return collectionCopy;
		}

		// XXX: Needs a better name.
		public static List<T> PopAll<T>(this ICollection<T> collection, Func<T, bool> match)
		{
			var removedItems = new List<T>();
			collection.RemoveAll(item =>
			{
				if (match(item))
				{
					removedItems.Add(item);
					return true;
				}
				return false;
			});
			return removedItems;
		}

		// XXX: Needs a better name.
		public static List<T> PopRange<T>(this IList<T> list, int index, int count)
		{
			var range = list.GetRange(index, count);
			list.RemoveRange(index, count);
			return range;
		}

		public static List<T> AddDefaultIfEmpty<T>(this List<T> list, Func<T> defaultSupplier)
		{
			if (list.Count == 0)
				list.Add(defaultSupplier());
			return list;
		}

		public static IList<T> AddDefaultIfEmpty<T>(this IList<T> list, Func<T> defaultSupplier)
		{
			if (list.Count == 0)
				list.Add(defaultSupplier());
			return list;
		}

		public static int RemoveAll<K, V>(this Dictionary<K, V> dictionary, Func<K, bool> keyPredicate = null, Func<V, bool> valuePredicate = null,
			Func<KeyValuePair<K, V>, bool> pairPredicate = null)
		{
			int removeCount = 0;
			if (keyPredicate != null)
			{
				foreach (var key in dictionary.Keys.Where(new Func<K, bool>(keyPredicate)).ToList())
				{
					if (dictionary.Remove(key))
						removeCount++;
				}
			}
			if (valuePredicate != null)
			{
				foreach (var key in dictionary.Where(pair => valuePredicate(pair.Value)).Select(pair => pair.Key).ToList())
				{
					if (dictionary.Remove(key))
						removeCount++;
				}
			}
			if (pairPredicate != null)
			{
				foreach (var key in dictionary.Where(new Func<KeyValuePair<K, V>, bool>(pairPredicate)).Select(pair => pair.Key).ToList())
				{
					if (dictionary.Remove(key))
						removeCount++;
				}
			}
			return removeCount;
		}

		public static string Join(this IEnumerable enumerable, string delimiter = ", ")
		{
			var sb = new StringBuilder();
			var first = true;
			foreach (var item in enumerable)
			{
				if (first)
					first = false;
				else
					sb.Append(delimiter);
				sb.Append(item?.ToString() ?? "null");
			}
			return sb.ToString();
		}

		// Not named Reverse, since Enumerable.Reverse<T>(IEnumerable<T>) already exists, and string implements IEnumerable<char>.
		// Also not a generic Unicode Reverse since it doesn't have special handling for Unicode surrogate pairs or Windows line breaks.
		public static string SimpleReverse(this string str)
		{
			var chars = str.ToCharArray();
			Array.Reverse(chars);
			return new string(chars);
		}

		public static int FindIndex<T>(this IList<T> list, params Func<T, bool>[] sequenceMatches)
		{
			return list.FindIndex(0, list.Count, sequenceMatches);
		}

		public static int FindIndex<T>(this IList<T> list, int startIndex, params Func<T, bool>[] sequenceMatches)
		{
			return list.FindIndex(startIndex, list.Count - startIndex, sequenceMatches);
		}

		public static int FindIndex<T>(this IList<T> list, int startIndex, int count, params Func<T, bool>[] sequenceMatches)
		{
			if (sequenceMatches is null)
				throw new ArgumentNullException(nameof(sequenceMatches));
			if (sequenceMatches.Length == 0)
				throw new ArgumentException($"sequenceMatches must not be empty");
			if (count - sequenceMatches.Length < 0)
				return -1;
			count -= sequenceMatches.Length - 1;
			var index = list.FindIndex(startIndex, count, sequenceMatches[0]);
			while (index != -1)
			{
				var allMatched = true;
				for (var matchIndex = 1; matchIndex < sequenceMatches.Length; matchIndex++)
				{
					if (!sequenceMatches[matchIndex](list[index + matchIndex]))
					{
						allMatched = false;
						break;
					}
				}
				if (allMatched)
					break;
				startIndex++;
				count--;
				index = list.FindIndex(startIndex, count, sequenceMatches[0]);
			}
			return index;
		}

		public static int FindLastIndex<T>(this IList<T> list, params Func<T, bool>[] sequenceMatches)
		{
			var listCount = list.Count;
			return list.FindLastIndex(listCount - 1, listCount, sequenceMatches);
		}

		public static int FindLastIndex<T>(this IList<T> list, int startIndex, params Func<T, bool>[] sequenceMatches)
		{
			return list.FindLastIndex(startIndex, startIndex + 1, sequenceMatches);
		}

		public static int FindLastIndex<T>(this IList<T> list, int startIndex, int count, params Func<T, bool>[] sequenceMatches)
		{
			if (sequenceMatches is null)
				throw new ArgumentNullException(nameof(sequenceMatches));
			if (sequenceMatches.Length == 0)
				throw new ArgumentException($"sequenceMatches must not be empty");
			if (count - sequenceMatches.Length < 0)
				return -1;
			var maxSequenceMatchIndex = sequenceMatches.Length - 1;
			startIndex -= maxSequenceMatchIndex;
			count -= maxSequenceMatchIndex;
			var index = list.FindLastIndex(startIndex, count, sequenceMatches[0]);
			while (index != -1)
			{
				var allMatched = true;
				for (var matchIndex = 1; matchIndex < sequenceMatches.Length; matchIndex++)
				{
					if (!sequenceMatches[matchIndex](list[index + matchIndex]))
					{
						allMatched = false;
						break;
					}
				}
				if (allMatched)
					break;
				startIndex--;
				count--;
				index = list.FindLastIndex(startIndex, count, sequenceMatches[0]);
			}
			return index;
		}
	}
}
