using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace LbmLib.Language
{
	public static class ArrayRefListExtensions
	{
		public static IRefList<T> AsRefList<T>(this T[] array) => new ArrayRefList<T>(array);

		public static IRefReadOnlyList<T> AsRefReadOnlyList<T>(this T[] array) => new ArrayRefReadOnlyList<T>(array);
	}

	abstract class AbstractArrayRefList<T> : IBaseRefList<T>
	{
		private protected readonly T[] array;

		private protected AbstractArrayRefList(T[] array)
		{
			this.array = array;
		}

		public abstract T this[int index] { get; set; }

		object IList.this[int index]
		{
			get => array[index];
			set => array[index] = (T)value;
		}

		public int Count => array.Length;

		public abstract bool IsReadOnly { get; }

		bool IList.IsReadOnly => IsReadOnly;

		bool IList.IsFixedSize => true;

		int ICollection.Count => array.Length;

		object ICollection.SyncRoot => array;

		bool ICollection.IsSynchronized => false;

		public void Add(T item) => throw new NotSupportedException();

		public abstract IRefList<T> AsNonReadOnly();

		public abstract IRefReadOnlyList<T> AsReadOnly();

		public void Clear() => throw new NotSupportedException();

		public bool Contains(T item) => array.Contains(item);

		public void CopyTo(T[] array, int arrayIndex) => this.array.CopyTo(array, arrayIndex);

		public override bool Equals(object obj) => obj is AbstractArrayRefList<T> refList && array.Equals(refList.array);

		public override int GetHashCode() => 276365737 + array.GetHashCode();

		public int IndexOf(T item) => array.IndexOf(item);

		public void Insert(int index, T item) => throw new NotSupportedException();

		public bool Remove(T item) => throw new NotSupportedException();

		public void RemoveAt(int index) => throw new NotSupportedException();

		int IList.Add(object value) => throw new NotSupportedException();

		void IList.Clear() => throw new NotSupportedException();

		bool IList.Contains(object value) => Contains((T)value);

		void ICollection.CopyTo(Array array, int index) => this.array.CopyTo(array, index);

		IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)array).GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => array.GetEnumerator();

		int IList.IndexOf(object value) => array.IndexOf((T)value);

		void IList.Insert(int index, object value) => throw new NotSupportedException();

		void IList.Remove(object value) => throw new NotSupportedException();

		void IList.RemoveAt(int index) => throw new NotSupportedException();
	}

	sealed class ArrayRefList<T> : AbstractArrayRefList<T>, IRefList<T>
	{
		struct ArrayRefListEnumerator : IRefListEnumerator<T>
		{
			readonly T[] array;
			int index;

			internal ArrayRefListEnumerator(T[] array)
			{
				this.array = array;
				index = -1;
			}

			public ref T Current => ref array[index];

			public int CurrentIndex => index;

			public bool MoveNext() => ++index < array.Length;
		}

		internal ArrayRefList(T[] array) : base(array)
		{
		}

		public override T this[int index]
		{
			get => array[index];
			set => array[index] = value;
		}

		public override bool IsReadOnly => false;

		public override IRefList<T> AsNonReadOnly() => this;

		public override IRefReadOnlyList<T> AsReadOnly() => new ArrayRefReadOnlyList<T>(array);

		public IRefListEnumerator<T> GetEnumerator() => new ArrayRefListEnumerator(array);

		public ref T ItemRef(int index) => ref array[index];
	}

	sealed class ArrayRefReadOnlyList<T> : AbstractArrayRefList<T>, IRefReadOnlyList<T>
	{
		struct ArrayRefReadOnlyListEnumerator : IRefReadOnlyListEnumerator<T>
		{
			readonly T[] array;
			int index;

			internal ArrayRefReadOnlyListEnumerator(T[] array)
			{
				this.array = array;
				index = -1;
			}

			public ref readonly T Current => ref array[index];

			public int CurrentIndex => index;

			public bool MoveNext() => ++index < array.Length;
		}

		internal ArrayRefReadOnlyList(T[] array) : base(array)
		{
		}

		// As this class isn't public, the indexer set accessor being public here doesn't matter.
		// If this instance is cast as an IRefReadOnlyList<T>, then the indexer set accessor effectively isn't public.
		public override T this[int index]
		{
			get => array[index];
			set => throw new NotSupportedException();
		}

		public override bool IsReadOnly => true;

		public override IRefList<T> AsNonReadOnly() => throw new NotSupportedException();

		public override IRefReadOnlyList<T> AsReadOnly() => this;

		public IRefReadOnlyListEnumerator<T> GetEnumerator() => new ArrayRefReadOnlyListEnumerator(array);

		public ref readonly T ItemRef(int index) => ref array[index];
	}
}
