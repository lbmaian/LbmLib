using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace LbmLib.Language
{
	public static class ChainedRefListExtensions
	{
		public static IBaseRefList<T> ChainConcat<T>(this IBaseRefList<T> left, IBaseRefList<T> right)
		{
			// Don't need to use ChainedListExtensions.IsReadOnly(list) helper function, since arrays can't implement IRefList anyway.
			if (left.IsReadOnly || right.IsReadOnly)
				return left.AsReadOnly().ChainConcat(right.AsReadOnly());
			else
				return left.AsNonReadOnly().ChainConcat(right.AsNonReadOnly());
		}

		public static IRefReadOnlyList<T> ChainConcat<T>(this IRefReadOnlyList<T> left, IRefReadOnlyList<T> right) =>
			new ChainedRefReadOnlyList<T>(left, right);

		public static IRefList<T> ChainConcat<T>(this IRefList<T> left, IRefList<T> right)
		{
			if (ChainedListExtensions.IsFixedSize(left) || ChainedListExtensions.IsFixedSize(right))
				return new ChainedFixedSizeRefList<T>(left, right);
			else
				return new ChainedRefList<T>(left, right);
		}

		public static IRefList<T> ChainConcat<T>(this T[] left, T[] right) =>
			new ChainedFixedSizeRefList<T>(new ArrayRefList<T>(left), new ArrayRefList<T>(right));

		public static IRefReadOnlyList<T> ChainConcat<T>(this IRefReadOnlyList<T> left, T[] right) =>
			new ChainedRefReadOnlyList<T>(left, new ArrayRefReadOnlyList<T>(right));

		public static IRefReadOnlyList<T> ChainConcat<T>(this T[] left, IRefReadOnlyList<T> right) =>
			new ChainedRefReadOnlyList<T>(new ArrayRefReadOnlyList<T>(left), right);

		public static IRefList<T> ChainConcat<T>(this IRefList<T> left, T[] right) =>
			new ChainedFixedSizeRefList<T>(left, new ArrayRefList<T>(right));

		public static IRefList<T> ChainConcat<T>(this T[] left, IRefList<T> right) =>
			new ChainedFixedSizeRefList<T>(new ArrayRefList<T>(left), right);

		public static IBaseRefList<T> ChainConcat<T>(this IBaseRefList<T> left, T[] right)
		{
			if (left.IsReadOnly)
				return left.AsReadOnly().ChainConcat(right);
			else
				return left.AsNonReadOnly().ChainConcat(right);
		}

		public static IBaseRefList<T> ChainConcat<T>(this T[] left, IBaseRefList<T> right)
		{
			if (right.IsReadOnly)
				return left.ChainConcat(right.AsReadOnly());
			else
				return left.ChainConcat(right.AsNonReadOnly());
		}

		public static IBaseRefList<T> ChainAppend<T>(this IBaseRefList<T> list, params T[] itemsToAppend) => list.ChainConcat(itemsToAppend);

		public static IRefList<T> ChainAppend<T>(this IRefList<T> list, params T[] itemsToAppend) => list.ChainConcat(itemsToAppend);

		public static IRefReadOnlyList<T> ChainAppend<T>(this IRefReadOnlyList<T> list, params T[] itemsToAppend) => list.ChainConcat(itemsToAppend);

		public static IBaseRefList<T> ChainAppend<T>(this T[] array, params T[] itemsToAppend) => array.ChainConcat(itemsToAppend);

		public static IBaseRefList<T> ChainPrepend<T>(this IBaseRefList<T> list, params T[] itemsToPrepend) => itemsToPrepend.ChainConcat(list);

		public static IRefList<T> ChainPrepend<T>(this IRefList<T> list, params T[] itemsToPrepend) => itemsToPrepend.ChainConcat(list);

		public static IRefReadOnlyList<T> ChainPrepend<T>(this IRefReadOnlyList<T> list, params T[] itemsToPrepend) => itemsToPrepend.ChainConcat(list);

		public static IBaseRefList<T> ChainPrepend<T>(this T[] array, params T[] itemsToPrepend) => itemsToPrepend.ChainConcat(array);
	}

	abstract class AbstractChainedRefList<T> : BaseChainedList<T, IRefList<T>>, IRefList<T>, IListEx<T>
	{
		struct ChainedRefListEnumerator : IRefListEnumerator<T>
		{
			readonly IRefListEnumerator<T> left;
			readonly IRefListEnumerator<T> right;
			int leftEndIndex;

			internal ChainedRefListEnumerator(IRefListEnumerator<T> left, IRefListEnumerator<T> right)
			{
				this.left = left;
				this.right = right;
				leftEndIndex = -1;
			}

			public ref T Current
			{
				[MethodImpl(256)] // AggressiveInlining
				get => ref (leftEndIndex == -1 ? ref left.Current : ref right.Current);
			}

			public int CurrentIndex
			{
				[MethodImpl(256)] // AggressiveInlining
				get => leftEndIndex == -1 ? left.CurrentIndex : leftEndIndex + right.CurrentIndex;
			}

			T IEnumerator<T>.Current => Current;

			object IEnumerator.Current => Current;

			[MethodImpl(256)] // AggressiveInlining
			public bool MoveNext()
			{
				if (leftEndIndex == -1)
				{
					if (left.MoveNext())
						return true;
					leftEndIndex = left.CurrentIndex;
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
				leftEndIndex = -1;
			}
		}

		protected AbstractChainedRefList(IRefList<T> left, IRefList<T> right) : base(left, right)
		{
		}

		public abstract void AddRange(IEnumerable<T> collection);

		public IRefList<T> AsNonReadOnly() => this;

		public IRefReadOnlyList<T> AsReadOnly() => new ChainedRefReadOnlyList<T>(left.AsReadOnly(), right.AsReadOnly());

		public IRefListEnumerator<T> GetEnumerator() => new ChainedRefListEnumerator(left.GetEnumerator(), right.GetEnumerator());

		public override IListEnumerator<T> GetListEnumerator() => GetEnumerator();

		public ref T ItemRef(int index)
		{
			var leftCount = left.Count;
			if (index < leftCount)
				return ref left.ItemRef(index);
			else
				return ref right.ItemRef(index - leftCount);
		}

		public abstract IRefList<T> GetRangeView(int index, int count);

		public abstract void InsertRange(int index, IEnumerable<T> collection);

		public abstract void RemoveRange(int index, int count);

		IListEx<T> IListWithRangeView<T, IListEx<T>>.GetRangeView(int index, int count) => (IListEx<T>)GetRangeView(index, count);
	}

	sealed class ChainedRefList<T> : AbstractChainedRefList<T>
	{
		internal ChainedRefList(IRefList<T> left, IRefList<T> right) : base(left, right)
		{
		}

		public override bool IsReadOnly => false;

		protected override bool IsFixedSize => false;

		public override void Add(T item) => right.Add(item);

		public override void AddRange(IEnumerable<T> collection) => right.AddRange(collection);

		public override void Clear()
		{
			left.Clear();
			right.Clear();
		}

		public override IRefList<T> GetRangeView(int index, int count)
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
					return new ChainedRefList<T>(left.GetRangeView(index, leftCount - index), right.GetRangeView(0, endIndex - leftCount));
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

		public override void InsertRange(int index, IEnumerable<T> collection)
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

		public override void RemoveRange(int index, int count)
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

	sealed class ChainedFixedSizeRefList<T> : AbstractChainedRefList<T>
	{
		internal ChainedFixedSizeRefList(IRefList<T> left, IRefList<T> right) : base(left, right)
		{
		}

		public override bool IsReadOnly => false;

		protected override bool IsFixedSize => true;

		public override void Add(T item) => throw new NotSupportedException();

		public override void AddRange(IEnumerable<T> collection) => throw new NotSupportedException();

		public override void Clear() => throw new NotSupportedException();

		public override IRefList<T> GetRangeView(int index, int count)
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
					return new ChainedFixedSizeRefList<T>(left.GetRangeView(index, leftCount - index), right.GetRangeView(0, endIndex - leftCount));
			}
			else
				return right.GetRangeView(index - leftCount, count);
		}

		public override void Insert(int index, T item) => throw new NotSupportedException();

		public override void InsertRange(int index, IEnumerable<T> collection) => throw new NotSupportedException();

		public override bool Remove(T item) => throw new NotSupportedException();

		public override void RemoveAt(int index) => throw new NotSupportedException();

		public override void RemoveRange(int index, int count) => throw new NotSupportedException();
	}

	sealed class ChainedRefReadOnlyList<T> : BaseChainedList<T, IRefReadOnlyList<T>>, IRefReadOnlyList<T>, IReadOnlyListEx<T>
	{
		struct ChainedRefReadOnlyListEnumerator : IRefReadOnlyListEnumerator<T>
		{
			readonly IRefReadOnlyListEnumerator<T> left;
			readonly IRefReadOnlyListEnumerator<T> right;
			int leftEndIndex;

			internal ChainedRefReadOnlyListEnumerator(IRefReadOnlyListEnumerator<T> left, IRefReadOnlyListEnumerator<T> right)
			{
				this.left = left;
				this.right = right;
				leftEndIndex = -1;
			}

			public ref readonly T Current
			{
				[MethodImpl(256)] // AggressiveInlining
				get => ref (leftEndIndex == -1 ? ref left.Current : ref right.Current);
			}

			public int CurrentIndex
			{
				[MethodImpl(256)] // AggressiveInlining
				get => leftEndIndex == -1 ? left.CurrentIndex : leftEndIndex + right.CurrentIndex;
			}

			T IEnumerator<T>.Current => Current;

			object IEnumerator.Current => Current;

			[MethodImpl(256)] // AggressiveInlining
			public bool MoveNext()
			{
				if (leftEndIndex == -1)
				{
					if (left.MoveNext())
						return true;
					leftEndIndex = left.CurrentIndex;
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
				leftEndIndex = -1;
			}
		}

		internal ChainedRefReadOnlyList(IRefReadOnlyList<T> left, IRefReadOnlyList<T> right) : base(left, right)
		{
		}

		// As this class isn't public, the indexer set accessor being public here doesn't matter.
		// If this instance is cast as an IRefReadOnlyList<T>, then the indexer set accessor effectively isn't public.
		public override T this[int index]
		{
			get => base[index];
			set => throw new NotSupportedException();
		}

		public override bool IsReadOnly => true;

		protected override bool IsFixedSize => true;

		public IRefList<T> AsNonReadOnly() => throw new NotSupportedException();

		public IRefReadOnlyList<T> AsReadOnly() => this;

		public IRefReadOnlyListEnumerator<T> GetEnumerator() => new ChainedRefReadOnlyListEnumerator(left.GetEnumerator(), right.GetEnumerator());

		public ref readonly T ItemRef(int index)
		{
			var leftCount = left.Count;
			if (index < leftCount)
				return ref left.ItemRef(index);
			else
				return ref right.ItemRef(index - leftCount);
		}

		public override void Add(T item) => throw new NotSupportedException();

		public override void Clear() => throw new NotSupportedException();

		public override void Insert(int index, T item) => throw new NotSupportedException();

		public IRefReadOnlyList<T> GetRangeView(int index, int count)
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
					return new ChainedRefReadOnlyList<T>(left.GetRangeView(index, leftCount - index), right.GetRangeView(0, endIndex - leftCount));
			}
			else
				return right.GetRangeView(index - leftCount, count);
		}

		public override bool Remove(T item) => throw new NotSupportedException();

		public override void RemoveAt(int index) => throw new NotSupportedException();

		IReadOnlyListEx<T> IListWithRangeView<T, IReadOnlyListEx<T>>.GetRangeView(int index, int count) => (IReadOnlyListEx<T>)GetRangeView(index, count);
	}
}
