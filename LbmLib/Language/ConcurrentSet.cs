using System;
using System.Collections;
using System.Collections.Generic;
#if NET35
using System.Collections.ObjectModel;
#else
using System.Linq;
using System.Collections.Concurrent;
#endif

namespace LbmLib.Language
{
#if NET35
	// In .NET Framework 3.5, ConcurrentDictionary<K, V> is simply a wrapper around SynchronizedDictionary<K, V>-wrapped Dictionary<K, V>
	// that "implements the interface" of .NET Framework 4+ ConcurrentDictionary<K, V>.
	public class ConcurrentDictionary<K, V> : IDictionary<K, V>, IDictionary
	{
		readonly SynchronizedDictionary<K, V> dictionary;

		public ConcurrentDictionary()
		{
			dictionary = new SynchronizedDictionary<K, V>(new Dictionary<K, V>());
		}

		public ConcurrentDictionary(IEnumerable<KeyValuePair<K, V>> pairs)
		{
			dictionary = new SynchronizedDictionary<K, V>(pairs.AsDictionary());
		}

		public V this[K key]
		{
			get => dictionary[key];
			set => dictionary[key] = value;
		}
		object IDictionary.this[object key]
		{
			get => ((IDictionary)dictionary)[key];
			set => ((IDictionary)dictionary)[key] = value;
		}

		// For parity with .NET Framework 4.0+ ConcurrentDictionary<K, V>, this returns a ReadOnlyCollection copy of the dictionary keys.
		// This prevents InvalidOperationException when enumerating over the keys while the dictionary is potentially being modified
		// during enumeration.
		public ICollection<K> Keys
		{
			get
			{
				lock (dictionary.SyncRoot)
					return new ReadOnlyCollection<K>(dictionary.Keys.AsList());
			}
		}

		// For parity with .NET Framework 4.0+ ConcurrentDictionary<K, V>, this returns a ReadOnlyCollection copy of the dictionary values.
		// This prevents InvalidOperationException when enumerating over the values while the dictionary is potentially being modified
		// during enumeration.
		public ICollection<V> Values
		{
			get
			{
				lock (dictionary.SyncRoot)
					return new ReadOnlyCollection<V>(dictionary.Values.AsList());
			}
		}

		public int Count => dictionary.Count;

		bool ICollection<KeyValuePair<K, V>>.IsReadOnly => false;

		bool IDictionary.IsReadOnly => false;

		bool IDictionary.IsFixedSize => dictionary.IsFixedSize;

		object ICollection.SyncRoot => dictionary.SyncRoot;

		bool ICollection.IsSynchronized => dictionary.IsSynchronized;

		ICollection IDictionary.Keys => ((IDictionary)dictionary).Keys;

		ICollection IDictionary.Values => ((IDictionary)dictionary).Values;

		public V AddOrUpdate(K key, Func<K, V> addValueFactory, Func<K, V, V> updateValueFactory)
		{
			lock (dictionary.SyncRoot)
			{
				if (dictionary.TryGetValue(key, out var value))
					value = updateValueFactory(key, value);
				else
					value = addValueFactory(key);
				dictionary[key] = value;
				return value;
			}
		}

		public V AddOrUpdate(K key, V addValue, Func<K, V, V> updateValueFactory)
		{
			lock (dictionary.SyncRoot)
			{
				if (dictionary.TryGetValue(key, out var value))
					value = updateValueFactory(key, value);
				else
					value = addValue;
				dictionary[key] = value;
				return value;
			}
		}

		public V AddOrUpdate<T>(K key, Func<K, T, V> addValueFactory, Func<K, V, T, V> updateValueFactory, T factoryArgument)
		{
			lock (dictionary.SyncRoot)
			{
				if (dictionary.TryGetValue(key, out var value))
					value = updateValueFactory(key, value, factoryArgument);
				else
					value = addValueFactory(key, factoryArgument);
				dictionary[key] = value;
				return value;
			}
		}

		public void Clear() => dictionary.Clear();

		public bool ContainsKey(K key) => dictionary.ContainsKey(key);

		public void ContainsValue(V value) => dictionary.ContainsValue(value);

