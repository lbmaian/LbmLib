using System;
using System.Collections;
using System.Collections.Generic;

namespace LbmLib.Language
{
	// Similar to System.Collection.Generic.SynchronizedCollection<T>, but as an ICollection<T> wrapper
	// and without needing a dependency on System.ServiceModel.dll.
	public class SynchronizedCollection<T> : ICollection<T>, ICollection
	{
		readonly ICollection<T> collection;
		readonly object sync;

		public SynchronizedCollection(object sync, ICollection<T> collection)
		{
			this.collection = collection;
			this.sync = sync;
		}

		public SynchronizedCollection(ICollection<T> collection) : this(new object(), collection)
		{
		}

		public int Count
		{
			get
			{
				lock (sync)
					return collection.Count;
			}
		}

		public bool IsReadOnly => collection.IsReadOnly;

		public object SyncRoot => sync;

		public bool IsSynchronized => true;

		public void Add(T item)
		{
			lock (sync)
				collection.Add(item);
		}

		public void Clear()
		{
			lock (sync)
				collection.Clear();
		}

		public bool Contains(T item)
		{
			lock (sync)
				return collection.Contains(item);
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			lock (sync)
				collection.CopyTo(array, arrayIndex);
		}

		public IEnumerator<T> GetEnumerator()
		{
			// Note: Enumeration itself is inherently not synchronized unless the caller explicitly enumerates within a SyncRoot lock,
			// but at least we can synchronize the GetEnumerator() call.
			lock (sync)
				return collection.GetEnumerator();
		}

		public bool Remove(T item)
		{
			lock (sync)
				return collection.Remove(item);
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		void ICollection.CopyTo(Array array, int index) => CopyTo((T[])array, index);

		public override bool Equals(object obj) => obj is SynchronizedCollection<T> collection && collection.Equals(collection.collection);

		public override int GetHashCode() => -1997316039 + collection.GetHashCode();

		public override string ToString() => collection.ToString();

		public static bool operator ==(SynchronizedCollection<T> left, SynchronizedCollection<T> right) => Equals(left, right);

		public static bool operator !=(SynchronizedCollection<T> left, SynchronizedCollection<T> right) => !Equals(left, right);
	}
}
