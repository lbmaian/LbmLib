using System;
using System.Collections.Generic;
using System.Linq;

namespace TranslationFilesGenerator.Tools
{
	public static class LanguageExtensions
	{
		public static List<T> AsList<T>(this IEnumerable<T> enumerable)
		{
			return enumerable as List<T> ?? new List<T>(enumerable);
		}

		// XXX: Needs a better name.
		public static List<T> PopAll<T>(this ICollection<T> collection)
		{
			var collectionCopy = new List<T>(collection);
			collection.Clear();
			return collectionCopy;
		}

		// XXX: Needs a better name.
		public static List<T> PopRange<T>(this List<T> list, int index, int count)
		{
			var range = list.GetRange(index, count);
			list.RemoveRange(index, count);
			return range;
		}

		public static IList<T> PopRange<T>(this IList<T> list, int index, int count)
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

		public static string Join<T>(this IEnumerable<T> enumerable, string delimiter = ", ")
		{
			return enumerable.Aggregate("", (string prev, T curr) => prev + (prev.Length != 0 ? delimiter : "") + curr?.ToString() ?? "null");
		}

		public static IList<T> GetRange<T>(this IList<T> list, int index, int count)
		{
			if (list is List<T> actualList)
				return actualList.GetRange(index, count);
			if (index < 0)
				throw new ArgumentOutOfRangeException($"index ({index}) cannot be < 0");
			if (count < 0)
				throw new ArgumentOutOfRangeException($"count ({count}) cannot be < 0");
			var listCount = list.Count;
			if (index > listCount - count)
				throw new ArgumentOutOfRangeException($"startIndex ({index}) + count ({count}) cannot be > list.Count ({listCount})");
			var range = new List<T>(count);
			var endIndexExcl = index + count;
			while (index < endIndexExcl)
			{
				range.Add(list[index]);
				index++;
			}
			return range;
		}

		public static void InsertRange<T>(this IList<T> list, int index, IEnumerable<T> collection)
		{
			if (list is List<T> actualList)
			{
				actualList.InsertRange(index, collection);
				return;
			}
			if (collection == null)
				throw new ArgumentNullException(nameof(collection));
			if (index < 0)
				throw new ArgumentOutOfRangeException($"index ({index}) cannot be < 0");
			var listCount = list.Count;
			if (index > listCount)
				throw new ArgumentOutOfRangeException($"startIndex ({index}) cannot be > list.Count ({listCount})");
			foreach (var item in collection)
			{
				list.Insert(index, item);
				index++;
			}
		}

		public static void AddRange<T>(this IList<T> list, IEnumerable<T> collection)
		{
			if (list is List<T> actualList)
			{
				actualList.AddRange(collection);
				return;
			}
			if (collection == null)
				throw new ArgumentNullException(nameof(collection));
			foreach (var item in collection)
			{
				list.Add(item);
			}
		}

		public static void RemoveRange<T>(this IList<T> list, int index, int count)
		{
			if (list is List<T> actualList)
			{
				actualList.RemoveRange(index, count);
				return;
			}
			if (index < 0)
				throw new ArgumentOutOfRangeException($"index ({index}) cannot be < 0");
			if (count < 0)
				throw new ArgumentOutOfRangeException($"count ({count}) cannot be < 0");
			var listCount = list.Count;
			if (index > listCount - count)
				throw new ArgumentOutOfRangeException($"startIndex ({index}) + count ({count}) cannot be > list.Count ({listCount})");
			var endIndex = index + count - 1;
			while (endIndex >= index)
			{
				list.RemoveAt(endIndex);
				endIndex--;
			}
		}

		static Func<T, bool> EqualsPredicate<T>(T item)
		{
			var equalityComparer = EqualityComparer<T>.Default;
			return x => equalityComparer.Equals(x, item);
		}

		public static int IndexOf<T>(this IList<T> list, T item)
		{
			if (list is List<T> actualList)
				return actualList.IndexOf(item);
			return FindIndexInternal(list, 0, list.Count, EqualsPredicate(item));
		}

		public static int IndexOf<T>(this IList<T> list, T item, int index)
		{
			if (list is List<T> actualList)
				return actualList.IndexOf(item, index);
			var listCount = list.Count;
			if (index > listCount)
				throw new ArgumentOutOfRangeException($"index ({index}) cannot be > list.Count ({listCount})");
			return FindIndexInternal(list, index, listCount - index, EqualsPredicate(item));
		}

