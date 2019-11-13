using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace LbmLib.Language
{
	public static class ArrayRefListExtensions
	{
		public static IRefList<T> AsRefList<T>(this T[] array) => new ArrayRefList<T>(array);
	}

	sealed class ArrayRefList<T> : IRefList<T>
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

		readonly T[] array;

		internal ArrayRefList(T[] array)
		{
			this.array = array;
		}

		public T this[int index]
		{
			get => array[index];
			set => array[index] = value;
		}

		object IList.this[int index]
		{
			get => array[index];
			set => array[index] = (T)value;
		}

		public int Count => array.Length;

		public bool IsReadOnly => false;

		bool IList.IsReadOnly => false;

		bool IList.IsFixedSize => true;

		int ICollection.Count => array.Length;

		object ICollection.SyncRoot => array;

		bool ICollection.IsSynchronized => false;

		public void Add(T item) => throw new NotSupportedException();

		public void Clear() => throw new NotSupportedException();

		public bool Contains(T item) => array.Contains(item);

		public void CopyTo(T[] array, int arrayIndex) => this.array.CopyTo(array, arrayIndex);

		public override bool Equals(object obj) => obj is ArrayRefList<T> refList && array.Equals(refList.array);

		public IRefListEnumerator<T> GetEnumerator() => new ArrayRefListEnumerator(array);

		public override int GetHashCode() => 276365737 + array.GetHashCode();

		public int IndexOf(T item) => array.IndexOf(item);

		public void Insert(int index, T item) => throw new NotSupportedException();

		public ref T ItemRef(int index) => ref array[index];

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
}
