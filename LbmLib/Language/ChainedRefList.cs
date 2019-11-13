using System;
using System.Runtime.CompilerServices;

namespace LbmLib.Language
{
	public static class ChainedRefListExtensions
	{
		public static IRefList<T> ChainConcat<T>(this IRefList<T> left, IRefList<T> right)
		{
			// Don't need to use ChainedListExtensions.IsReadOnly(list) helper function, since the arrays can't implement IRefList anyway.
			if (left.IsReadOnly || right.IsReadOnly)
				return new ChainedReadOnlyRefList<T>(left, right);
			else if (ChainedListExtensions.IsFixedSize(left) || ChainedListExtensions.IsFixedSize(right))
				return new ChainedFixedSizeRefList<T>(left, right);
			else
				return new ChainedRefList<T>(left, right);
		}

		public static IRefList<T> ChainConcat<T>(this T[] left, T[] right) =>
			new ChainedFixedSizeRefList<T>(new ArrayRefList<T>(left), new ArrayRefList<T>(right));

		public static IRefList<T> ChainConcat<T>(this IRefList<T> left, T[] right) =>
			new ChainedFixedSizeRefList<T>(left, new ArrayRefList<T>(right));

		public static IRefList<T> ChainConcat<T>(this T[] left, IRefList<T> right) =>
			new ChainedFixedSizeRefList<T>(new ArrayRefList<T>(left), right);

		public static IRefList<T> ChainAppend<T>(this IRefList<T> list, params T[] itemsToAppend) =>
			new ChainedFixedSizeRefList<T>(list, new ArrayRefList<T>(itemsToAppend));

		public static IRefList<T> ChainAppend<T>(this T[] array, params T[] itemsToAppend) =>
			new ChainedFixedSizeRefList<T>(new ArrayRefList<T>(array), new ArrayRefList<T>(itemsToAppend));

		public static IRefList<T> ChainPrepend<T>(this IRefList<T> list, params T[] itemsToPrepend) =>
			new ChainedFixedSizeRefList<T>(new ArrayRefList<T>(itemsToPrepend), list);

		public static IRefList<T> ChainPrepend<T>(this T[] array, params T[] itemsToPrepend) =>
			new ChainedFixedSizeRefList<T>(new ArrayRefList<T>(itemsToPrepend), new ArrayRefList<T>(array));
	}

	abstract class AbstractChainedRefList<T> : BaseChainedList<T, IRefList<T>>, IRefList<T>
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
		}

		private protected AbstractChainedRefList(IRefList<T> left, IRefList<T> right) : base(left, right)
		{
		}

		public IRefListEnumerator<T> GetEnumerator() => new ChainedRefListEnumerator(left.GetEnumerator(), right.GetEnumerator());

		public ref T ItemRef(int index)
		{
			var leftCount = left.Count;
			if (index < leftCount)
				return ref left.ItemRef(index);
			else
				return ref right.ItemRef(index - leftCount);
		}
	}

	sealed class ChainedRefList<T> : AbstractChainedRefList<T>
	{
		internal ChainedRefList(IRefList<T> left, IRefList<T> right) : base(left, right)
		{
		}

		public override T this[int index]
		{
			get => InternalGet(index);
			set => InternalSet(index, value);
		}

		public override bool IsReadOnly => throw new NotImplementedException();

		private protected override bool IsFixedSize => throw new NotImplementedException();

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

	sealed class ChainedFixedSizeRefList<T> : AbstractChainedRefList<T>
	{
		internal ChainedFixedSizeRefList(IRefList<T> left, IRefList<T> right) : base(left, right)
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

	sealed class ChainedReadOnlyRefList<T> : AbstractChainedRefList<T>
	{
		internal ChainedReadOnlyRefList(IRefList<T> left, IRefList<T> right) : base(left, right)
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