		// The .NET 4.0+ ConcurrentDictionary<K, V> allows enumeration while the dictionary is being modified,
		// where dictionary entries (KeyValuePair<K, V>) are returned on demand via the enumerator,
		// never throwing InvalidOperationException due to the dictionary being potentially modified during enumeration.
		// There is no reliable way to replicate this behavior exactly with Dictionary<K, V>, so the next best option is done:
		// The dictionary entries must be copied immediately within this method (rather than on demand during enumeration),
		// or else InvalidOperationException is risked (due to the dictionary being potentially modified during enumeration).
		public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
		{
			lock (dictionary.SyncRoot)
				return new List<KeyValuePair<K, V>>(dictionary).GetEnumerator();
		}

		public V GetOrAdd(K key, Func<K, V> valueFactory)
		{
			lock (dictionary.SyncRoot)
			{
				if (dictionary.TryGetValue(key, out var existingValue))
					return existingValue;
				var value = valueFactory(key);
				dictionary[key] = value;
				return value;
			}
		}

		public V GetOrAdd(K key, V value)
		{
			lock (dictionary.SyncRoot)
			{
				if (dictionary.TryGetValue(key, out var existingValue))
					return existingValue;
				dictionary[key] = value;
				return value;
			}
		}

		public V GetOrAdd<T>(K key, Func<K, T, V> valueFactory, T factoryArgument)
		{
			lock (dictionary.SyncRoot)
			{
				if (dictionary.TryGetValue(key, out var existingValue))
					return existingValue;
				var value = valueFactory(key, factoryArgument);
				dictionary[key] = value;
				return value;
			}
		}

		public KeyValuePair<K, V>[] ToArray()
		{
			lock (dictionary.SyncRoot)
			{
				var array = new KeyValuePair<K, V>[dictionary.Count];
				dictionary.CopyTo(array, 0);
				return array;
			}
		}

		public bool TryAdd(K key, V value) => dictionary.TryAdd(key, value);

		public bool TryGetValue(K key, out V value) => dictionary.TryGetValue(key, out value);

		public bool TryRemove(K key, out V value)
		{
			lock (dictionary.SyncRoot)
			{
				if (dictionary.TryGetValue(key, out value))
					return dictionary.Remove(key);
				return false;
			}
		}

		public bool TryUpdate(K key, V newValue, V comparisonValue)
		{
			lock (dictionary.SyncRoot)
			{
				if (dictionary.TryGetValue(key, out var existingValue) && Equals(existingValue, comparisonValue))
				{
					dictionary[key] = newValue;
					return true;
				}
				return false;
			}
		}

		void IDictionary<K, V>.Add(K key, V value) => dictionary.Add(key, value);

		void ICollection<KeyValuePair<K, V>>.Add(KeyValuePair<K, V> item) => dictionary.Add(item);

		void IDictionary.Add(object key, object value) => ((IDictionary)dictionary).Add(key, value);

		bool ICollection<KeyValuePair<K, V>>.Contains(KeyValuePair<K, V> item) => dictionary.Contains(item);

		bool IDictionary.Contains(object key) => ((IDictionary)dictionary).Contains(key);

		void ICollection<KeyValuePair<K, V>>.CopyTo(KeyValuePair<K, V>[] array, int arrayIndex) => dictionary.CopyTo(array, arrayIndex);

		void ICollection.CopyTo(Array array, int index) => ((ICollection)dictionary).CopyTo(array, index);

		bool IDictionary<K, V>.Remove(K key) => dictionary.Remove(key);

		bool ICollection<KeyValuePair<K, V>>.Remove(KeyValuePair<K, V> item) => dictionary.Remove(item);

		void IDictionary.Remove(object key) => ((IDictionary)dictionary).Remove(key);

		IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)dictionary).GetEnumerator();

		IDictionaryEnumerator IDictionary.GetEnumerator() => ((IDictionary)dictionary).GetEnumerator();

		public override bool Equals(object obj) => obj is ConcurrentDictionary<K, V> dictionary && dictionary.Equals(dictionary.dictionary);

		public override int GetHashCode() => -1095569795 + dictionary.GetHashCode();

		public override string ToString() => dictionary.ToString();

		public static bool operator ==(ConcurrentDictionary<K, V> left, ConcurrentDictionary<K, V> right) => Equals(left, right);

		public static bool operator !=(ConcurrentDictionary<K, V> left, ConcurrentDictionary<K, V> right) => !Equals(left, right);
	}

	// In .NET Framework 3.5, ConcurrentSet<T> is simply a wrapper around SynchronizedSet<T>-wrapped HashSet<T>.
	public class ConcurrentSet<T> : ICollection<T>, ICollection
	{
		readonly SynchronizedSet<T> set;

		public ConcurrentSet()
		{
			set = new SynchronizedSet<T>(new HashSet<T>());
		}

		public ConcurrentSet(IEnumerable<T> collection)
		{
			set = new SynchronizedSet<T>(collection.AsHashSet());
		}

		public int Count => set.Count;

		bool ICollection<T>.IsReadOnly => false;

		object ICollection.SyncRoot => set.SyncRoot;

		bool ICollection.IsSynchronized => set.IsSynchronized;

		public bool Add(T item) => set.Add(item);

		public void Clear() => set.Clear();

		public bool Contains(T item) => set.Contains(item);

		public void CopyTo(T[] array, int arrayIndex) => set.CopyTo(array, arrayIndex);

		public override bool Equals(object obj) => obj is ConcurrentSet<T> set && set.Equals(set.set);

		public void ExceptWith(IEnumerable<T> other) => set.ExceptWith(other);

		public IEnumerator<T> GetEnumerator() => set.GetEnumerator();

		public override int GetHashCode() => -191684997 + set.GetHashCode();

		public void IntersectWith(IEnumerable<T> other) => set.IntersectWith(other);

		public bool IsProperSubsetOf(IEnumerable<T> other) => set.IsProperSubsetOf(other);

		public bool IsProperSupersetOf(IEnumerable<T> other) => set.IsProperSupersetOf(other);

		public bool IsSubsetOf(IEnumerable<T> other) => set.IsSubsetOf(other);

		public bool IsSupersetOf(IEnumerable<T> other) => set.IsSupersetOf(other);

		public bool Overlaps(IEnumerable<T> other) => set.Overlaps(other);

		public bool Remove(T item) => set.Remove(item);

		public bool SetEquals(IEnumerable<T> other) => set.SetEquals(other);

		public void SymmetricExceptWith(IEnumerable<T> other) => set.SymmetricExceptWith(other);

		public override string ToString() => set.ToString();

		public void UnionWith(IEnumerable<T> other) => set.UnionWith(other);

		void ICollection<T>.Add(T item) => Add(item);

		void ICollection.CopyTo(Array array, int index) => CopyTo((T[])array, index);

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public static bool operator ==(ConcurrentSet<T> left, ConcurrentSet<T> right) => Equals(left, right);

		public static bool operator !=(ConcurrentSet<T> left, ConcurrentSet<T> right) => !Equals(left, right);
	}
