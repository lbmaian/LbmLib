using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace LbmLib.Language
{
	public static class ListRangeExtensions
	{
		// Generic IList<T> equivalent to List<T>.GetRange(int, int).
		public static List<T> GetRange<T>(this IList<T> list, int index, int count)
		{
			if (list is List<T> actualList)
				return actualList.GetRange(index, count);
			else if (list is IListWithReadOnlyRangeMethods<T> listWithRangeMethods)
				return listWithRangeMethods.GetRange(index, count);
			else
				return list.GenericGetRange(index, count);
		}

		internal static List<T> GenericGetRange<T>(this IList<T> list, int index, int count)
		{
			if (index < 0)
				throw new ArgumentOutOfRangeException($"index ({index}) cannot be < 0");
			if (count < 0)
				throw new ArgumentOutOfRangeException($"count ({count}) cannot be < 0");
			var listCount = list.Count;
			if (index > listCount - count)
				throw new ArgumentOutOfRangeException($"index ({index}) + count ({count}) cannot be > list.Count ({listCount})");
			var range = new List<T>(count);
			var endIndexExcl = index + count;
			while (index < endIndexExcl)
			{
				range.Add(list[index]);
				index++;
			}
			return range;
		}

		// Generic IList<T> equivalent to List<T>.CopyTo(int, T[], int, int).
		public static void CopyTo<T>(this IList<T> list, int index, T[] array, int arrayIndex, int count)
		{
			if (list is List<T> actualList)
				actualList.CopyTo(index, array, arrayIndex, count);
			else if (list is Array actualArray)
				Array.Copy(actualArray, index, array, arrayIndex, count);
			else if (list is IListWithReadOnlyRangeMethods<T> listWithRangeMethods)
				listWithRangeMethods.CopyTo(index, array, arrayIndex, count);
			else
				list.GenericCopyTo(index, array, arrayIndex, count);
		}

		internal static void GenericCopyTo<T>(this IList<T> list, int index, T[] array, int arrayIndex, int count)
		{
			// Following is based off Mono .NET Framework implementation, since MS .NET Framework one delegates most error checking to an extern Array.Copy.
			var listCount = list.Count;
			var arrayCount = array.Length;
			if (index < 0)
				throw new ArgumentOutOfRangeException($"index ({index}) cannot be < 0");
			if (arrayIndex < 0)
				throw new ArgumentOutOfRangeException($"arrayIndex ({arrayIndex}) cannot be < 0");
			if (count < 0)
				throw new ArgumentOutOfRangeException($"count ({count}) cannot be < 0");
			if (listCount - index < count)
				throw new ArgumentOutOfRangeException($"index ({index}) + count ({count}) cannot be > list.Count ({listCount})");
			if (arrayCount - arrayIndex < count)
				throw new ArgumentOutOfRangeException($"arrayIndex ({arrayIndex}) + count ({count}) cannot be > array.Length ({arrayCount})");
			var endIndexExcl = index + count;
			while (index < endIndexExcl)
			{
				array[arrayIndex] = list[index];
				index++;
				arrayIndex++;
			}
		}

		// Generic IList<T> equivalent to List<T>.AddRange(IEnumerable<T>).
		public static void AddRange<T>(this IList<T> list, IEnumerable<T> collection)
		{
			if (list is List<T> actualList)
				actualList.AddRange(collection);
			else if (list is IListWithNonReadOnlyRangeMethods<T> listWithRangeMethods)
				listWithRangeMethods.AddRange(collection);
			else
				list.GenericAddRange(collection);
		}

		internal static void GenericAddRange<T>(this IList<T> list, IEnumerable<T> collection)
		{
			if (collection is null)
				throw new ArgumentNullException(nameof(collection));
			foreach (var item in collection)
			{
				list.Add(item);
			}
		}

		// Generic IList<T> equivalent to List<T>.InsertRange(int, IEnumerable<T>).
		public static void InsertRange<T>(this IList<T> list, int index, IEnumerable<T> collection)
		{
			if (list is List<T> actualList)
				actualList.InsertRange(index, collection);
			else if (list is IListWithNonReadOnlyRangeMethods<T> listWithRangeMethods)
				listWithRangeMethods.InsertRange(index, collection);
			else
				list.GenericInsertRange(index, collection);
		}

		internal static void GenericInsertRange<T>(this IList<T> list, int index, IEnumerable<T> collection)
		{
			if (collection is null)
				throw new ArgumentNullException(nameof(collection));
			if (index < 0)
				throw new ArgumentOutOfRangeException($"index ({index}) cannot be < 0");
			var listCount = list.Count;
			if (index > listCount)
				throw new ArgumentOutOfRangeException($"startIndex ({index}) cannot be > list.Count ({listCount})");
			foreach (var item in collection)
			{
				list.Insert(index, item);
				index++;
			}
		}

		// Generic IList<T> equivalent to List<T>.RemoveRange(int, int).
		public static void RemoveRange<T>(this IList<T> list, int index, int count)
		{
			if (list is List<T> actualList)
				actualList.RemoveRange(index, count);
			else if (list is IListWithNonReadOnlyRangeMethods<T> listWithRangeMethods)
				listWithRangeMethods.RemoveRange(index, count);
			else
				list.GenericRemoveRange(index, count);
		}

		internal static void GenericRemoveRange<T>(this IList<T> list, int index, int count)
		{
			if (index < 0)
				throw new ArgumentOutOfRangeException($"index ({index}) cannot be < 0");
			if (count < 0)
				throw new ArgumentOutOfRangeException($"count ({count}) cannot be < 0");
			var listCount = list.Count;
			if (index > listCount - count)
				throw new ArgumentOutOfRangeException($"startIndex ({index}) + count ({count}) cannot be > list.Count ({listCount})");
			var endIndex = index + count - 1;
			while (endIndex >= index)
			{
				list.RemoveAt(endIndex);
				endIndex--;
			}
		}

		// Catch-all GetRangeView for lists that at compile-time do not have a GetRangeView method.
		// For example, a statically-typed IList<T> that has runtime-type of ChainedRefList<T> would use this extension method,
		// despite ChainedRefList<T> having its own GetRangeView method (that would return IRefList<T> instead of IListEx<T>).
		// Regardless of return type of the runtime-type's GetRangeView method, this extension method returns an IListEx<T>.
		public static IListEx<T> GetRangeView<T>(this IList<T> list, int index, int count)
		{
			// Due to TList in IListWithRangeView<T, out TList> being covariant, the following does match any IListWithRangeView<T, TList>
			// where TList implements IListEx<T>.
			if (list is IListWithRangeView<T, IListEx<T>> listWithRangeMethods)
				return listWithRangeMethods.GetRangeView(index, count);
			if (count < 0)
				throw new ArgumentOutOfRangeException($"count ({count}) cannot be < 0");
			if (index < 0)
				throw new ArgumentOutOfRangeException($"index ({index}) was out of range.");
			if (index + count > list.Count)
				throw new ArgumentOutOfRangeException($"index ({index}) + count ({count}) was out of range.");
			if (list is List<T> actualList)
				return new ListView<T>(actualList, index, count);
			if (list is T[] actualArray)
				return new ArrayRefListView<T>(actualArray, index, count);
			return new GenericListView<T>(list, index, count);
		}

		// It's not safe to have a GetRangeView overload for IReadOnlyList due to the fact that it would cause ambiguous call errors for lists
		// that implement both IList and IReadOnlyList (and most IReadOnlyList implementations also implement IList).
		// Furthermore, the IReadOnlyList interface was only introduced in .NET Framework 4.5.
		// Nonetheless, we can implement GetRangeView overloads for the common readonly list implementations.
		// ReadOnlyCollection<T> is returned by Array.AsReadOnly and List<T>.AsReadOnly methods.
		public static IReadOnlyListEx<T> GetRangeView<T>(this ReadOnlyCollection<T> readOnlyCollection, int index, int count)
		{
			// XXX: ReadOnlyCollection is very light wrapper. Nonetheless, I'm deciding that the tradeoff between a one-time reflection cost
			// versus an extra layer of indirection implicit in ReadOnlyCollection is worth it in favor of the one-time reflection.
			// Also, I'm accessing the private list field within ReadOnlyCollection rather than the protected Items get accessor,
			// since the former involves only a single reflection operation, and due to serialization compatibility, is required to exist.
			var listField = typeof(ReadOnlyCollection<T>).GetField("list", BindingFlags.Instance | BindingFlags.NonPublic);
			var list = (IList<T>)listField.GetValue(readOnlyCollection);
			if (count < 0)
				throw new ArgumentOutOfRangeException($"count ({count}) cannot be < 0");
			if (index < 0)
				throw new ArgumentOutOfRangeException($"index ({index}) was out of range.");
			if (index + count > list.Count)
				throw new ArgumentOutOfRangeException($"index ({index}) + count ({count}) was out of range.");
			return new GenericReadOnlyListView<T>(list, index, count);
		}
	}

	struct GenericListEnumerator<T> : IListEnumerator<T>
	{
		readonly IList<T> list;
		readonly int startIndex;
		readonly int endIndex;
		int index;

		public GenericListEnumerator(IList<T> list, int startIndex, int endIndex)
		{
			this.list = list;
			this.startIndex = startIndex;
			this.endIndex = endIndex;
			index = startIndex - 1;
		}

		public T Current => list[index];

		object IEnumerator.Current => list[index];

		public int CurrentIndex => index - startIndex;

		public bool MoveNext() => ++index < endIndex;

		public void Dispose()
		{
		}

		void IEnumerator.Reset() => index = startIndex - 1;
	}

	abstract class AbstractListView<T, TList> : IList<T>, IList, IListWithReadOnlyRangeMethods<T>, IListWithListEnumerator<T> where TList : IList<T>
	{
		protected readonly TList list;
		protected readonly int indexOffset;
		protected readonly int count;

		protected AbstractListView(TList list, int indexOffset, int count)
		{
			this.list = list;
			this.indexOffset = indexOffset;
			this.count = count;
		}

		public T this[int index]
		{
			get
			{
				CheckValidIndex(index);
				return list[index];
			}
			set
			{
				CheckValidIndex(index);
				list[index] = value;
			}
		}

		public int Count => count;

		public abstract bool IsReadOnly { get; }

		protected abstract bool IsFixedSize { get; }

		bool IList.IsReadOnly => IsReadOnly;

		bool IList.IsFixedSize => IsFixedSize;

		int ICollection.Count => Count;

		object ICollection.SyncRoot => ((ICollection)list).SyncRoot;

		bool ICollection.IsSynchronized => false;

		object IList.this[int index]
		{
			get => this[index];
			set => this[index] = (T)value;
		}

		public abstract void Add(T item);

		protected void CheckValidIndex(int index)
		{
			if (index < 0)
				throw new ArgumentOutOfRangeException($"index ({index}) was out of range.");
			if (index >= count)
				throw new ArgumentOutOfRangeException($"index ({index}) was out of range.");
		}

		protected void CheckValidRange(int index, int count)
		{
			if (count < 0)
				throw new ArgumentOutOfRangeException($"count ({count}) cannot be < 0");
			if (index < 0)
				throw new ArgumentOutOfRangeException($"index ({index}) was out of range.");
			if (index + count > Count)
				throw new ArgumentOutOfRangeException($"index ({index}) + count ({count}) was out of range.");
		}

		public abstract void Clear();

		public bool Contains(T item)
		{
			var endIndex = indexOffset + count;
			if (item == null)
			{
				for (var index = indexOffset; index < endIndex; index++)
				{
					if (list[index] == null)
						return true;
				}
				return false;
			}
			EqualityComparer<T> comparer = EqualityComparer<T>.Default;
			for (var index = indexOffset; index < endIndex; index++)
			{
				if (comparer.Equals(list[index], item))
					return true;
			}
			return false;
		}

		// Note: Not delegating to CopyTo(index: 0, array, arrayIndex, count: count) since the latter may have unnecessary validation checks
		// for index and count.
		public abstract void CopyTo(T[] array, int arrayIndex);

		public abstract void CopyTo(int index, T[] array, int arrayIndex, int count);

		public IEnumerator<T> GetEnumerator() => GetListEnumerator();

		public IListEnumerator<T> GetListEnumerator() => new GenericListEnumerator<T>(list, indexOffset, indexOffset + count);

		public abstract List<T> GetRange(int index, int count);

		public int IndexOf(T item) => list.IndexOf(item, indexOffset, count);

		public abstract void Insert(int index, T item);

		public abstract bool Remove(T item);

		public abstract void RemoveAt(int index);

		int IList.Add(object value)
		{
			Add((T)value);
			return Count - 1;
		}

		bool IList.Contains(object value) => Contains((T)value);

		void IList.Clear() => Clear();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		int IList.IndexOf(object value) => IndexOf((T)value);

		void IList.Insert(int index, object value) => Insert(index, (T)value);

		void IList.Remove(object value) => Remove((T)value);

		void IList.RemoveAt(int index) => RemoveAt(index);

		void ICollection.CopyTo(Array array, int index) => CopyTo((T[])array, index);
	}

	sealed class ListView<T> : AbstractListView<T, List<T>>, IListEx<T>
	{
		internal ListView(List<T> list, int indexOffset, int count) : base(list, indexOffset, count)
		{
		}

		public override bool IsReadOnly => false;

		protected override bool IsFixedSize => false;

		public override void Add(T item) => list.Insert(indexOffset + count, item);

		public void AddRange(IEnumerable<T> collection) => list.InsertRange(indexOffset + count, collection);

		public override void Clear() => list.RemoveRange(indexOffset, count);

		public override void CopyTo(T[] array, int arrayIndex) => list.CopyTo(indexOffset, array, arrayIndex, count);

		public override void CopyTo(int index, T[] array, int arrayIndex, int count)
		{
			CheckValidRange(index, count);
			list.CopyTo(indexOffset + index, array, arrayIndex, count);
		}

		public override List<T> GetRange(int index, int count)
		{
			CheckValidRange(index, count);
			return list.GetRange(indexOffset + index, count);
		}

		public IListEx<T> GetRangeView(int index, int count)
		{
			if (index == 0 && count == Count)
				return this;
			CheckValidRange(index, count);
			return new ListView<T>(list, indexOffset + index, count);
		}

		public override void Insert(int index, T item)
		{
			CheckValidIndex(index);
			list.Insert(indexOffset + index, item);
		}

		public void InsertRange(int index, IEnumerable<T> collection)
		{
			CheckValidIndex(index);
			list.InsertRange(indexOffset + index, collection);
		}

		public override bool Remove(T item)
		{
			int index = list.IndexOf(item, indexOffset, count);
			if (index >= 0)
			{
				list.RemoveAt(index);
				return true;
			}
			return false;
		}

		public override void RemoveAt(int index)
		{
			CheckValidIndex(index);
			list.RemoveAt(indexOffset + index);
		}

		public void RemoveRange(int index, int count)
		{
			CheckValidRange(index, count);
			list.RemoveRange(indexOffset + index, count);
		}
	}

	sealed class GenericListView<T> : AbstractListView<T, IList<T>>, IListEx<T>
	{
		internal GenericListView(IList<T> list, int indexOffset, int count) : base(list, indexOffset, count)
		{
		}

		public override bool IsReadOnly => list.IsReadOnly;

		protected override bool IsFixedSize => ((IList)list).IsFixedSize;

		public override void Add(T item) => list.Insert(indexOffset + count, item);

		public void AddRange(IEnumerable<T> collection) => list.GenericInsertRange(indexOffset + count, collection);

		public override void Clear() => list.GenericRemoveRange(indexOffset, count);

		public override void CopyTo(T[] array, int arrayIndex) => list.GenericCopyTo(indexOffset, array, arrayIndex, count);

		public override void CopyTo(int index, T[] array, int arrayIndex, int count)
		{
			CheckValidRange(index, count);
			list.GenericCopyTo(indexOffset + index, array, arrayIndex, count);
		}

		public override List<T> GetRange(int index, int count)
		{
			CheckValidRange(index, count);
			return list.GenericGetRange(indexOffset + index, count);
		}

		public IListEx<T> GetRangeView(int index, int count)
		{
			if (index == 0 && count == Count)
				return this;
			CheckValidRange(index, count);
			return new GenericListView<T>(list, indexOffset + index, count);
		}

		public override void Insert(int index, T item)
		{
			CheckValidIndex(index);
			list.Insert(indexOffset + index, item);
		}

		public void InsertRange(int index, IEnumerable<T> collection)
		{
			CheckValidIndex(index);
			list.GenericInsertRange(indexOffset + index, collection);
		}

		public override bool Remove(T item)
		{
			int index = list.IndexOf(item, indexOffset, count);
			if (index >= 0)
			{
				list.RemoveAt(index);
				return true;
			}
			return false;
		}

		public override void RemoveAt(int index)
		{
			CheckValidIndex(index);
			list.RemoveAt(indexOffset + index);
		}

		public void RemoveRange(int index, int count)
		{
			CheckValidRange(index, count);
			list.GenericRemoveRange(indexOffset + index, count);
		}
	}

	sealed class GenericReadOnlyListView<T> : AbstractListView<T, IList<T>>, IReadOnlyListEx<T>
	{
		internal GenericReadOnlyListView(IList<T> list, int indexOffset, int count) : base(list, indexOffset, count)
		{
		}

		public override bool IsReadOnly => true;

		protected override bool IsFixedSize => true;

		public override void Add(T item) => throw new NotSupportedException();

		public override void Clear() => throw new NotSupportedException();

		public override void CopyTo(T[] array, int arrayIndex) => list.GenericCopyTo(indexOffset, array, arrayIndex, count);

		public override void CopyTo(int index, T[] array, int arrayIndex, int count)
		{
			CheckValidRange(index, count);
			list.GenericCopyTo(indexOffset + index, array, arrayIndex, count);
		}

		public override List<T> GetRange(int index, int count)
		{
			CheckValidRange(index, count);
			return list.GenericGetRange(indexOffset + index, count);
		}

		public IReadOnlyListEx<T> GetRangeView(int index, int count)
		{
			if (index == 0 && count == Count)
				return this;
			CheckValidRange(index, count);
			return new GenericReadOnlyListView<T>(list, indexOffset + index, count);
		}

		public override void Insert(int index, T item) => throw new NotSupportedException();

		public override bool Remove(T item) => throw new NotSupportedException();

		public override void RemoveAt(int index) => throw new NotSupportedException();
	}
}