		public static int IndexOf<T>(this IList<T> list, T item, int index, int count)
		{
			if (list is List<T> actualList)
				return actualList.IndexOf(item, index, count);
			var listCount = list.Count;
			if ((uint)index > (uint)listCount)
				throw new ArgumentOutOfRangeException($"index ({index}) cannot be > list.Count ({listCount})");
			if (count < 0)
				throw new ArgumentOutOfRangeException($"count ({count}) cannot be < 0");
			if (index > listCount - count)
				throw new ArgumentOutOfRangeException($"index ({index}) + count ({count}) cannot be > list.Count ({listCount})");
			return FindIndexInternal(list, index, count, EqualsPredicate(item));
		}

		public static int LastIndexOf<T>(this IList<T> list, T item)
		{
			if (list is List<T> actualList)
				return actualList.LastIndexOf(item);
			var listCount = list.Count;
			return FindLastIndexInternal(list, listCount - 1, listCount, EqualsPredicate(item));
		}

		public static int LastIndexOf<T>(this IList<T> list, T item, int index)
		{
			if (list is List<T> actualList)
				return actualList.LastIndexOf(item, index);
			var listCount = list.Count;
			if ((listCount == 0 && index != -1) || (uint)index >= (uint)listCount)
				throw new ArgumentOutOfRangeException($"index ({index}) cannot be >= list.Count ({listCount})");
			return FindLastIndexInternal(list, index, index + 1, EqualsPredicate(item));
		}

		public static int LastIndexOf<T>(this IList<T> list, T item, int index, int count)
		{
			if (list is List<T> actualList)
				return actualList.LastIndexOf(item, index, count);
			var listCount = list.Count;
			if ((listCount == 0 && index != -1) || (uint)index >= (uint)listCount)
				throw new ArgumentOutOfRangeException($"index ({index}) cannot be >= list.Count ({listCount})");
			if (count < 0)
				throw new ArgumentOutOfRangeException($"count ({count}) cannot be < 0");
			if (index - count + 1 < 0)
				throw new ArgumentOutOfRangeException($"index ({index}) + count ({count}) - 1 cannot be < 0");
			return FindLastIndexInternal(list, index, count, EqualsPredicate(item));
		}

		public static T Find<T>(this IList<T> list, Func<T, bool> match)
		{
			if (list is List<T> actualList)
				return actualList.Find(new Predicate<T>(match));
			var index = list.FindIndex(match);
			return index != -1 ? list[index] : default;
		}

		public static T Find<T>(this IList<T> list, int startIndex, Func<T, bool> match)
		{
			var index = list.FindIndex(startIndex, match);
			return index != -1 ? list[index] : default;
		}

		public static T Find<T>(this IList<T> list, int startIndex, int count, Func<T, bool> match)
		{
			var index = list.FindIndex(startIndex, count, match);
			return index != -1 ? list[index] : default;
		}

		public static int FindIndex<T>(this IList<T> list, Func<T, bool> match)
		{
			if (list is List<T> actualList)
				return actualList.FindIndex(new Predicate<T>(match));
			// Following is based off MS .NET Framework reference implementation.
			return FindIndexInternal(list, 0, list.Count, match);
		}

		public static int FindIndex<T>(this IList<T> list, int startIndex, Func<T, bool> match)
		{
			if (list is List<T> actualList)
				return actualList.FindIndex(startIndex, new Predicate<T>(match));
			// Following is based off MS .NET Framework reference implementation.
			var listCount = list.Count;
			if (startIndex > listCount)
				throw new ArgumentOutOfRangeException($"startIndex ({startIndex}) cannot be > list.Count ({listCount})");
			return FindIndexInternal(list, startIndex, listCount - startIndex, match);
		}

		public static int FindIndex<T>(this IList<T> list, int startIndex, int count, Func<T, bool> match)
		{
			if (list is List<T> actualList)
				return actualList.FindIndex(startIndex, count, new Predicate<T>(match));
			// Following is based off MS .NET Framework reference implementation.
			var listCount = list.Count;
			if ((uint)startIndex > (uint)listCount)
				throw new ArgumentOutOfRangeException($"startIndex ({startIndex}) cannot be > list.Count ({listCount})");
			if (count < 0)
				throw new ArgumentOutOfRangeException($"count ({count}) cannot be < 0");
			if (startIndex > listCount - count)
				throw new ArgumentOutOfRangeException($"startIndex ({startIndex}) + count ({count}) cannot be > list.Count ({listCount})");
			return FindIndexInternal(list, startIndex, count, match);
		}

