using System;
using System.Collections;
using System.Collections.Generic;

namespace LbmLib.Language
{
	public static class ArrayRefListExtensions
	{
		public static IRefList<T> AsRefList<T>(this T[] array) => new ArrayRefList<T>(array);

		public static IRefReadOnlyList<T> AsRefReadOnlyList<T>(this T[] array) => new ArrayRefReadOnlyList<T>(array);
	}

	// Implementation note: Freely using virtual (and non-inlinable) methods in the classes here,
	// since all these will be accessed by interface, which necessitates virtual calling anyway.

	abstract class AbstractArrayRefList<T> : IBaseRefList<T>
	{
		protected readonly T[] array;

		protected AbstractArrayRefList(T[] array)
		{
			this.array = array;
		}

		public virtual T this[int index]
		{
			get => array[index];
			set => array[index] = value;
		}

		object IList.this[int index]
		{
			get => this[index];
			set => this[index] = (T)value;
		}

		public virtual int Count => array.Length;

		public abstract bool IsReadOnly { get; }

		bool IList.IsReadOnly => IsReadOnly;

		bool IList.IsFixedSize => true;

		int ICollection.Count => Count;

		object ICollection.SyncRoot => array.SyncRoot;

		bool ICollection.IsSynchronized => false;

		public void Add(T item) => throw new NotSupportedException();

		public abstract IRefList<T> AsNonReadOnly();

		public abstract IRefReadOnlyList<T> AsReadOnly();

		protected void CheckValidRange(int index, int count)
		{
			// For some reason, new T[count] where count is negative produces an OverflowException.
			if (count < 0)
				throw new OverflowException($"count ({count}) cannot be < 0");
			if (index < 0)
				throw new IndexOutOfRangeException($"index ({index}) was outside the bounds of the array.");
			if (index + count > Count)
				throw new IndexOutOfRangeException($"index ({index}) + count ({count}) was outside the bounds of the array.");
		}

		public void Clear() => throw new NotSupportedException();

		public virtual bool Contains(T item) => array.Contains(item);

		public virtual void CopyTo(T[] array, int arrayIndex) => Array.Copy(this.array, 0, array, arrayIndex, this.array.Length);

		// This doesn't need to call CheckValidRange, since checks are implicit in Array.Copy.
		public virtual void CopyTo(int index, T[] array, int arrayIndex, int count) => Array.Copy(this.array, index, array, arrayIndex, count);

		public override bool Equals(object obj) => obj is AbstractArrayRefList<T> refList && array.Equals(refList.array);

		public override int GetHashCode() => 276365737 + array.GetHashCode();

		public virtual List<T> GetRange(int index, int count)
		{
			// This doesn't need to call CheckValidRange, since checks are implicit in Array.Copy.
			var range = new T[count];
			Array.Copy(array, index, range, 0, count);
			// List constructor itself copies the range array, unfortunately.
			return new List<T>(range);
		}

		public virtual int IndexOf(T item) => array.IndexOf(item);

		public void Insert(int index, T item) => throw new NotSupportedException();

		public bool Remove(T item) => throw new NotSupportedException();

		public void RemoveAt(int index) => throw new NotSupportedException();

		int IList.Add(object value) => throw new NotSupportedException();

		void IList.Clear() => throw new NotSupportedException();

		bool IList.Contains(object value) => Contains((T)value);

		void ICollection.CopyTo(Array array, int index) => CopyTo((T[])array, index);

		IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)array).GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => array.GetEnumerator();

		int IList.IndexOf(object value) => IndexOf((T)value);

		void IList.Insert(int index, object value) => throw new NotSupportedException();

		void IList.Remove(object value) => throw new NotSupportedException();

		void IList.RemoveAt(int index) => throw new NotSupportedException();
	}

	abstract class AbstractArrayRefListView<T> : AbstractArrayRefList<T>
	{
		protected readonly int indexOffset;
		protected readonly int count;

		internal AbstractArrayRefListView(T[] array, int indexOffset, int count) : base(array)
		{
			this.indexOffset = indexOffset;
			this.count = count;
		}

		public override T this[int index]
		{
			get
			{
				CheckValidIndex(index);
				return array[indexOffset + index];
			}
			set
			{
				CheckValidIndex(index);
				array[indexOffset + index] = value;
			}
		}

		public override int Count => count;

		protected void CheckValidIndex(int index)
		{
			if (index < 0)
				throw new IndexOutOfRangeException($"index ({index}) was outside the bounds of the array.");
			if (index >= count)
				throw new IndexOutOfRangeException($"index ({index}) was outside the bounds of the array.");
		}

		public override bool Contains(T item) => Array.IndexOf(array, item, indexOffset, count) >= array.GetLowerBound(0);

		public override void CopyTo(T[] array, int arrayIndex) => Array.Copy(this.array, indexOffset, array, arrayIndex, count);

		public override void CopyTo(int index, T[] array, int arrayIndex, int count)
		{
			CheckValidRange(index, count);
			Array.Copy(this.array, indexOffset + index, array, arrayIndex, count);
		}

		public override int GetHashCode()
		{
			var hashCode = -338248414;
			hashCode = hashCode * -1521134295 + array.GetHashCode();
			hashCode = hashCode * -1521134295 + indexOffset.GetHashCode();
			hashCode = hashCode * -1521134295 + count.GetHashCode();
			return hashCode;
		}

		public override List<T> GetRange(int index, int count)
		{
			CheckValidRange(index, count);
			var range = new T[count];
			Array.Copy(array, indexOffset + index, range, 0, count);
			// List constructor itself copies the range array, unfortunately.
			return new List<T>(range);
		}

		public override bool Equals(object obj)
		{
			return obj is AbstractArrayRefListView<T> refListView &&
				array.Equals(refListView.array) &&
				indexOffset == refListView.indexOffset &&
				count == refListView.count;
		}

		public override int IndexOf(T item) => Array.IndexOf(array, item, indexOffset, count);

		public override string ToString() => array.Join(indexOffset, count);
	}

	struct ArrayRefListEnumerator<T> : IRefListEnumerator<T>
	{
		readonly T[] array;
		readonly int startIndex;
		readonly int endIndex;
		int index;

		internal ArrayRefListEnumerator(T[] array, int startIndex, int endIndex)
		{
			this.array = array;
			this.startIndex = startIndex;
			this.endIndex = endIndex;
			index = startIndex - 1;
		}

		public ref T Current => ref array[index];

		public int CurrentIndex => index - startIndex;

		T IEnumerator<T>.Current => Current;

		object IEnumerator.Current => Current;

		public bool MoveNext() => ++index < endIndex;

		public void Dispose()
		{
		}

		void IEnumerator.Reset() => index = startIndex - 1;
	}

	sealed class ArrayRefList<T> : AbstractArrayRefList<T>, IRefList<T>, IListEx<T>
	{
		internal ArrayRefList(T[] array) : base(array)
		{
		}

		public override bool IsReadOnly => false;

		public void AddRange(IEnumerable<T> collection) => throw new NotSupportedException();

		public override IRefList<T> AsNonReadOnly() => this;

		public override IRefReadOnlyList<T> AsReadOnly() => new ArrayRefReadOnlyList<T>(array);

		public IRefListEnumerator<T> GetEnumerator() => new ArrayRefListEnumerator<T>(array, 0, array.Length);

		public IListEnumerator<T> GetListEnumerator() => GetEnumerator();

		public IRefList<T> GetRangeView(int index, int count)
		{
			if (index == 0 && count == Count)
				return this;
			CheckValidRange(index, count);
			return new ArrayRefListView<T>(array, index, count);
		}

		public void InsertRange(int index, IEnumerable<T> collection) => throw new NotSupportedException();

		public ref T ItemRef(int index) => ref array[index];

		public void RemoveRange(int index, int count) => throw new NotSupportedException();

		IListEx<T> IListWithRangeView<T, IListEx<T>>.GetRangeView(int index, int count) => (IListEx<T>)GetRangeView(index, count);
	}

	sealed class ArrayRefListView<T> : AbstractArrayRefListView<T>, IRefList<T>, IListEx<T>
	{
		internal ArrayRefListView(T[] array, int indexOffset, int count) : base(array, indexOffset, count)
		{
		}

		public override bool IsReadOnly => false;

		public void AddRange(IEnumerable<T> collection) => throw new NotSupportedException();

		public override IRefList<T> AsNonReadOnly() => this;

		public override IRefReadOnlyList<T> AsReadOnly() => new ArrayRefReadOnlyListView<T>(array, indexOffset, count);

		public IRefListEnumerator<T> GetEnumerator() => new ArrayRefListEnumerator<T>(array, indexOffset, indexOffset + count);

		public IListEnumerator<T> GetListEnumerator() => GetEnumerator();

		public IRefList<T> GetRangeView(int index, int count)
		{
			if (index == 0 && count == Count)
				return this;
			CheckValidRange(index, count);
			return new ArrayRefListView<T>(array, indexOffset + index, count);
		}

		public void InsertRange(int index, IEnumerable<T> collection) => throw new NotSupportedException();

		public ref T ItemRef(int index)
		{
			CheckValidIndex(index);
			return ref array[indexOffset + index];
		}

		public void RemoveRange(int index, int count) => throw new NotSupportedException();

		IListEx<T> IListWithRangeView<T, IListEx<T>>.GetRangeView(int index, int count) => (IListEx<T>)GetRangeView(index, count);
	}

	struct ArrayRefReadOnlyListEnumerator<T> : IRefReadOnlyListEnumerator<T>
	{
		readonly T[] array;
		readonly int startIndex;
		readonly int endIndex;
		int index;

		internal ArrayRefReadOnlyListEnumerator(T[] array, int startIndex, int endIndex)
		{
			this.array = array;
			this.startIndex = startIndex;
			this.endIndex = endIndex;
			index = startIndex - 1;
		}

		public ref readonly T Current => ref array[index];

		public int CurrentIndex => index - startIndex;

		T IEnumerator<T>.Current => Current;

		object IEnumerator.Current => Current;

		public bool MoveNext() => ++index < endIndex;

		public void Dispose()
		{
		}

		void IEnumerator.Reset() => index = startIndex - 1;
	}

	sealed class ArrayRefReadOnlyList<T> : AbstractArrayRefList<T>, IRefReadOnlyList<T>, IReadOnlyListEx<T>
	{
		internal ArrayRefReadOnlyList(T[] array) : base(array)
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

		public override IRefList<T> AsNonReadOnly() => throw new NotSupportedException();

		public override IRefReadOnlyList<T> AsReadOnly() => this;

		public IRefReadOnlyListEnumerator<T> GetEnumerator() => new ArrayRefReadOnlyListEnumerator<T>(array, 0, array.Length);

		public IListEnumerator<T> GetListEnumerator() => GetEnumerator();

		public ref readonly T ItemRef(int index) => ref array[index];

		public IRefReadOnlyList<T> GetRangeView(int index, int count)
		{
			if (index == 0 && count == Count)
				return this;
			CheckValidRange(index, count);
			return new ArrayRefReadOnlyListView<T>(array, index, count);
		}

		IReadOnlyListEx<T> IListWithRangeView<T, IReadOnlyListEx<T>>.GetRangeView(int index, int count) => (IReadOnlyListEx<T>)GetRangeView(index, count);
	}

	sealed class ArrayRefReadOnlyListView<T> : AbstractArrayRefListView<T>, IRefReadOnlyList<T>, IReadOnlyListEx<T>
	{
		internal ArrayRefReadOnlyListView(T[] array, int indexOffset, int count) : base(array, indexOffset, count)
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

		public override IRefList<T> AsNonReadOnly() => throw new NotSupportedException();

		public override IRefReadOnlyList<T> AsReadOnly() => this;

		public IRefReadOnlyListEnumerator<T> GetEnumerator() => new ArrayRefReadOnlyListEnumerator<T>(array, indexOffset, indexOffset + count);

		public IListEnumerator<T> GetListEnumerator() => GetEnumerator();

		public ref readonly T ItemRef(int index)
		{
			CheckValidIndex(index);
			return ref array[indexOffset + index];
		}

		public IRefReadOnlyList<T> GetRangeView(int index, int count)
		{
			if (index == 0 && count == Count)
				return this;
			CheckValidRange(index, count);
			return new ArrayRefReadOnlyListView<T>(array, indexOffset + index, count);
		}

		IReadOnlyListEx<T> IListWithRangeView<T, IReadOnlyListEx<T>>.GetRangeView(int index, int count) => (IReadOnlyListEx<T>)GetRangeView(index, count);
	}
}
