using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

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
	abstract class BaseChainedList<T, TList> : IList<T>, IList, IListWithReadOnlyRangeMethods<T>, IListWithListEnumerator<T> where TList : IList<T>
	{
		struct ChainedListEnumerator : IListEnumerator<T>
		{
			readonly IEnumerator<T> left;
			readonly IEnumerator<T> right;
			int index;
			bool useLeft;

			internal ChainedListEnumerator(IEnumerator<T> left, IEnumerator<T> right)
			{
				this.left = left;
				this.right = right;
				index = -1;
				useLeft = true;
			}

			public T Current => useLeft ? left.Current : right.Current;

			public int CurrentIndex => index;

			object IEnumerator.Current => Current;

			[MethodImpl(256)] // AggressiveInlining
			public bool MoveNext()
			{
				index++;
				if (useLeft)
				{
					if (left.MoveNext())
						return true;
					useLeft = false;
				}
				return right.MoveNext();
			}

			public void Dispose()
			{
				left.Dispose();
				right.Dispose();
			}

			void IEnumerator.Reset()
			{
				left.Reset();
				right.Reset();
				index = -1;
				useLeft = true;
			}
		}

		protected readonly TList left;
		protected readonly TList right;

		protected BaseChainedList(TList left, TList right)
		{
			this.left = left;
			this.right = right;
		}

		public virtual T this[int index]
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

		object IList.this[int index]
		{
			get => this[index];
			set => this[index] = (T)value;
		}

		public virtual int Count => left.Count + right.Count;

		public abstract bool IsReadOnly { get; }

		protected abstract bool IsFixedSize { get; }

		bool IList.IsFixedSize => IsFixedSize;

		bool IList.IsReadOnly => IsReadOnly;

		int ICollection.Count => Count;

		// The left and right IRefLists are not managed by BaseChainedList itself, so there's no way to guarantee a locked object can synchronize both.
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

		public void CopyTo(int index, T[] array, int arrayIndex, int count)
		{
			var leftCount = left.Count;
			if (index < leftCount)
			{
				var endIndex = index + count;
				if (endIndex <= leftCount)
				{
					left.CopyTo(index, array, arrayIndex, count);
				}
				else
				{
					var leftRangeCount = leftCount - index;
					left.CopyTo(index, array, arrayIndex, leftRangeCount);
					right.CopyTo(0, array, arrayIndex + leftRangeCount, endIndex - leftCount);
				}
			}
			else
			{
				right.CopyTo(index - leftCount, array, arrayIndex, count);
			}
		}

		public override bool Equals(object obj) =>
			obj is BaseChainedList<T, TList> chainedList && left.Equals(chainedList.left) && right.Equals(chainedList.right);

		public override int GetHashCode()
		{
			var hashCode = -124503083;
			hashCode = hashCode * -1521134295 + left.GetHashCode();
			hashCode = hashCode * -1521134295 + right.GetHashCode();
			return hashCode;
		}

		public virtual IListEnumerator<T> GetListEnumerator() => new ChainedListEnumerator(left.GetEnumerator(), right.GetEnumerator());

		public List<T> GetRange(int index, int count)
		{
			var leftCount = left.Count;
			if (index < leftCount)
			{
				var endIndex = index + count;
				if (endIndex <= leftCount)
				{
					return left.GetRange(index, count);
				}
				else
				{
					var range = new List<T>(count);
					range.AddRange(left.GetRange(index, leftCount - index));
					range.AddRange(right.GetRange(0, endIndex - leftCount));
					return range;
				}
			}
			else
			{
				return right.GetRange(index - leftCount, count);
			}
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

	sealed class ChainedList<T> : BaseChainedList<T, IList<T>>, IListEx<T>
	{
		internal ChainedList(IList<T> left, IList<T> right) : base(left, right)
		{
		}

		public override bool IsReadOnly => false;

		protected override bool IsFixedSize => false;

		public override void Add(T item) => right.Add(item);

		public void AddRange(IEnumerable<T> collection) => right.AddRange(collection);

		public override void Clear()
		{
			left.Clear();
			right.Clear();
		}

		public IListEx<T> GetRangeView(int index, int count)
		{
			var leftCount = left.Count;
			if (index < leftCount)
			{
				var endIndex = index + count;
				if (endIndex <= leftCount)
					return left.GetRangeView(index, count);
				else if (index == 0 && count == leftCount + right.Count)
					return this;
				else
					return new ChainedList<T>(left.GetRangeView(index, leftCount - index), right.GetRangeView(0, endIndex - leftCount));
			}
			else
				return right.GetRangeView(index - leftCount, count);
		}

		public override void Insert(int index, T item)
		{
			var leftCount = left.Count;
			if (index <= leftCount)
				left.Insert(index, item);
			else
				right.Insert(index - leftCount, item);
		}

		public void InsertRange(int index, IEnumerable<T> collection)
		{
			var leftCount = left.Count;
			if (index <= leftCount)
				left.InsertRange(index, collection);
			else
				right.InsertRange(index - leftCount, collection);
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

		public void RemoveRange(int index, int count)
		{
			var leftCount = left.Count;
			if (index < leftCount)
			{
				var endIndex = index + count;
				if (endIndex <= leftCount)
				{
					left.RemoveRange(index, count);
				}
				else
				{
					left.RemoveRange(index, leftCount - index);
					right.RemoveRange(0, endIndex - leftCount);
				}
			}
			else
			{
				right.RemoveRange(index - leftCount, count);
			}
		}
	}

	sealed class ChainedFixedSizeList<T> : BaseChainedList<T, IList<T>>, IListEx<T>
	{
		internal ChainedFixedSizeList(IList<T> left, IList<T> right) : base(left, right)
		{
		}

		public override bool IsReadOnly => false;

		protected override bool IsFixedSize => true;

		public override void Add(T item) => throw new NotSupportedException();

		public void AddRange(IEnumerable<T> collection) => throw new NotSupportedException();

		public override void Clear() => throw new NotSupportedException();

		public IListEx<T> GetRangeView(int index, int count)
		{
			var leftCount = left.Count;
			if (index < leftCount)
			{
				var endIndex = index + count;
				if (endIndex <= leftCount)
					return left.GetRangeView(index, count);
				else if (index == 0 && count == leftCount + right.Count)
					return this;
				else
					return new ChainedFixedSizeList<T>(left.GetRangeView(index, leftCount - index), right.GetRangeView(0, endIndex - leftCount));
			}
			else
				return right.GetRangeView(index - leftCount, count);
		}

		public override void Insert(int index, T item) => throw new NotSupportedException();

		public void InsertRange(int index, IEnumerable<T> collection) => throw new NotSupportedException();

		public override bool Remove(T item) => throw new NotSupportedException();

		public override void RemoveAt(int index) => throw new NotSupportedException();

		public void RemoveRange(int index, int count) => throw new NotSupportedException();
	}

	sealed class ChainedReadOnlyList<T> : BaseChainedList<T, IList<T>>, IReadOnlyListEx<T>
#if !NET35
		, IReadOnlyList<T>
#endif
	{
		internal ChainedReadOnlyList(IList<T> left, IList<T> right) : base(left, right)
		{
		}

		// As this class isn't public, the indexer set accessor being public here doesn't matter.
		// If this instance is cast as an IReadOnlyList<T>, then the indexer set accessor effectively isn't public.
		public override T this[int index]
		{
			get => base[index];
			set => throw new NotSupportedException();
		}

		public override bool IsReadOnly => true;

		protected override bool IsFixedSize => true;

		public override void Add(T item) => throw new NotSupportedException();

		public override void Clear() => throw new NotSupportedException();

		public IReadOnlyListEx<T> GetRangeView(int index, int count)
		{
			var leftCount = left.Count;
			if (index < leftCount)
			{
				var endIndex = index + count;
				if (endIndex <= leftCount)
					return left.AsReadOnly().GetRangeView(index, count);
				else if (index == 0 && count == leftCount + right.Count)
					return this;
				else
					return new ChainedReadOnlyList<T>(left.GetRangeView(index, leftCount - index), right.GetRangeView(0, endIndex - leftCount));
			}
			else
				return right.AsReadOnly().GetRangeView(index - leftCount, count);
		}

		public override void Insert(int index, T item) => throw new NotSupportedException();

		public override bool Remove(T item) => throw new NotSupportedException();

		public override void RemoveAt(int index) => throw new NotSupportedException();
	}
}
