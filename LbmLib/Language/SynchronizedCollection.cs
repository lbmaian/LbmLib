using System;
using System.Collections;
using System.Collections.Generic;

namespace LbmLib.Language
{
	public struct SynchronizedEnumerator<T> : IEnumerator<T>
	{
		readonly IEnumerator<T> enumerator;
		readonly object sync;
		T current;

		public SynchronizedEnumerator(object sync, IEnumerator<T> enumerator)
		{
			this.enumerator = enumerator;
			this.sync = sync;
			current = default;
		}

		public T Current => current;

		object IEnumerator.Current => current;

		public bool MoveNext()
		{
			// This localizes the synchronization of both enumerator.MoveNext() and enumerator.Current to a single method.
			lock (sync)
			{
				if (enumerator.MoveNext())
				{
					current = enumerator.Current;
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
			}
		}
	}

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

		// Note: Enumeration itself is inherently not synchronized unless the caller explicitly enumerates within a SyncRoot lock,
		// but at least we can synchronize the GetEnumerator() call and the returned enumerator's relevant properties/methods.
		public IEnumerator<T> GetEnumerator()
		{
			lock (sync)
				return new SynchronizedEnumerator<T>(sync, collection.GetEnumerator());
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
	}
}