#else
	// A concurrent ISet<T> implementation based off ConcurrentDictionary<T, bool>.
	public class ConcurrentSet<T> : ISet<T>, ICollection
	{
		readonly ConcurrentDictionary<T, bool> dictionary;

		public ConcurrentSet()
		{
			dictionary = new ConcurrentDictionary<T, bool>();
		}

		public ConcurrentSet(IEnumerable<T> collection)
		{
			dictionary = new ConcurrentDictionary<T, bool>(collection.Select(item => new KeyValuePair<T, bool>(item, true)));
		}

		public ConcurrentSet(int concurrencyLevel, int capacity)
		{
			dictionary = new ConcurrentDictionary<T, bool>(concurrencyLevel, capacity);
		}

		public ConcurrentSet(IEqualityComparer<T> comparer)
		{
			dictionary = new ConcurrentDictionary<T, bool>(comparer);
		}

		public ConcurrentSet(IEnumerable<T> collection, IEqualityComparer<T> comparer)
		{
			dictionary = new ConcurrentDictionary<T, bool>(collection.Select(item => new KeyValuePair<T, bool>(item, true)), comparer);
		}

		public ConcurrentSet(int concurrencyLevel, IEnumerable<T> collection, IEqualityComparer<T> comparer)
		{
			dictionary = new ConcurrentDictionary<T, bool>(concurrencyLevel, collection.Select(item => new KeyValuePair<T, bool>(item, true)), comparer);
		}

		public ConcurrentSet(int concurrencyLevel, int capacity, IEqualityComparer<T> comparer)
		{
			dictionary = new ConcurrentDictionary<T, bool>(concurrencyLevel, capacity, comparer);
		}

		public int Count => dictionary.Count;

		bool ICollection<T>.IsReadOnly => false;

		object ICollection.SyncRoot => ((ICollection)dictionary).SyncRoot;

		bool ICollection.IsSynchronized => ((ICollection)dictionary).IsSynchronized;

		public bool Add(T item) => dictionary.TryAdd(item, true);

		public void Clear() => dictionary.Clear();

		public bool Contains(T item) => dictionary.ContainsKey(item);

		public void CopyTo(T[] array, int arrayIndex)
		{
			var length = array.Length - arrayIndex;
			var pairs = new KeyValuePair<T, bool>[length];
			((ICollection< KeyValuePair<T, bool>>)dictionary).CopyTo(pairs, 0);
			Array.Copy(pairs, 0, array, arrayIndex, length);
		}

		// Note: For consistency with other set operations, this can only remove items that are currently in the set
		// at the moment this method is called, so additional items could be added to this set by the time this method returns
		// and they wouldn't be considered.
		// This also assumes that other is not mutated while the method is running;
		// If it is mutated, InvalidOperationException may be thrown, or items added to other may or may not get accounted for.
		// The runtime performance of this method is O(n+m), where n is # items in this set, and m is # items in other.
		public void ExceptWith(IEnumerable<T> other)
		{
			var itemSet = dictionary.Keys.AsSet();
			foreach (var item in other)
			{
				if (itemSet.Remove(item))
					dictionary.TryRemove(item, out _);
			}
		}

		public IEnumerator<T> GetEnumerator()
		{
			var enumerator = dictionary.GetEnumerator();
			while (enumerator.MoveNext())
				yield return enumerator.Current.Key;
		}

		// Note: This can only remove items that are currently in the set at the moment this method is called
		// so additional items could be added to this set by the time this method returns and they wouldn't be considered.
		// This also assumes that other is not mutated while the method is running;
		// If it is mutated, InvalidOperationException may be thrown, or items added to other may or may not get accounted for.
		// The runtime performance of this method is O(n*x), where n is # items in this set, and other.Contains's performance is O(x).
		// Therefore, it's recommended that other.Contains have a O(1) implmentation.
		public void IntersectWith(IEnumerable<T> other)
		{
			var items = dictionary.Keys;
			foreach (var item in items)
			{
				if (!other.Contains(item))
					dictionary.TryRemove(item, out _);
			}
		}

		// Note: This only considers items that are currently in the set at the moment this method is called
		// so additional items could be added to this set by the time this method returns and they wouldn't be considered.
		// This also assumes that other is not mutated while the method is running;
		// If it is mutated, InvalidOperationException may be thrown, or items added to other may or may not get accounted for.
		// The runtime performance of this method is O(n*x+m), where n is # items in this set, m is # items in other,
		// and other.Contains's performance is O(x). Therefore, it's recommended that other.Contains have a O(1) implmentation.
		public bool IsProperSubsetOf(IEnumerable<T> other)
		{
			var items = dictionary.Keys;
			if (other is ISet<T> otherSet && otherSet.Count <= items.Count)
				return false;
			return ContainsAll(items, other) && !ContainsAll(other, items.AsSet());
		}

		// Note: This only considers items that are currently in the set at the moment this method is called
		// so additional items could be added to this set by the time this method returns and they wouldn't be considered.
		// This also assumes that other is not mutated while the method is running;
		// If it is mutated, InvalidOperationException may be thrown, or items added to other may or may not get accounted for.
		// The runtime performance of this method is O(n*x+m), where n is # items in this set, m is # items in other,
		// and other.Contains's performance is O(x). Therefore, it's recommended that other.Contains have a O(1) implmentation.
		public bool IsProperSupersetOf(IEnumerable<T> other)
		{
			var items = dictionary.Keys;
			if (other is ISet<T> otherSet && otherSet.Count >= items.Count)
				return false;
			return ContainsAll(other, items.AsSet()) && !ContainsAll(items, other);
		}

		// Note: This only considers items that are currently in the set at the moment this method is called
		// so additional items could be added to this set by the time this method returns and they wouldn't be considered.
		// This also assumes that other is not mutated while the method is running;
		// If it is mutated, InvalidOperationException may be thrown, or items added to other may or may not get accounted for.
		// The runtime performance of this method is O(n*x), where n is # items in this set, and other.Contains's performance is O(x).
		// Therefore, it's recommended that other.Contains have a O(1) implmentation.
		public bool IsSubsetOf(IEnumerable<T> other)
		{
			var items = dictionary.Keys;
			if (other is ISet<T> otherSet && otherSet.Count < items.Count)
				return false;
			return ContainsAll(items, other);
		}

		// Note: This only considers items that are currently in the set at the moment this method is called
		// so additional items could be added to this set by the time this method returns and they wouldn't be considered.
		// This also assumes that other is not mutated while the method is running;
		// If it is mutated, InvalidOperationException may be thrown, or items added to other may or may not get accounted for.
		// The runtime performance of this method is O(n+m), where n is # items in this set, and m is # items in other.
		public bool IsSupersetOf(IEnumerable<T> other)
		{
			var items = dictionary.Keys;
			if (other is ISet<T> otherSet && otherSet.Count > items.Count)
				return false;
			return ContainsAll(other, items.AsSet());
		}

		static bool ContainsAll(IEnumerable<T> itemsA, IEnumerable<T> itemsB)
		{
			foreach (var item in itemsA)
			{
				if (!itemsB.Contains(item))
					return false;
			}
			return true;
		}

		// Note: This only considers items that are currently in the set at the moment this method is called
		// so additional items could be added to this set by the time this method returns and they wouldn't be considered.
		// This also assumes that other is not mutated while the method is running;
		// If it is mutated, InvalidOperationException may be thrown, or items added to other may or may not get accounted for.
		// The runtime performance of this method is O(n+m), where n is # items in this set, and m is # items in other.
		public bool Overlaps(IEnumerable<T> other)
		{
			var items = dictionary.Keys;
			foreach (var item in other)
			{
				if (items.Contains(item))
					return true;
			}
			return false;
		}

		public bool Remove(T item) => dictionary.TryRemove(item, out _);

		// Note: This only considers items that are currently in the set at the moment this method is called
		// so additional items could be added to this set by the time this method returns and they wouldn't be considered.
		// This also assumes that other is not mutated while the method is running;
		// If it is mutated, InvalidOperationException may be thrown, or items added to other may or may not get accounted for.
		// The runtime performance of this method is O(n*x+m), where n is # items in this set, m is # items in other,
		// and other.Contains's performance is O(x). Therefore, it's recommended that other.Contains have a O(1) implmentation.
		public bool SetEquals(IEnumerable<T> other)
		{
			var items = dictionary.Keys;
			// Annoyingly, ConcurrentDictionary.Keys isn't necessarily an ISet itself, so we cannot simply do items.SetEquals(other).
			// Still, since we know the items in IDictionary.Keys are unique, we can do an optimization if other is also an ISet.
			if (other is ISet<T> otherSet)
				return otherSet.Count == items.Count && ContainsAll(items, otherSet);
			return ContainsAll(items, other) && ContainsAll(other, items.AsSet());
		}

		// Note: This can only remove items that are currently in the set at the moment this method is called
		// so additional items could be added to this set by the time this method returns and they wouldn't be considered.
		// This also assumes that other is not mutated while the method is running;
		// If it is mutated, InvalidOperationException may be thrown, or items added to other may or may not get accounted for.
		// The runtime performance of this method is O(n*x+m), where n is # items in this set, m is # items in other,
		// and other.Contains's performance is O(x). Therefore, it's recommended that other.Contains have a O(1) implmentation.
		public void SymmetricExceptWith(IEnumerable<T> other)
		{
			var itemSet = dictionary.Keys.AsSet();
			foreach (var item in other)
			{
				if (itemSet.Remove(item))
					dictionary.TryRemove(item, out _);
			}
			foreach (var item in itemSet)
			{
				if (other.Contains(item))
					dictionary.TryRemove(item, out _);
			}
		}

		// This also assumes that other is not mutated while the method is running;
		// If it is mutated, InvalidOperationException may be thrown, or items added to other may or may not get accounted for.
		// The runtime performance of this method is O(m), where m is # items in other.
		public void UnionWith(IEnumerable<T> other)
		{
			foreach (var item in other)
			{
				dictionary.TryAdd(item, true);
			}
		}

		void ICollection<T>.Add(T item) => Add(item);

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		void ICollection.CopyTo(Array array, int index) => CopyTo((T[])array, index);

		public override bool Equals(object obj) => obj is ConcurrentSet<T> set && dictionary.Equals(set.dictionary);

		public override int GetHashCode() => -1095569795 + dictionary.GetHashCode();

		public override string ToString() => dictionary.ToString();

		public static bool operator ==(ConcurrentSet<T> left, ConcurrentSet<T> right) => Equals(left, right);

		public static bool operator !=(ConcurrentSet<T> left, ConcurrentSet<T> right) => !Equals(left, right);
	}
#endif
}
