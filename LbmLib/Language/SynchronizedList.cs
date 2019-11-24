using System;
using System.Collections;
using System.Collections.Generic;

namespace LbmLib.Language
{
	// Similar to System.Collection.Generic.SynchronizedCollection<T>, but as an IList<T> wrapper
	// and without needing a dependency on System.ServiceModel.dll.
	public class SynchronizedList<T> : IList<T>, IList
	{
		readonly IList<T> list;
		readonly object sync;

		public SynchronizedList(object sync, IList<T> list)
		{
			this.list = list;
			this.sync = sync;
		}

		public SynchronizedList(IList<T> list) : this(new object(), list)
		{
		}

		public T this[int index]
		{
			get
			{
				lock (sync)
					return list[index];
			}
			set
			{
				lock (sync)
					list[index] = value;
			}
		}

		public int Count
		{
			get
			{
				lock (sync)
					return list.Count;
			}
		}

		public bool IsReadOnly => list.IsReadOnly;

		public object SyncRoot => sync;

		public bool IsSynchronized => true;

		bool IList.IsFixedSize => list is IList ilist ? ilist.IsFixedSize : false;

		object IList.this[int index]
		{
			get => this[index];
			set => this[index] = (T)value;
		}

		public void Add(T item)
		{
			lock (sync)
				list.Add(item);
		}

		public void Clear()
		{
			lock (sync)
				list.Clear();
		}

		public bool Contains(T item)
		{
			lock (sync)
				return list.Contains(item);
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			lock (sync)
				list.CopyTo(array, arrayIndex);
		}

		public IEnumerator<T> GetEnumerator()
		{
			// Note: Enumeration itself is inherently not synchronized unless the caller explicitly enumerates within a SyncRoot lock,
			// but at least we can synchronize the GetEnumerator() call.
			lock (sync)
				return list.GetEnumerator();
		}

		public int IndexOf(T item)
		{
			lock (sync)
				return list.IndexOf(item);
		}

		public void Insert(int index, T item)
		{
			lock (sync)
				list.Insert(index, item);
		}

		public bool Remove(T item)
		{
			lock (sync)
				return list.Remove(item);
		}

		public void RemoveAt(int index)
		{
			lock (sync)
				list.RemoveAt(index);
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		int IList.Add(object value)
		{
			Add((T)value);
			return Count - 1;
		}

		bool IList.Contains(object value) => Contains((T)value);

		int IList.IndexOf(object value) => IndexOf((T)value);

		void IList.Insert(int index, object value) => Insert(index, (T)value);

		void IList.Remove(object value) => Remove((T)value);

		void ICollection.CopyTo(Array array, int index) => CopyTo((T[])array, index);

		public override bool Equals(object obj) => obj is SynchronizedList<T> list && list.Equals(list.list);

		public override int GetHashCode() => 276365737 + list.GetHashCode();

		public override string ToString() => list.ToString();

		public static bool operator ==(SynchronizedList<T> left, SynchronizedList<T> right) => Equals(left, right);

		public static bool operator !=(SynchronizedList<T> left, SynchronizedList<T> right) => !Equals(left, right);
	}
}
