using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Theraot.Collections.ThreadSafe;

namespace TranslationFilesGenerator.Tools
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

	public static class ListMethodsAsCollectionsInterfacesExtensions
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

		// If this method was called Reverse, it would typically have overload preference over Enumerable.Reverse, which wouldn't be preferable
		// for IList implementations that don't even support in-place reverses, such as arrays.
		// Furthermore, Enumerable.Reverse has very different semantics with List.Reverse/IList.ReverseInPlace.
		// The former returns a lazy reverse iterating wrapper enumerable while the latter reverses in-place and has void return type.
		public static void ReverseInPlace<T>(this IList<T> list)
		{
			list.ReverseInPlace(0, list.Count);
		}

		public static void ReverseInPlace<T>(this IList<T> list, int index, int count)
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

	public static class StringExtensions
	{
		// More convenient string methods for splitting strings.

		public static string[] SplitStringDelimiter(this string str, string delimiter, StringSplitOptions options = StringSplitOptions.None)
		{
			return str.Split(new[] { delimiter }, options);
		}

		public static string[] SplitStringDelimiter(this string str, string[] delimiters, StringSplitOptions options = StringSplitOptions.None)
		{
			return str.Split(delimiters, options);
		}

		public static string[] SplitKeepStringDelimiter(this string str, string delimiter, int keepDelimiterIndex)
		{
			var leftDelimiter = delimiter.Substring(0, keepDelimiterIndex);
			var rightDelimiter = delimiter.Substring(keepDelimiterIndex);
			var strs = str.SplitStringDelimiter(delimiter);
			var endIndex = strs.Length - 1;
			if (endIndex == 0)
				return strs;
			strs[0] += leftDelimiter;
			for (var index = 1;  index < endIndex; index++)
			{
				strs[index] = rightDelimiter + strs[index] + leftDelimiter;
			}
			strs[endIndex] = rightDelimiter + strs[endIndex];
			return strs;
		}
	}

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

		sealed class MethodHandleEqualityComparer : IEqualityComparer<MethodBase>
		{
			// MS .NET Framework reference implementation has DynamicMethod.MethodHandle always throw an exception.
			// Attempt workaround by calling internal DynamicMethod.GetMethodDescriptor method.
			static readonly Func<MethodBase, RuntimeMethodHandle> GetMethodDescriptorFunc = InitializeGetMethodDescriptorFunc();

			static Func<MethodBase, RuntimeMethodHandle> InitializeGetMethodDescriptorFunc()
			{
				var bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
				var getMethodDescriptorMethod = typeof(DynamicMethod).GetMethod("GetMethodDescriptor", bindingFlags);
				if (getMethodDescriptorMethod is null)
					return null;
				var rtDynamicMethodType = typeof(DynamicMethod).GetNestedType("RTDynamicMethod", bindingFlags);
				var rtDynamicMethodOwnerField = rtDynamicMethodType.GetField("m_owner", bindingFlags);
				//return (Func<MethodBase, RuntimeMethodHandle>)Delegate.CreateDelegate(typeof(Func<MethodBase, RuntimeMethodHandle>), getMethodDescriptorMethod);
				return method => {
					Console.WriteLine(method.GetType().ToDebugString());
					if (method.GetType() == rtDynamicMethodType)
						method = (DynamicMethod)rtDynamicMethodOwnerField.GetValue(method);
					RuntimeMethodHandle handle;
					if (method is DynamicMethod)
						handle = (RuntimeMethodHandle)getMethodDescriptorMethod.Invoke(method, new object[0]);
					else
						handle = method.MethodHandle;
					Console.WriteLine(" => " + handle);
					return handle;
				};
			}

			public bool Equals(MethodBase x, MethodBase y)
			{
				if (x is null)
					return y is null;
				if (y is null)
					return false;
				if (GetMethodDescriptorFunc is null)
					return x.MethodHandle == y.MethodHandle;
				else
					return GetMethodDescriptorFunc(x) == GetMethodDescriptorFunc(y);
			}

			public int GetHashCode(MethodBase obj)
			{
				return obj is null ? 0 : (GetMethodDescriptorFunc is null ? obj.MethodHandle : GetMethodDescriptorFunc(obj)).GetHashCode();
			}
		}

		sealed class PartialApplyClosure
		{
			public readonly object[] FixedNonConstantArguments;

			internal static readonly Dictionary<MethodBase, uint> CountByMethod = new Dictionary<MethodBase, uint>();
			internal static readonly WeakDictionary<MethodBase, PartialApplyClosure> Closures =
				new WeakDictionary<MethodBase, PartialApplyClosure>(/*new MethodHandleEqualityComparer()*/) { AutoRemoveDeadItems = true };
			//internal static readonly Dictionary<MethodBase, PartialApplyClosure> Closures = new Dictionary<MethodBase, PartialApplyClosure>();

			internal static readonly FieldInfo ClosuresField = typeof(PartialApplyClosure).GetField(nameof(Closures), BindingFlags.Static | BindingFlags.NonPublic);
			internal static readonly FieldInfo NonConstantArgumentsField = typeof(PartialApplyClosure).GetField(nameof(FixedNonConstantArguments));

			internal static readonly MethodInfo GetCurrentMethodMethod = typeof(MethodBase).GetMethod(nameof(MethodBase.GetCurrentMethod));
			internal static readonly MethodInfo WeakDictionaryTryGetValueMethod = typeof(WeakDictionary<MethodBase, PartialApplyClosure>).GetMethod(
				nameof(WeakDictionary<MethodBase, PartialApplyClosure>.TryGetValue), new[] { typeof(MethodBase), typeof(PartialApplyClosure).MakeByRefType() });
			//internal static readonly MethodInfo ConditionalWeakTableTryGetValueMethod = typeof(Dictionary<MethodBase, PartialApplyClosure>).GetMethod("TryGetValue");
			internal static readonly ConstructorInfo InvalidOperationExceptionConstructor = typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) });
			internal static readonly MethodInfo ToDebugStringMethod = typeof(DebugExtensions).GetMethod(nameof(DebugExtensions.ToDebugString), new[] { typeof(MethodBase) });
			internal static readonly MethodInfo StringConcatMethod = typeof(string).GetMethod(nameof(string.Concat), new[] { typeof(string), typeof(string) });

			internal PartialApplyClosure(object[] fixedNonConstantArguments)
			{
				FixedNonConstantArguments = fixedNonConstantArguments;
			}

			public override string ToString()
			{
				return "PartialApplyClosure[" + FixedNonConstantArguments.Join() + "]";
			}
		}

		static readonly bool DynamicPartialApplyGenerateDebugDll = false;

		public static MethodInfo DynamicPartialApply(this MethodInfo method, params object[] fixedArguments)
		{
			// Note: partialApplyCount is only used for the generated method name. The name doesn't have to be unique, but it's nice for debugging purposes.
			if (!PartialApplyClosure.CountByMethod.TryGetValue(method, out var partialApplyCount))
				partialApplyCount = 0;
			else if (partialApplyCount > uint.MaxValue)
				partialApplyCount = 0;

			var parameters = method.GetParameters();
			var totalArgumentCount = parameters.Length;
			var fixedArgumentCount = fixedArguments.Length;
			var isStatic = method.IsStatic;
			var declaringType = method.DeclaringType;
			var returnType = method.ReturnType;

			// Since DynamicMethod can only be created as a static method, if instance method, prepend an additional non-fixed argument.
			var prefixArgumentCount = isStatic ? 0 : 1;
			totalArgumentCount += prefixArgumentCount;
			var nonFixedArgumentCount = totalArgumentCount - fixedArgumentCount;
			var nonFixedParameterTypes = new Type[nonFixedArgumentCount];

			for (var index = 0; index < fixedArgumentCount; index++)
			{
				var parameterType = parameters[index].ParameterType;
				if (parameterType.IsByRef)
					throw new ArgumentException("Cannot partial apply with a fixed argument passed by reference (including in and out arguments): " + parameters[index]);
			}
			// See above - need to prepend an additional non-fixed argument for instance methods.
			if (!isStatic)
				nonFixedParameterTypes[0] = declaringType;
			for (var index = prefixArgumentCount; index < nonFixedArgumentCount; index++)
				nonFixedParameterTypes[index] = parameters[fixedArgumentCount + index].ParameterType;

			// Annoyingly, although both DynamicMethod and MethodBuilder implement MethodInfo and have both GetILGenerator and DefineParameter methods,
			// there isn't an interface for those methods!
			MethodInfo newMethod;
			ILGenerator ilGenerator;
			AssemblyBuilder assemblyBuilder = null;
			TypeBuilder typeBuilder = null;
			if (DynamicPartialApplyGenerateDebugDll)
			{
				// This is based off Harmony.DynamicTools.CreateSaveableMethod.
				var assemblyName = new AssemblyName("DebugAssembly");
				var path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
				assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave, path);
				var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name, assemblyName.Name + ".dll");
				typeBuilder = moduleBuilder.DefineType("Debug" + declaringType.Name, TypeAttributes.Public);
				var methodBuilder = typeBuilder.DefineMethod(
					method.Name + "_PartialApply_" + partialApplyCount,
					MethodAttributes.Public | MethodAttributes.Static,
					CallingConventions.Standard,
					returnType,
					nonFixedParameterTypes);
				if (!isStatic)
					methodBuilder.DefineParameter(0, ParameterAttributes.None, "instance");
				for (int index = prefixArgumentCount; index < nonFixedArgumentCount; index++)
				{
					var parameter = parameters[fixedArgumentCount + index];
					// DefineParameter(0,...) refers to the return value, so for actual parameters, it's practically 1-based rather than 0-based.
					methodBuilder.DefineParameter(index + 1, parameter.Attributes, parameter.Name);
				}
				newMethod = methodBuilder;
				ilGenerator = methodBuilder.GetILGenerator();
			}
			else
			{
				var dynamicMethod = new DynamicMethod(
					method.Name + "_PartialApply_" + partialApplyCount,
					// At least for legacy Unity, only the following MethodAttributes/CallingConventions are supported for DynamicMethod.
					MethodAttributes.Public | MethodAttributes.Static,
					CallingConventions.Standard,
					returnType,
					nonFixedParameterTypes,
					declaringType,
					skipVisibility: true);
				if (!isStatic)
					dynamicMethod.DefineParameter(0, ParameterAttributes.None, "instance");
				for (int index = prefixArgumentCount; index < nonFixedArgumentCount; index++)
				{
					var parameter = parameters[fixedArgumentCount + index];
					// DefineParameter(0,...) refers to the return value, so for actual parameters, it's practically 1-based rather than 0-based.
					dynamicMethod.DefineParameter(index + 1, parameter.Attributes, parameter.Name);
					// XXX: Do any custom attributes like ParamArrayAttribute need to be copied too?
					// There's no good generic way to copy attributes as far as I can tell, since CustomAttributeBuilder is very cumbersome to use.
				}
				newMethod = dynamicMethod;
				ilGenerator = dynamicMethod.GetILGenerator();
			}

			var closure = default(PartialApplyClosure);
			var fixedNonConstantArgumentsVar = default(LocalBuilder);
			var fixedNonConstantArguments = fixedArguments.Where(CanEmitConstant);
			if (fixedNonConstantArguments.Any())
			{
				// Create the closure (will be registered with PartialApplyClosure.Closures with dynamicMethod at the very end,
				// since it's possible that an exception could be thrown throughout this PartialApply method).
				closure = new PartialApplyClosure(fixedNonConstantArguments.ToArray());

				// Emit the code that loads the closure from PartialApplyClosure.ClosuresField, and then its FixedNonConstantArguments into a local variable.
				var closureVar = ilGenerator.DeclareLocal(typeof(PartialApplyClosure));
				fixedNonConstantArgumentsVar = ilGenerator.DeclareLocal(typeof(object[]));
				ilGenerator.Emit(OpCodes.Ldsfld, PartialApplyClosure.ClosuresField);
				ilGenerator.Emit(OpCodes.Call, PartialApplyClosure.GetCurrentMethodMethod);
				ilGenerator.EmitLdloca(closureVar);
				ilGenerator.Emit(OpCodes.Call, PartialApplyClosure.WeakDictionaryTryGetValueMethod);
				var foundClosureLabel = ilGenerator.DefineLabel();
				ilGenerator.Emit(OpCodes.Brtrue_S, foundClosureLabel); // throw away returned bool
				ilGenerator.Emit(OpCodes.Ldstr, "Unexpectedly did not find closure object for ");
				ilGenerator.Emit(OpCodes.Call, PartialApplyClosure.GetCurrentMethodMethod);
				ilGenerator.Emit(OpCodes.Call, PartialApplyClosure.ToDebugStringMethod);
				ilGenerator.Emit(OpCodes.Call, PartialApplyClosure.StringConcatMethod);
				ilGenerator.Emit(OpCodes.Newobj, PartialApplyClosure.InvalidOperationExceptionConstructor);
				ilGenerator.Emit(OpCodes.Throw);
				ilGenerator.MarkLabel(foundClosureLabel);
				ilGenerator.EmitLdloc(closureVar);
				ilGenerator.Emit(OpCodes.Ldfld, PartialApplyClosure.NonConstantArgumentsField);
				ilGenerator.EmitStloc(fixedNonConstantArgumentsVar);
			}

			if (!isStatic)
				ilGenerator.Emit(OpCodes.Ldarg_0);

			var fixedNonConstantArgumentIndex = 0;
			for (var index = 0;  index < fixedArgumentCount; index++)
			{
				var fixedArgument = fixedArguments[index];
				if (!ilGenerator.TryEmitConstant(fixedArgument))
				{
					// Need the closure at this point to obtain the fixed non-constant arguments.
					var fixedArgumentType = fixedArgument.GetType();
					ilGenerator.EmitLdloc(fixedNonConstantArgumentsVar);
					ilGenerator.EmitLdcI4(fixedNonConstantArgumentIndex);
					// PartialApplyClosure.NonConstantArgumentsField is an array of objects, so each element of it needs to be accessed by reference,
					// and if argument is supposed to be a value type, unbox it.
					ilGenerator.Emit(OpCodes.Ldelem_Ref);
					if (fixedArgumentType.IsValueType)
						ilGenerator.Emit(OpCodes.Unbox_Any, fixedArgumentType);
					fixedNonConstantArgumentIndex++;
				}
			}

			for (var index = (short)0; index < nonFixedArgumentCount; index++)
			{
				ilGenerator.EmitLdarg(index);
			}

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

			if (returnType != typeof(void) && returnType.IsValueType)
			{
				ilGenerator.Emit(OpCodes.Box, returnType);
			}
			ilGenerator.Emit(OpCodes.Ret);

			if (DynamicPartialApplyGenerateDebugDll)
			{
				typeBuilder.CreateType();
				assemblyBuilder.Save("DebugAssembly.dll");
				// MethodBuilder doesn't have a RuntimeMethodHandle, so get the built method from typeBuilder.
				method = typeBuilder.GetMethod(method.Name, nonFixedParameterTypes);
			}
			else
			{
				// TODO: Should this dependency on Harmony here be removed?
				// Doesn't matter as long as both this file and Harmony-related files remain in the same project/library.
				//Harmony.DynamicTools.PrepareDynamicMethod(newMethod as DynamicMethod);
			}

			var nonFixedParameterAndReturnTypes = new Type[nonFixedArgumentCount + 1];
			Array.Copy(nonFixedParameterTypes, nonFixedParameterAndReturnTypes, nonFixedArgumentCount);
			nonFixedParameterAndReturnTypes[nonFixedArgumentCount] = returnType;
			var delegateType = ExpressionEx.GetDelegateType(nonFixedParameterAndReturnTypes);
			newMethod = newMethod.CreateDelegate(delegateType).Method;

			PartialApplyClosure.CountByMethod.Add(method, partialApplyCount + 1);
			if (!(closure is null))
			{
				PartialApplyClosure.Closures.AddNew(newMethod, closure);
				Console.WriteLine($"Added closure for {newMethod.ToDebugString()}: {PartialApplyClosure.Closures.TryGetValue(newMethod, out var checkClosure)} => {checkClosure}");
			}
			return newMethod;
		}

		// TODO: Following should be public extension methods somewhere?

		static bool CanEmitConstant(object argument)
		{
			if (argument is null)
				return true;
			switch (Type.GetTypeCode(argument.GetType()))
			{
			case TypeCode.Boolean:
			case TypeCode.SByte:
			case TypeCode.Int16:
			case TypeCode.Int32:
			case TypeCode.Char:
			case TypeCode.Byte:
			case TypeCode.UInt16:
			case TypeCode.UInt32:
			case TypeCode.Int64:
			case TypeCode.UInt64:
			case TypeCode.Single:
			case TypeCode.Double:
			case TypeCode.String:
				return true;
			}
			return false;
		}

		static bool TryEmitConstant(this ILGenerator ilGenerator, object argument)
		{
			if (argument is null)
			{
				ilGenerator.Emit(OpCodes.Ldnull);
				return true;
			}
			switch (Type.GetTypeCode(argument.GetType()))
			{
			case TypeCode.Boolean:
				ilGenerator.Emit((bool)argument ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
				return true;
			// Apparently don't need to handle signed and unsigned integer types of size 4 bytes or less differently from each other.
			case TypeCode.SByte:
			case TypeCode.Int16:
			case TypeCode.Int32:
			case TypeCode.Char:
			case TypeCode.Byte:
			case TypeCode.UInt16:
			case TypeCode.UInt32:
				ilGenerator.EmitLdcI4((int)argument);
				return true;
			// Likewise, ulong and long don't have to be treated differently, but if ldc.i4* is used, needs a conv.i8 afterwards.
			case TypeCode.Int64:
			case TypeCode.UInt64:
				var longArgument = (long)argument;
				if (longArgument >= int.MinValue && longArgument <= int.MaxValue)
				{
					ilGenerator.EmitLdcI4((int)longArgument);
					ilGenerator.Emit(OpCodes.Conv_I8);
				}
				else
				{
					ilGenerator.Emit(OpCodes.Ldc_I8, longArgument);
				}
				return true;
			case TypeCode.Single:
				ilGenerator.Emit(OpCodes.Ldc_R4, (float)argument);
				return true;
			case TypeCode.Double:
				ilGenerator.Emit(OpCodes.Ldc_R8, (double)argument);
				return true;
			case TypeCode.String:
				ilGenerator.Emit(OpCodes.Ldstr, (string)argument);
				return true;
			}
			return false;
		}

		static void EmitLdcI4(this ILGenerator ilGenerator, int value)
		{
			switch (value)
			{
			case -1:
				ilGenerator.Emit(OpCodes.Ldc_I4_M1);
				break;
			case 0:
				ilGenerator.Emit(OpCodes.Ldc_I4_0);
				break;
			case 1:
				ilGenerator.Emit(OpCodes.Ldc_I4_1);
				break;
			case 2:
				ilGenerator.Emit(OpCodes.Ldc_I4_2);
				break;
			case 3:
				ilGenerator.Emit(OpCodes.Ldc_I4_3);
				break;
			case 4:
				ilGenerator.Emit(OpCodes.Ldc_I4_4);
				break;
			case 5:
				ilGenerator.Emit(OpCodes.Ldc_I4_5);
				break;
			case 6:
				ilGenerator.Emit(OpCodes.Ldc_I4_6);
				break;
			case 7:
				ilGenerator.Emit(OpCodes.Ldc_I4_7);
				break;
			case 8:
				ilGenerator.Emit(OpCodes.Ldc_I4_8);
				break;
			default:
				if (value >= -sbyte.MinValue && value <= sbyte.MaxValue)
					ilGenerator.Emit(OpCodes.Ldc_I4_S, (sbyte)value);
				else
					ilGenerator.Emit(OpCodes.Ldc_I4, value);
				break;
			}
		}

		static void EmitLdloc(this ILGenerator ilGenerator, LocalBuilder localVar)
		{
			var localIndex = localVar.LocalIndex;
			switch (localIndex)
			{
			case 0:
				ilGenerator.Emit(OpCodes.Ldloc_0);
				break;
			case 1:
				ilGenerator.Emit(OpCodes.Ldloc_1);
				break;
			case 2:
				ilGenerator.Emit(OpCodes.Ldloc_2);
				break;
			case 3:
				ilGenerator.Emit(OpCodes.Ldloc_3);
				break;
			default:
				if (localIndex <= byte.MaxValue)
					ilGenerator.Emit(OpCodes.Ldloc_S, localVar);
				else
					ilGenerator.Emit(OpCodes.Ldloc, localVar);
				break;
			}
		}

		static void EmitLdloca(this ILGenerator ilGenerator, LocalBuilder localVar)
		{
			if (localVar.LocalIndex <= byte.MaxValue)
				ilGenerator.Emit(OpCodes.Ldloca_S, localVar);
			else
				ilGenerator.Emit(OpCodes.Ldloca, localVar);
		}

		static void EmitStloc(this ILGenerator ilGenerator, LocalBuilder localVar)
		{
			var localIndex = localVar.LocalIndex;
			switch (localIndex)
			{
			case 0:
				ilGenerator.Emit(OpCodes.Stloc_0);
				break;
			case 1:
				ilGenerator.Emit(OpCodes.Stloc_1);
				break;
			case 2:
				ilGenerator.Emit(OpCodes.Stloc_2);
				break;
			case 3:
				ilGenerator.Emit(OpCodes.Stloc_3);
				break;
			default:
				if (localIndex <= byte.MaxValue)
					ilGenerator.Emit(OpCodes.Stloc_S, localVar);
				else
					ilGenerator.Emit(OpCodes.Stloc, localVar);
				break;
			}
		}

		static void EmitLdarg(this ILGenerator ilGenerator, short index)
		{
			switch (index)
			{
			case 0:
				ilGenerator.Emit(OpCodes.Ldarg_0);
				break;
			case 1:
				ilGenerator.Emit(OpCodes.Ldarg_1);
				break;
			case 2:
				ilGenerator.Emit(OpCodes.Ldarg_1);
				break;
			case 3:
				ilGenerator.Emit(OpCodes.Ldarg_1);
				break;
			default:
				if (index <= byte.MaxValue)
					ilGenerator.Emit(OpCodes.Ldarg_S, (byte)index);
				else
					ilGenerator.Emit(OpCodes.Ldarg, index);
				break;
			}
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
