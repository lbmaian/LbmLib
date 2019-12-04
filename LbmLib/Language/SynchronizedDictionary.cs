using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace LbmLib.Language
{
	// Synchronized wrapper over an IDictionary. Intended for .NET 3.5 Framework usage, since it lacks ConcurrentDictionary,
	// which would have better performance over a naive synchronize-on-every-method class like this.
	public class SynchronizedDictionary<K, V> : IDictionary<K, V>, IDictionary
	{
		readonly IDictionary<K, V> dictionary;
		readonly object sync;

		public SynchronizedDictionary(object sync, IDictionary<K, V> dictionary)
		{
			this.sync = sync;
			this.dictionary = dictionary;
		}

		public SynchronizedDictionary(IDictionary<K, V> dictionary) : this(new object(), dictionary)
		{
		}

		public V this[K key]
		{
			get
			{
				lock (sync)
					return dictionary[key];
			}
			set
			{
				lock (sync)
					dictionary[key] = value;
			}
		}

		public ICollection<K> Keys => new SynchronizedCollection<K>(sync, dictionary.Keys);

		public ICollection<V> Values => new SynchronizedCollection<V>(sync, dictionary.Values);

		public int Count
		{
			get
			{
				lock (sync)
					return dictionary.Count;
			}
		}

		public bool IsReadOnly => dictionary.IsReadOnly;

		ICollection IDictionary.Keys => new SynchronizedCollection<K>(sync, dictionary.Keys);

		ICollection IDictionary.Values => new SynchronizedCollection<V>(sync, dictionary.Values);

		public bool IsFixedSize => dictionary is IDictionary idictionary ? idictionary.IsFixedSize : false;

		public object SyncRoot => sync;

		public bool IsSynchronized => true;

		object IDictionary.this[object key]
		{
			get => this[(K)key];
			set => this[(K)key] = (V)value;
		}

		public void Add(K key, V value)
		{
			lock (sync)
				dictionary.Add(key, value);
		}

		public void Add(KeyValuePair<K, V> item)
		{
			lock (sync)
				dictionary.Add(item);
		}

		public void Clear()
		{
			lock (sync)
				dictionary.Clear();
		}

		public bool Contains(KeyValuePair<K, V> item)
		{
			lock (sync)
				return dictionary.Contains(item);
		}

		public bool ContainsKey(K key)
		{
			lock (sync)
				return dictionary.ContainsKey(key);
		}

		public bool ContainsValue(V value)
		{
			if (dictionary is Dictionary<K, V> actualDictionary)
			{
				lock (sync)
					return actualDictionary.ContainsValue(value);
			}
			else
			{
				lock (sync)
					return dictionary.Any(pair => Equals(pair.Value, value));
			}
		}

		public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
		{
			lock (sync)
				dictionary.CopyTo(array, arrayIndex);
		}

		// Note: Enumeration itself is inherently not synchronized unless the caller explicitly enumerates within a SyncRoot lock,
		// but at least we can synchronize the GetEnumerator() call and the returned enumerator's relevant properties/methods.
		public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
		{
			lock (sync)
				return new SynchronizedEnumerator<KeyValuePair<K, V>>(sync, dictionary.GetEnumerator());
		}

		public bool Remove(K key)
		{
			lock (sync)
				return dictionary.Remove(key);
		}

		public bool Remove(KeyValuePair<K, V> item)
		{
			lock (sync)
				return dictionary.Remove(item);
		}

		public bool TryAdd(K key, V value)
		{
			lock (sync)
				return dictionary.TryAdd(key, value);
		}

		public bool TryGetValue(K key, out V value)
		{
			lock (sync)
				return dictionary.TryGetValue(key, out value);
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		bool IDictionary.Contains(object key) => ContainsKey((K)key);

		void IDictionary.Add(object key, object value) => Add((K)key, (V)value);

		struct WrapperDictionaryEnumerator : IDictionaryEnumerator
		{
			readonly IDictionaryEnumerator enumerator;
			readonly object sync;
			DictionaryEntry entry;

			internal WrapperDictionaryEnumerator(object sync, IDictionaryEnumerator enumerator)
			{
				this.enumerator = enumerator;
				this.sync = sync;
				entry = default;
			}

			public object Key => entry.Key;

			public object Value => entry.Value;

			public DictionaryEntry Entry => entry;

			public object Current => entry;

			public bool MoveNext()
			{
				// This localizes the synchronization of both enumerator.MoveNext() and enumerator.Current to a single method.
				lock (sync)
				{
					if (enumerator.MoveNext())
					{
						entry = enumerator.Entry;
						return true;
					}
					return false;
				}
			}

			void IEnumerator.Reset()
			{
				lock (sync)
				{
					enumerator.Reset();
					entry = default;
				}
			}
		}

		struct KeyValuePairDictionaryEnumerator : IDictionaryEnumerator, IDisposable
		{
			readonly IEnumerator<KeyValuePair<K, V>> enumerator;
			readonly object sync;
			DictionaryEntry entry;

			internal KeyValuePairDictionaryEnumerator(object sync, IEnumerator<KeyValuePair<K, V>> enumerator)
			{
				this.enumerator = enumerator;
				this.sync = sync;
				entry = default;
			}

			public object Key => entry.Key;

			public object Value => entry.Value;

			public DictionaryEntry Entry => entry;

			public object Current => entry;

			public bool MoveNext()
			{
				// This localizes the synchronization of both enumerator.MoveNext() and enumerator.Current to a single method.
				lock (sync)
				{
					if (enumerator.MoveNext())
					{
						var pair = enumerator.Current;
						entry = new DictionaryEntry(pair.Key, pair.Value);
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
					entry = default;
				}
			}
		}

		// Note: Enumeration itself is inherently not synchronized unless the caller explicitly enumerates within a SyncRoot lock,
		// but at least we can synchronize the GetEnumerator() call and the returned enumerator's relevant properties/methods.
		IDictionaryEnumerator IDictionary.GetEnumerator()
		{
			lock (sync)
			{
				if (dictionary is IDictionary idictionary)
					return new WrapperDictionaryEnumerator(sync, idictionary.GetEnumerator());
				else
					return new KeyValuePairDictionaryEnumerator(sync, dictionary.GetEnumerator());
			}
		}

		void IDictionary.Remove(object key) => Remove((K)key);

		void ICollection.CopyTo(Array array, int index) => CopyTo((KeyValuePair<K, V>[])array, index);

		public override bool Equals(object obj) => obj is SynchronizedDictionary<K, V> dictionary && dictionary.Equals(dictionary.dictionary);

		public override int GetHashCode() => -1095569795 + dictionary.GetHashCode();

		public override string ToString() => dictionary.ToString();
	}
}
