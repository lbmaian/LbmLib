using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace LbmLib.Language
{
	public static class ChainedListExtensions
	{
		[MethodImpl(256)] // AggressiveInlining
		public static IList<T> ChainConcat<T>(this IList<T> left, IList<T> right) => new ChainedList<T>(left, right);

		[MethodImpl(256)] // AggressiveInlining
		public static IList<T> ChainAppend<T>(this IList<T> list, params T[] itemsToAppend) => new ChainedList<T>(list, itemsToAppend);

		[MethodImpl(256)] // AggressiveInlining
		public static IList<T> ChainPrepend<T>(this IList<T> list, params T[] itemsToPrepend) => new ChainedList<T>(itemsToPrepend, list);

		// An IList that chains two ILists together, the first list being the left list, the second list being the right list.
		// Any mutations done to this IList mutates one of the lists, depending on mutation index/item.
		// If either left or right ILists are readonly, then the chained IList is readonly.
		// For the edge case where an item is being inserted at an index in between the two lists, it is inserted at the end of the left list.
		// More than two ILists together fluently into a recursive ChainedList structure, e.g. listA.ChainConcat(listB).ChainConcat(listC).
		// Note: This is NOT thread-safe, even if both lists are thread-safe.
		class ChainedList<T> : IList<T>, IList
		{
			readonly IList<T> left;
			readonly IList<T> right;
			readonly bool isReadOnly;
			readonly bool isFixedSize;

			internal ChainedList(IList<T> left, IList<T> right)
			{
				isReadOnly = IsReadOnly(left) || IsReadOnly(right);
				// Assertion: isReadOnly implies isFixedSize.
				isFixedSize = isReadOnly || IsFixedSize(left) || IsFixedSize(right);
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
					if (isReadOnly)
						throw new NotSupportedException();
					var leftCount = left.Count;
					if (index < leftCount)
						left[index] = value;
					else
						right[index - leftCount] = value;
				}
			}

			object IList.this[int index]
			{
				get => this[index];
				set => this[index] = (T)value;
			}

			public int Count => left.Count + right.Count;

			public bool IsReadOnly => isReadOnly;

			bool IList.IsReadOnly => isReadOnly;

			bool IList.IsFixedSize => isFixedSize;

			int ICollection.Count => Count;

			// The left and right ILists are not managed by ChainedList itself, so there's no way to guarantee a locked object can synchronize both.
			object ICollection.SyncRoot => throw new NotSupportedException();

			bool ICollection.IsSynchronized => false;

			public void Add(T item)
			{
				if (isFixedSize)
					throw new NotSupportedException();
				right.Add(item);
			}

			public void Clear()
			{
				if (isFixedSize)
					throw new NotSupportedException();
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
				if (isFixedSize)
					throw new NotSupportedException();
				var leftCount = left.Count;
				if (index <= leftCount)
					left.Insert(index, item);
				else
					right.Insert(index - leftCount, item);
			}

			public bool Remove(T item)
			{
				if (isFixedSize)
					throw new NotSupportedException();
				if (left.Remove(item))
					return true;
				else
					return right.Remove(item);
			}

			public void RemoveAt(int index)
			{
				if (isFixedSize)
					throw new NotSupportedException();
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

			int IList.Add(object value)
			{
				Add((T)value);
				return Count - 1;
			}

			void IList.Clear() => Clear();

			bool IList.Contains(object value) => Contains((T)value);

			void ICollection.CopyTo(Array array, int index) => CopyTo((T[])array, index);

			IEnumerator IEnumerable.GetEnumerator() => Enumerable.Concat(left, right).GetEnumerator();

			int IList.IndexOf(object value) => IndexOf((T)value);

			void IList.Insert(int index, object value) => Insert(index, (T)value);

			void IList.Remove(object value) => Remove((T)value);

			void IList.RemoveAt(int index) => RemoveAt(index);
		}

		// Workaround for the bullshit where ((IList<T>)array).IsReadOnly is true, yet array.IsReadonly is false.
		internal static bool IsReadOnly<T>(IList<T> list) => list is Array ? false : list.IsReadOnly;

		// If the IList doesn't implement List, assume it doesn't have a fixed size.
		internal static bool IsFixedSize<T>(IList<T> list) => list is IList nonGenericList ? nonGenericList.IsFixedSize : false;
	}
}