		static int FindIndexInternal<T>(IList<T> list, int startIndex, int count, Func<T, bool> match)
		{
			if (match == null)
				throw new ArgumentNullException(nameof(match));
			var endIndexExcl = startIndex + count;
			for (var index = startIndex; index < endIndexExcl; index++)
			{
				if (match(list[index]))
					return index;
			}
			return -1;
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
			if (sequenceMatches == null)
				throw new ArgumentNullException(nameof(sequenceMatches));
			if (sequenceMatches.Length == 0)
				throw new ArgumentException($"sequenceMatches must not be empty");
			if (count - sequenceMatches.Length < 0)
				throw new ArgumentOutOfRangeException($"count ({count}) - sequenceMatches.Length ({sequenceMatches.Length}) cannot be < 0");
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

		public static T FindLast<T>(this IList<T> list, Func<T, bool> match)
		{
			if (list is List<T> actualList)
				return actualList.FindLast(new Predicate<T>(match));
			var index = list.FindLastIndex(match);
			return index != -1 ? list[index] : default;
		}

		public static T FindLast<T>(this IList<T> list, int startIndex, Func<T, bool> match)
		{
			var index = list.FindLastIndex(startIndex, match);
			return index != -1 ? list[index] : default;
		}

		public static T FindLast<T>(this IList<T> list, int startIndex, int count, Func<T, bool> match)
		{
			var index = list.FindLastIndex(startIndex, count, match);
			return index != -1 ? list[index] : default;
		}

		public static int FindLastIndex<T>(this IList<T> list, Func<T, bool> match)
		{
			if (list is List<T> actualList)
				return actualList.FindLastIndex(new Predicate<T>(match));
			var listCount = list.Count;
			return FindLastIndexInternal(list, listCount - 1, listCount, match);
		}

		public static int FindLastIndex<T>(this IList<T> list, int startIndex, Func<T, bool> match)
		{
			if (list is List<T> actualList)
				return actualList.FindLastIndex(startIndex, new Predicate<T>(match));
			var listCount = list.Count;
			if ((listCount == 0 && startIndex != -1) || (uint)startIndex >= (uint)listCount)
				throw new ArgumentOutOfRangeException($"startIndex ({startIndex}) cannot be >= list.Count ({listCount})");
			return FindLastIndexInternal(list, startIndex, startIndex + 1, match);
		}

		public static int FindLastIndex<T>(this IList<T> list, int startIndex, int count, Func<T, bool> match)
		{
			if (list is List<T> actualList)
				return actualList.FindLastIndex(startIndex, count, new Predicate<T>(match));
			// Following is based off MS .NET Framework reference implementation.
			var listCount = list.Count;
			if ((listCount == 0 && startIndex != -1) || (uint)startIndex >= (uint)listCount)
				throw new ArgumentOutOfRangeException($"startIndex ({startIndex}) cannot be >= list.Count ({listCount})");
			if (count < 0)
				throw new ArgumentOutOfRangeException($"count ({count}) cannot be < 0");
			if (startIndex - count + 1 < 0)
				throw new ArgumentOutOfRangeException($"startIndex ({startIndex}) + count ({count}) - 1 cannot be < 0");
			return FindLastIndexInternal(list, startIndex, count, match);
		}

		static int FindLastIndexInternal<T>(IList<T> list, int startIndex, int count, Func<T, bool> match)
		{
			if (match == null)
				throw new ArgumentNullException(nameof(match));
			var endIndexExcl = startIndex - count;
			for (var index = startIndex; index > endIndexExcl; index--)
			{
				if (match(list[index]))
					return index;
			}
			return -1;
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
			if (sequenceMatches == null)
				throw new ArgumentNullException(nameof(sequenceMatches));
			if (sequenceMatches.Length == 0)
				throw new ArgumentException($"sequenceMatches must not be empty");
			if (count - sequenceMatches.Length < 0)
				throw new ArgumentOutOfRangeException($"count ({count}) - sequenceMatches.Length ({sequenceMatches.Length}) cannot be < 0");
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
