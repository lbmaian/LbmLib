using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace LbmLib.Language
{
	public static class ChainedListExtensions
	{
		public static IList<T> ChainConcat<T>(this IList<T> left, IList<T> right)
		{
			if (left is IBaseRefList<T> leftRefList)
			{
				if (right is IBaseRefList<T> rightRefList)
					return leftRefList.ChainConcat(rightRefList);
				else if (right is T[] rightArray)
					return leftRefList.ChainConcat(rightArray);
			}
			else if (left is T[] leftArray)
			{
				if (right is IBaseRefList<T> rightRefList)
					return leftArray.ChainConcat(rightRefList);
				else if (right is T[] rightArray)
					return leftArray.ChainConcat(rightArray);
			}

			if (IsReadOnly(left) || IsReadOnly(right))
				return new ChainedReadOnlyList<T>(left, right);
			else if (IsFixedSize(left) || IsFixedSize(right))
				return new ChainedFixedSizeList<T>(left, right);
			else
				return new ChainedList<T>(left, right);
		}

		// Workaround for the bullshit where ((IList<T>)array).IsReadOnly is true, yet array.IsReadonly is false.
		internal static bool IsReadOnly<T>(IList<T> list) => list is Array ? false : list.IsReadOnly;

		// If the IList doesn't implement List, assume it doesn't have a fixed size.
		internal static bool IsFixedSize<T>(IList<T> list) => list is IList nonGenericList ? nonGenericList.IsFixedSize : false;
	}

	// An IList that chains two ILists together, the first list being the left list, the second list being the right list.
	// Any mutations done to this IList mutates one of the lists, depending on mutation index/item.
	// If either left or right ILists are readonly, then the chained IList is readonly.
	// For the edge case where an item is being inserted at an index in between the two lists, it is inserted at the end of the left list.
	// More than two ILists together fluently into a recursive ChainedList structure, e.g. listA.ChainConcat(listB).ChainConcat(listC).
	// It technically forms a binary non-self-balancing tree where each node represents an IList, a bit similar to an ImmutableList.
	// Note: This is NOT thread-safe, even if both lists are thread-safe.
	abstract class BaseChainedList<T, L> : IList<T>, IList where L : IList<T>
	{
		private protected readonly L left;
		private protected readonly L right;

		private protected BaseChainedList(L left, L right)
		{
			this.left = left;
			this.right = right;
		}

		public abstract T this[int index] { get; set; }

		private protected T InternalGet(int index)
		{
			var leftCount = left.Count;
			if (index < leftCount)
				return left[index];
			else
				return right[index - leftCount];
		}

		private protected void InternalSet(int index, T value)
		{
			var leftCount = left.Count;
			if (index < leftCount)
				left[index] = value;
			else
				right[index - leftCount] = value;
		}

		object IList.this[int index]
		{
			get => this[index];
			set => this[index] = (T)value;
		}

		public int Count => left.Count + right.Count;

		public abstract bool IsReadOnly { get; }

		private protected abstract bool IsFixedSize { get; }

		bool IList.IsFixedSize => IsFixedSize;

		bool IList.IsReadOnly => IsReadOnly;

		int ICollection.Count => Count;

		// The left and right IRefLists are not managed by ChainedRefList itself, so there's no way to guarantee a locked object can synchronize both.
		object ICollection.SyncRoot => throw new NotSupportedException();

		bool ICollection.IsSynchronized => false;

		public abstract void Add(T item);

		public abstract void Clear();

		public bool Contains(T item) => left.Contains(item) || right.Contains(item);

		public void CopyTo(T[] array, int arrayIndex)
		{
			left.CopyTo(array, arrayIndex);
			right.CopyTo(array, left.Count + arrayIndex);
		}

		public override bool Equals(object obj) => obj is BaseChainedList<T, L> chainedList && left.Equals(chainedList.left) && right.Equals(chainedList.right);

		public override int GetHashCode()
		{
			var hashCode = -124503083;
			hashCode = hashCode * -1521134295 + left.GetHashCode();
			hashCode = hashCode * -1521134295 + right.GetHashCode();
			return hashCode;
		}

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

		public abstract void Insert(int index, T item);

		public abstract bool Remove(T item);

		public abstract void RemoveAt(int index);

		public override string ToString()
		{
			// Dumb heuristic that assumes either:
			// a) Either the left or right lists uses the default object.ToString() implementation,
			//    in which case, it essentially returns object.ToString() for itself.
			// b) The left and right lists returns a comma-delimited string of items with no prefix/suffix delimiter,
			//    and concatenates the two such strings with the same delimiter.
			var leftStr = left.ToString();
			if (leftStr == left.GetType().ToString())
				return GetType().ToString();
			var rightStr = right.ToString();
			if (rightStr == right.GetType().ToString())
				return GetType().ToString();
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

		IEnumerator<T> IEnumerable<T>.GetEnumerator() => Enumerable.Concat(left, right).GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => Enumerable.Concat(left, right).GetEnumerator();

		int IList.IndexOf(object value) => IndexOf((T)value);

		void IList.Insert(int index, object value) => Insert(index, (T)value);

		void IList.Remove(object value) => Remove((T)value);

		void IList.RemoveAt(int index) => RemoveAt(index);
	}

	abstract class AbstractChainedList<T> : BaseChainedList<T, IList<T>>
	{
		private protected AbstractChainedList(IList<T> left, IList<T> right) : base(left, right)
		{
		}
	}

	sealed class ChainedList<T> : AbstractChainedList<T>
	{
		internal ChainedList(IList<T> left, IList<T> right) : base(left, right)
		{
		}

		public override T this[int index]
		{
			get => InternalGet(index);
			set => InternalSet(index, value);
		}

		public override bool IsReadOnly => false;

		private protected override bool IsFixedSize => false;

		public override void Add(T item) => right.Add(item);

		public override void Clear()
		{
			left.Clear();
			right.Clear();
		}

		public override void Insert(int index, T item)
		{
			var leftCount = left.Count;
			if (index <= leftCount)
				left.Insert(index, item);
			else
				right.Insert(index - leftCount, item);
		}

		public override bool Remove(T item)
		{
			if (left.Remove(item))
				return true;
			else
				return right.Remove(item);
		}

		public override void RemoveAt(int index)
		{
			var leftCount = left.Count;
			if (index < leftCount)
				left.RemoveAt(index);
			else
				right.RemoveAt(index - leftCount);
		}
	}

	sealed class ChainedFixedSizeList<T> : AbstractChainedList<T>
	{
		internal ChainedFixedSizeList(IList<T> left, IList<T> right) : base(left, right)
		{
		}

		public override T this[int index]
		{
			get => InternalGet(index);
			set => InternalSet(index, value);
		}

		public override bool IsReadOnly => false;

		private protected override bool IsFixedSize => true;

		public override void Add(T item) => throw new NotSupportedException();

		public override void Clear() => throw new NotSupportedException();

		public override void Insert(int index, T item) => throw new NotSupportedException();

		public override bool Remove(T item) => throw new NotSupportedException();

		public override void RemoveAt(int index) => throw new NotSupportedException();
	}

	sealed class ChainedReadOnlyList<T> : AbstractChainedList<T>
	{
		internal ChainedReadOnlyList(IList<T> left, IList<T> right) : base(left, right)
		{
		}

		public override T this[int index]
		{
			get => InternalGet(index);
			set => throw new NotSupportedException();
		}

		public override bool IsReadOnly => true;

		private protected override bool IsFixedSize => true;

		public override void Add(T item) => throw new NotSupportedException();

		public override void Clear() => throw new NotSupportedException();

		public override void Insert(int index, T item) => throw new NotSupportedException();

		public override bool Remove(T item) => throw new NotSupportedException();

		public override void RemoveAt(int index) => throw new NotSupportedException();
	}
}
