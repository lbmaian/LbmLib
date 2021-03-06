﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LbmLib.Language
{
	// Various extension methods for IEnumerable, ICollection, IList, and IDictionary.
	public static class CollectionExtensions
	{
#if NET35 || NET45
		// Enumerable.Append was added in .NET Framework 4.7.1 and .NET Core 1.0.
		public static IEnumerable<TSource> Append<TSource>(this IEnumerable<TSource> source, TSource element)
		{
			if (source is null)
				throw new ArgumentNullException(nameof(source));
			foreach (var item in source)
				yield return item;
			yield return element;
		}

		// Enumerable.Prepend was added in .NET Framework 4.7.1 and .NET Core 1.0.
		public static IEnumerable<TSource> Prepend<TSource>(this IEnumerable<TSource> source, TSource element)
		{
			if (source is null)
				throw new ArgumentNullException(nameof(source));
			yield return element;
			foreach (var item in source)
				yield return item;
		}
#endif

		public static List<T> AsList<T>(this IEnumerable<T> enumerable) =>
			enumerable as List<T> ?? new List<T>(enumerable);

		public static List<T> AsList<T>(this IEnumerable enumerable) =>
			enumerable as List<T> ?? new List<T>(enumerable.Cast<T>());

		public static HashSet<T> AsHashSet<T>(this IEnumerable<T> enumerable) =>
			enumerable as HashSet<T> ?? new HashSet<T>(enumerable);

		public static HashSet<T> AsHashSet<T>(this IEnumerable enumerable) =>
			enumerable as HashSet<T> ?? new HashSet<T>(enumerable.Cast<T>());

#if !NET35
		public static ISet<T> AsSet<T>(this IEnumerable<T> enumerable) =>
			enumerable as ISet<T> ?? new HashSet<T>(enumerable);

		public static ISet<T> AsSet<T>(this IEnumerable enumerable) =>
			enumerable as ISet<T> ?? new HashSet<T>(enumerable.Cast<T>());
#endif

		public static Dictionary<K, V> ToDictionary<K, V>(this IEnumerable<KeyValuePair<K, V>> pairs) =>
			pairs.ToDictionary(pair => pair.Key, pair => pair.Value);

		public static Dictionary<K, V> AsDictionary<K, V>(this IEnumerable<KeyValuePair<K, V>> pairs) =>
			pairs as Dictionary<K, V> ?? pairs.ToDictionary(pair => pair.Key, pair => pair.Value);

		// TODO: Although convenient, looks awkward - remove in favor of optional argument in IList.GetRange?
		// Essentially IList.GetRange(list, 0, count) without needing to check whether index is out of bounds.
		public static List<T> GetRangeFromStart<T>(this IList<T> list, int count)
		{
			if (list is List<T> actualList)
				return actualList.GetRange(0, count);
			if (count < 0)
				throw new ArgumentOutOfRangeException($"count ({count}) cannot be < 0");
			var listCount = list.Count;
			if (count > listCount)
				throw new ArgumentOutOfRangeException($"count ({count}) cannot be > list.Count ({listCount})");
			var range = new List<T>(count);
			for (var index = 0; index < count; index++)
			{
				range.Add(list[index]);
			}
			return range;
		}

		// TODO: Although convenient, looks awkward - remove in favor of optional argument in IList.GetRange?
		// Essentially IList.GetRange(list, index, list.Count - index) without needing to check whether count is out of bounds.
		public static List<T> GetRangeToEnd<T>(this IList<T> list, int index)
		{
			if (list is List<T> actualList)
				return actualList.GetRange(index, list.Count - index);
			if (index < 0)
				throw new ArgumentOutOfRangeException($"index ({index}) cannot be < 0");
			var listCount = list.Count;
			if (index > listCount)
				throw new ArgumentOutOfRangeException($"index ({index}) cannot be > list.Count ({listCount})");
			var count = listCount - index;
			var range = new List<T>(count);
			var endIndexExcl = index + count;
			while (index < endIndexExcl)
			{
				range.Add(list[index]);
				index++;
			}
			return range;
		}

		// XXX: Needs a better name?
		public static List<T> PopAll<T>(this ICollection<T> collection)
		{
			var collectionCopy = new List<T>(collection);
			collection.Clear();
			return collectionCopy;
		}

		// XXX: Needs a better name?
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

		// XXX: Needs a better name?
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

		public static bool TryAdd<K, V>(this IDictionary<K, V> dictionary, K key, V value)
		{
			if (dictionary is IDictionary idictionary && idictionary.IsSynchronized)
			{
				lock (idictionary.SyncRoot)
					return TryAddInternal(dictionary, key, value);
			}
			else
			{
				return TryAddInternal(dictionary, key, value);
			}
		}

		static bool TryAddInternal<K, V>(IDictionary<K, V> dictionary, K key, V value)
		{
			if (dictionary.ContainsKey(key))
				return false;
			dictionary.Add(key, value);
			return true;
		}

		public static int RemoveAll<K, V>(this IDictionary<K, V> dictionary, Func<K, bool> keyPredicate = null, Func<V, bool> valuePredicate = null,
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
				else if (!(delimiter is null))
					sb.Append(delimiter);
				sb.Append(item?.ToString() ?? "null");
			}
			return sb.ToString();
		}

		public static string Join(this IList list, int startIndex, int count, string delimiter = ", ")
		{
			if (count == 0)
				return "";
			var sb = new StringBuilder();
			sb.Append(list[startIndex]?.ToString() ?? "null");
			var endIndex = startIndex + count;
			for (var index = startIndex + 1; index < endIndex; index++)
			{
				if (!(delimiter is null))
					sb.Append(delimiter);
				sb.Append(list[index]?.ToString() ?? "null");
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

		// TODO: Rename to FindSequenceIndex.
		public static int FindIndex<T>(this IList<T> list, params Func<T, bool>[] sequenceMatches)
		{
			return list.FindIndex(0, list.Count, sequenceMatches);
		}

		// TODO: Rename to FindSequenceIndex.
		public static int FindIndex<T>(this IList<T> list, int startIndex, params Func<T, bool>[] sequenceMatches)
		{
			return list.FindIndex(startIndex, list.Count - startIndex, sequenceMatches);
		}

		// TODO: Rename to FindSequenceIndex.
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

		// TODO: Rename to FindSequenceLastIndex.
		public static int FindLastIndex<T>(this IList<T> list, params Func<T, bool>[] sequenceMatches)
		{
			var listCount = list.Count;
			return list.FindLastIndex(listCount - 1, listCount, sequenceMatches);
		}

		// TODO: Rename to FindSequenceLastIndex.
		public static int FindLastIndex<T>(this IList<T> list, int startIndex, params Func<T, bool>[] sequenceMatches)
		{
			return list.FindLastIndex(startIndex, startIndex + 1, sequenceMatches);
		}

		// TODO: Rename to FindSequenceLastIndex.
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
