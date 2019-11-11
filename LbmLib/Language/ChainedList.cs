using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace LbmLib.Language
{
	public static class ChainedListExtensions
	{
		public static IList<T> ChainConcat<T>(this IList<T> left, IList<T> right) => new ChainedList<T>(left, right);

		public static IList<T> ChainAppend<T>(this IList<T> list, params T[] itemsToAppend) => new ChainedList<T>(list, itemsToAppend);

		public static IList<T> ChainPrepend<T>(this IList<T> list, params T[] itemsToPrepend) => new ChainedList<T>(itemsToPrepend, list);

		// An IList that chains two ILists together, the first list being the left list, the second list being the right list.
		// Any mutations done to this IList mutates one of the lists, depending on mutation index/item.
		// If either left or right ILists are readonly, then the chained IList is readonly.
		// For the edge case where an item is being inserted at an index in between the two lists, it is inserted at the end of the left list.
		// More than two ILists together fluently into a recursive ChainedList structure, e.g. listA.ChainConcat(listB).ChainConcat(listC).
		// Note: This is NOT thread-safe, even if both lists are thread-safe.
		class ChainedList<T> : IList<T>
		{
			readonly IList<T> left;
			readonly IList<T> right;

			internal ChainedList(IList<T> left, IList<T> right)
			{
				var leftReadOnly = left.IsReadOnly;
				var rightReadOnly = right.IsReadOnly;
				if (!leftReadOnly && rightReadOnly)
					left = left.AsReadOnly();
				else if (leftReadOnly && !rightReadOnly)
					right = right.AsReadOnly();
				this.left = left;
				this.right = right;
			}

			public T this[int index]
			{
				get
				{
					var leftCount = left.Count;
					if (index < leftCount)
						return left[index];
					else
						return right[index - leftCount];
				}
				set
				{
					var leftCount = left.Count;
					if (index < leftCount)
						left[index] = value;
					else
						right[index - leftCount] = value;
				}
			}

			public int Count => left.Count + right.Count;

			public bool IsReadOnly => left.IsReadOnly;

			public void Add(T item) => right.Add(item);

			public void Clear()
			{
				left.Clear();
				right.Clear();
			}

			public bool Contains(T item) => left.Contains(item) || right.Contains(item);

			public void CopyTo(T[] array, int arrayIndex)
			{
				left.CopyTo(array, arrayIndex);
				right.CopyTo(array, left.Count + arrayIndex);
			}

			public override bool Equals(object obj)
			{
				return obj is ChainedList<T> chainedList &&
					   left.Equals(chainedList.left) &&
					   right.Equals(chainedList.right);
			}

			public IEnumerator<T> GetEnumerator() => Enumerable.Concat(left, right).GetEnumerator();

			public override int GetHashCode() => left.GetHashCode() * -1521134295 + right.GetHashCode();

			public int IndexOf(T item)
			{
				var index = left.IndexOf(item);
				if (index != -1)
					return index;
				index = right.IndexOf(item);
				if (index != -1)
					return left.Count + index;
				return -1;
			}

			public void Insert(int index, T item)
			{
				var leftCount = left.Count;
				if (index <= leftCount)
					left.Insert(index, item);
				else
					right.Insert(index - leftCount, item);
			}

			public bool Remove(T item)
			{
				if (left.Remove(item))
					return true;
				else
					return right.Remove(item);
			}

			public void RemoveAt(int index)
			{
				var leftCount = left.Count;
				if (index < leftCount)
					left.RemoveAt(index);
				else
					right.RemoveAt(index - leftCount);
			}

			public override string ToString()
			{
				// Dumb heuristic that assumes either:
				// a) Either the left or right lists uses the default object.ToString() implementation,
				//    in which case, it essentially returns object.ToString() for itself.
				// b) The left and right lists returns a comma-delimited string of items with no prefix/suffix delimiter,
				//    and concatenates the two such strings with the same delimiter.
				var leftStr = left.ToString();
				if (leftStr == left.GetType().ToString())
					return typeof(ChainedList<T>).ToString();
				var rightStr = right.ToString();
				if (rightStr == right.GetType().ToString())
					return typeof(ChainedList<T>).ToString();
				if (leftStr.Contains(", ") || rightStr.Contains(", "))
					return leftStr + ", " + rightStr;
				else
					return leftStr + "," + rightStr;
			}

			IEnumerator IEnumerable.GetEnumerator() => Enumerable.Concat(left, right).GetEnumerator();
		}
	}
}
