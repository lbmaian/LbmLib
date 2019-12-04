using System;
using System.Collections;
using System.Collections.Generic;

namespace LbmLib.Language
{
	// Synchronizing wrapper around an ISet<T> (or HashSet<T> for .NET Framework 3.5, since it lacks ISet<T>).
	public class SynchronizedSet<T> :
#if NET35
		ICollection<T>,
#else
		ISet<T>,
#endif
		ICollection
	{
#if NET35
		readonly HashSet<T> set;
#else
		readonly ISet<T> set;
#endif
		readonly object sync;

#if NET35
		public SynchronizedSet(object sync, HashSet<T> set)
#else
		public SynchronizedSet(object sync, ISet<T> set)
#endif
		{
			this.set = set;
			this.sync = sync;
		}

#if NET35
		public SynchronizedSet(HashSet<T> set) : this(new object(), set)
#else
		public SynchronizedSet(ISet<T> set) : this(new object(), set)
#endif
		{
		}

		public int Count
		{
			get
			{
				lock (sync)
					return set.Count;
			}
		}

		public bool IsReadOnly =>
#if NET35
			((ICollection<T>)set).IsReadOnly;
#else
			set.IsReadOnly;
#endif

		public object SyncRoot => sync;

		public bool IsSynchronized => true;

		public bool Add(T item)
		{
			lock (sync)
				return set.Add(item);
		}

		public void Clear()
		{
			lock (sync)
				set.Clear();
		}

		public bool Contains(T item)
		{
			lock (sync)
				return set.Contains(item);
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			lock (sync)
				set.CopyTo(array, arrayIndex);
		}

		public void ExceptWith(IEnumerable<T> other)
		{
			lock (sync)
				set.ExceptWith(other);
		}

		// Note: Enumeration itself is inherently not synchronized unless the caller explicitly enumerates within a SyncRoot lock,
		// but at least we can synchronize the GetEnumerator() call and the returned enumerator's relevant properties/methods.
		public IEnumerator<T> GetEnumerator()
		{
			lock (sync)
				return new SynchronizedEnumerator<T>(sync, set.GetEnumerator());
		}

		public void IntersectWith(IEnumerable<T> other)
		{
			lock (sync)
				set.IntersectWith(other);
		}

		public bool IsProperSubsetOf(IEnumerable<T> other)
		{
			lock (sync)
				return set.IsProperSubsetOf(other);
		}

		public bool IsProperSupersetOf(IEnumerable<T> other)
		{
			lock (sync)
				return set.IsProperSupersetOf(other);
		}

		public bool IsSubsetOf(IEnumerable<T> other)
		{
			lock (sync)
				return set.IsSubsetOf(other);
		}

		public bool IsSupersetOf(IEnumerable<T> other)
		{
			lock (sync)
				return set.IsSupersetOf(other);
		}

		public bool Overlaps(IEnumerable<T> other)
		{
			lock (sync)
				return set.Overlaps(other);
		}

		public bool Remove(T item)
		{
			lock (sync)
				return set.Remove(item);
		}

		public bool SetEquals(IEnumerable<T> other)
		{
			lock (sync)
				return set.SetEquals(other);
		}

		public void SymmetricExceptWith(IEnumerable<T> other)
		{
			lock (sync)
				set.SymmetricExceptWith(other);
		}

		public void UnionWith(IEnumerable<T> other)
		{
			lock (sync)
				set.UnionWith(other);
		}

		void ICollection<T>.Add(T item) => Add(item);

		void ICollection.CopyTo(Array array, int index) => CopyTo((T[])array, index);

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public override bool Equals(object obj) => obj is SynchronizedSet<T> set && set.Equals(set.set);

		public override int GetHashCode() => -191684997 + set.GetHashCode();

		public override string ToString() => set.ToString();
	}
}
