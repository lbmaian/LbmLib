using System;
using System.Collections;
using System.Collections.Generic;

namespace LbmLib.Language
{
	public struct SynchronizedListEnumerator<T> : IListEnumerator<T>
	{
		readonly IListEnumerator<T> enumerator;
		readonly object sync;
		T current;
		int currentIndex;

		public SynchronizedListEnumerator(object sync, IListEnumerator<T> enumerator)
		{
			this.sync = sync;
			this.enumerator = enumerator;
			current = default;
			currentIndex = -1;
		}

		public T Current => current;

		public int CurrentIndex => currentIndex;

		object IEnumerator.Current => current;

		public bool MoveNext()
		{
			// This localizes the synchronization of both enumerator.MoveNext() and enumerator.Current to a single method.
			lock (sync)
			{
				if (enumerator.MoveNext())
				{
					current = enumerator.Current;
					currentIndex = enumerator.CurrentIndex;
					return true;
				}
				return false;
			}
		}

		public void Dispose()
		{
			lock (sync)
				enumerator.Dispose();
		}

		void IEnumerator.Reset()
		{
			lock (sync)
			{
				enumerator.Reset();
				current = default;
				currentIndex = -1;
			}
		}
	}

	// Similar to System.Collection.Generic.SynchronizedCollection<T>, but as an IList<T> wrapper
	// and without needing a dependency on System.ServiceModel.dll.
	public class SynchronizedList<T> : IListEx<T>
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

		public void AddRange(IEnumerable<T> collection)
		{
			lock (sync)
				list.AddRange(collection);
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

		public void CopyTo(int index, T[] array, int arrayIndex, int count)
		{
			lock (sync)
				list.CopyTo(index, array, arrayIndex, count);
		}

		// Note: Enumeration itself is inherently not synchronized unless the caller explicitly enumerates within a SyncRoot lock,
		// but at least we can synchronize the GetEnumerator() call and the returned enumerator's relevant properties/methods.
		public IEnumerator<T> GetEnumerator()
		{
			lock (sync)
				return new SynchronizedEnumerator<T>(sync, list.GetEnumerator());
		}

		// Note: Enumeration itself is inherently not synchronized unless the caller explicitly enumerates within a SyncRoot lock,
		// but at least we can synchronize the GetListEnumerator() call and the returned enumerator's relevant properties/methods.
		public IListEnumerator<T> GetListEnumerator()
		{
			lock (sync)
				return new SynchronizedListEnumerator<T>(sync, list.GetListEnumerator());
		}

		public List<T> GetRange(int index, int count)
		{
			lock (sync)
				return list.GetRange(index, count);
		}

		public IListEx<T> GetRangeView(int index, int count)
		{
			lock (sync)
				return new SynchronizedList<T>(sync, list.GetRangeView(index, count));
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

		public void InsertRange(int index, IEnumerable<T> collection)
		{
			lock (sync)
				list.InsertRange(index, collection);
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

		public void RemoveRange(int index, int count)
		{
			lock (sync)
				list.RemoveRange(index, count);
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
	}
}
