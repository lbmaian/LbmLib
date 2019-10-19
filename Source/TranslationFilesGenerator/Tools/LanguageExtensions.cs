using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TranslationFilesGenerator.Tools
{
	public static class CollectionExtensions
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

		public static string Join<T>(this IEnumerable<T> enumerable, string delimiter = ", ")
		{
			return enumerable.Aggregate("", (string prev, T curr) => prev + (prev.Length != 0 ? delimiter : "") + curr?.ToString() ?? "null");
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

	public static class ListMethodsAsIListOrIEnumerableExtensions
	{
		public static ReadOnlyCollection<T> AsReadOnly<T>(this IList<T> list)
		{
			return new ReadOnlyCollection<T>(list);
		}

		public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
		{
			if (enumerable is List<T> actualList)
			{
				actualList.ForEach(action);
				return;
			}
			foreach (var item in enumerable)
				action(item);
		}

		public static List<T> GetRange<T>(this IList<T> list, int index, int count)
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
			if (collection is null)
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
			if (collection is null)
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

		public static void Reverse<T>(this IList<T> list)
		{
			list.Reverse(0, list.Count);
		}

		public static void Reverse<T>(this IList<T> list, int index, int count)
		{
			if (list is List<T> actualList)
			{
				actualList.Reverse(index, count);
				return;
			}
			// TODO: This could be made more efficient by implementing algorithm in Array.Reverse(array, index, count),
			// although it's probably not worth the effort.
			var listCount = list.Count;
			var array = new T[listCount];
			Array.Reverse(array, index, count);
			for (var i = 0; i < listCount; i++)
				list[i] = array[i];
		}

		public static void Sort<T>(this IList<T> list)
		{
			// MS .Net Framework reference implementation treats null the same as Comparer<T>.Default in List/Array.Sort,
			// but the Mono .Net Framework 3.5 implementation (mscorlib 2.0.0.0) uses a quicksort of comparer is non-null and a comb sort if it's null,
			// and furthermore its List.Sort() implementation passes Comparer<T>.Default rather than null to Array.Sort.
			// Mono has since then adopted the MS .Net open source implementation of List.
			// All this means is that passing Comparer<T>.Default or null can potentially have different behavior.
			// So all the IList.Sort extension method implementations will just delegate to the List/Array.Sort methods as possible,
			// instead of all other IList.Sort extension methods delegating to a single IList.Sort extension, which then delegates to List/Array.Sort.
			if (list is List<T> actualList)
			{
				actualList.Sort();
				return;
			}
			// TODO: This could be made more efficient by implementing algorithm in Array.Sort(array),
			// although it's probably not worth the effort.
			var listCount = list.Count;
			var array = new T[listCount];
			Array.Sort(array);
			for (var i = 0; i < listCount; i++)
				list[i] = array[i];
		}

		public static void Sort<T>(this IList<T> list, IComparer<T> comparer)
		{
			if (list is List<T> actualList)
			{
				actualList.Sort(comparer);
				return;
			}
			// TODO: This could be made more efficient by implementing algorithm in Array.Sort(array),
			// although it's probably not worth the effort.
			var listCount = list.Count;
			var array = new T[listCount];
			Array.Sort(array, comparer);
			for (var i = 0; i < listCount; i++)
				list[i] = array[i];
		}

		public static void Sort<T>(this IList<T> list, int index, int count, IComparer<T> comparer)
		{
			if (list is List<T> actualList)
			{
				actualList.Sort(index, count, comparer);
				return;
			}
			// TODO: This could be made more efficient by implementing algorithm in Array.Sort(array, index, count, comparer),
			// although it's probably not worth the effort.
			var listCount = list.Count;
			var array = new T[listCount];
			Array.Sort(array, index, count, comparer);
			for (var i = 0; i < listCount; i++)
				list[i] = array[i];
		}

		public static void Sort<T>(this IList<T> list, Func<T, T, int> comparison)
		{
			if (list is List<T> actualList)
			{
				actualList.Sort(new Comparison<T>(comparison));
				return;
			}
			// TODO: This could be made more efficient by implementing algorithm in Array.Sort(array, comparison),
			// although it's probably not worth the effort.
			var listCount = list.Count;
			var array = new T[listCount];
			Array.Sort(array, new Comparison<T>(comparison));
			for (var i = 0; i < listCount; i++)
				list[i] = array[i];
		}

		public static void CopyTo<T>(this IList<T> list, T[] array)
		{
			list.CopyTo(array, 0);
		}

		public static void CopyTo<T>(this IList<T> list, int index, T[] array, int arrayIndex, int count)
		{
			if (list is List<T> actualList)
			{
				actualList.CopyTo(index, array, arrayIndex, count);
				return;
			}
			if (list is Array actualArray)
			{
				Array.Copy(actualArray, index, array, arrayIndex, count);
			}
			// Following is based off Mono .NET Framework implementation, since MS .NET Framework one delegates most error checking to an extern Array.Copy.
			var listCount = list.Count;
			var arrayCount = array.Length;
			if (index < 0)
				throw new ArgumentOutOfRangeException($"index ({index}) cannot be < 0");
			if (arrayIndex < 0)
				throw new ArgumentOutOfRangeException($"arrayIndex ({arrayIndex}) cannot be < 0");
			if (count < 0)
				throw new ArgumentOutOfRangeException($"count ({count}) cannot be < 0");
			if (listCount - index < count)
				throw new ArgumentOutOfRangeException($"index ({index}) + count ({count}) cannot be > list.Count ({listCount})");
			if (arrayCount - arrayIndex < count)
				throw new ArgumentOutOfRangeException($"arrayIndex ({arrayIndex}) + count ({count}) cannot be > array.Length ({arrayCount})");
			var endIndexExcl = index + count;
			while (index < endIndexExcl)
			{
				array[arrayIndex] = list[index];
				index++;
				arrayIndex++;
			}
		}

		public static T[] ToArray<T>(this IList<T> list)
		{
			if (list is List<T> actualList)
				return actualList.ToArray();
			var listCount = list.Count;
			var array = new T[listCount];
			for (var index = 0; index < listCount; index++)
				array[index] = list[index];
			return array;
		}

		public static bool Exists<T>(this IEnumerable<T> enumerable, Func<T, bool> match)
		{
			if (enumerable is List<T> actualList)
				return actualList.Exists(new Predicate<T>(match));
			return enumerable.Any(match);
		}

		public static bool TrueForAll<T>(this IEnumerable<T> enumerable, Func<T, bool> match)
		{
			if (enumerable is List<T> actualList)
				return actualList.TrueForAll(new Predicate<T>(match));
			return enumerable.All(match);
		}

		public static int BinarySearch<T>(this IList<T> list, T item)
		{
			return list.BinarySearch(0, list.Count, item, null);
		}

		public static int BinarySearch<T>(this IList<T> list, T item, IComparer<T> comparer)
		{
			return list.BinarySearch(0, list.Count, item, comparer);
		}

		public static int BinarySearch<T>(this IList<T> list, int index, int count, T item, IComparer<T> comparer)
		{
			if (list is List<T> actualList)
				return actualList.BinarySearch(index, count, item, comparer);
			// TODO: This could be made more efficient by implementing algorithm in Array.BinarySearch(array, index, count, item, comparer),
			// although it's probably not worth the effort.
			var listCount = list.Count;
			var array = new T[listCount];
			return Array.BinarySearch(array, index, count, item, comparer);
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
			// Following is based off MS .NET Framework reference implementation.
			var listCount = list.Count;
			if (index > listCount)
				throw new ArgumentOutOfRangeException($"index ({index}) cannot be > list.Count ({listCount})");
			return FindIndexInternal(list, index, listCount - index, EqualsPredicate(item));
		}

		public static int IndexOf<T>(this IList<T> list, T item, int index, int count)
		{
			if (list is List<T> actualList)
				return actualList.IndexOf(item, index, count);
			// Following is based off MS .NET Framework reference implementation.
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
			// Following is based off MS .NET Framework reference implementation.
			var listCount = list.Count;
			if ((listCount == 0 && index != -1) || (uint)index >= (uint)listCount)
				throw new ArgumentOutOfRangeException($"index ({index}) cannot be >= list.Count ({listCount})");
			return FindLastIndexInternal(list, index, index + 1, EqualsPredicate(item));
		}

		public static int LastIndexOf<T>(this IList<T> list, T item, int index, int count)
		{
			if (list is List<T> actualList)
				return actualList.LastIndexOf(item, index, count);
			// Following is based off MS .NET Framework reference implementation.
			var listCount = list.Count;
			if ((listCount == 0 && index != -1) || (uint)index >= (uint)listCount)
				throw new ArgumentOutOfRangeException($"index ({index}) cannot be >= list.Count ({listCount})");
			if (count < 0)
				throw new ArgumentOutOfRangeException($"count ({count}) cannot be < 0");
			if (index - count + 1 < 0)
				throw new ArgumentOutOfRangeException($"index ({index}) + count ({count}) - 1 cannot be < 0");
			return FindLastIndexInternal(list, index, count, EqualsPredicate(item));
		}

		public static int RemoveAll<T>(this IList<T> list, Func<T, bool> match)
		{
			if (list is List<T> actualList)
				return actualList.RemoveAll(new Predicate<T>(match));
			// Following is based off both Mono and MS .NET Framework implementations.
			if (match is null)
				throw new ArgumentNullException(nameof(match));
			var listCount = list.Count;
			var freeIndex = 0;
			while (freeIndex < listCount && !match(list[freeIndex]))
				freeIndex++;
			if (freeIndex >= listCount)
				return 0;
			for (var index = freeIndex + 1; index < listCount; index++)
			{
				if (!match(list[index]))
				{
					list[freeIndex] = list[index];
					freeIndex++;
				}
			}
			for (var index = listCount - 1; index >= freeIndex; index--)
			{
				list.RemoveAt(index);
			}
			return listCount - freeIndex;
		}

		public static int RemoveAll<T>(this ICollection<T> collection, Func<T, bool> match)
		{
			if (collection is IList<T> list)
				return list.RemoveAll(match);
			var items = collection.FindAll(match);
			foreach (var item in items)
				collection.Remove(item);
			return items.Count;
		}

		public static List<T> FindAll<T>(this ICollection<T> collection, Func<T, bool> match)
		{
			if (collection is List<T> actualList)
				return actualList.FindAll(new Predicate<T>(match));
			var items = new List<T>();
			foreach (var item in collection)
			{
				if (match(item))
					items.Add(item);
			}
			return items;
		}

		public static T Find<T>(this IList<T> list, Func<T, bool> match)
		{
			if (list is List<T> actualList)
				return actualList.Find(new Predicate<T>(match));
			var index = list.FindIndex(match);
			return index != -1 ? list[index] : default;
		}

		public static T Find<T>(this ICollection<T> collection, Func<T, bool> match)
		{
			if (collection is IList<T> list)
				return list.Find(match);
			foreach (var item in collection)
			{
				if (match(item))
					return item;
			}
			return default;
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
			if (match is null)
				throw new ArgumentNullException(nameof(match));
			var endIndexExcl = startIndex + count;
			for (var index = startIndex; index < endIndexExcl; index++)
			{
				if (match(list[index]))
					return index;
			}
			return -1;
		}

		public static T FindLast<T>(this IList<T> list, Func<T, bool> match)
		{
			if (list is List<T> actualList)
				return actualList.FindLast(new Predicate<T>(match));
			var index = list.FindLastIndex(match);
			return index != -1 ? list[index] : default;
		}

		public static T FindLast<T>(this ICollection<T> collection, Func<T, bool> match)
		{
			if (collection is IList<T> list)
				return list.FindLast(match);
			var last = default(T);
			foreach (var item in collection)
			{
				if (match(item))
					last = item;
			}
			return last;
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
			// Following is based off MS .NET Framework reference implementation.
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
			if (match is null)
				throw new ArgumentNullException(nameof(match));
			var endIndexExcl = startIndex - count;
			for (var index = startIndex; index > endIndexExcl; index--)
			{
				if (match(list[index]))
					return index;
			}
			return -1;
		}
	}

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
