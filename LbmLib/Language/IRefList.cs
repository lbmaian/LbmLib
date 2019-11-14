using System.Collections;
using System.Collections.Generic;

namespace LbmLib.Language
{
	// Base interface for IRefList and IRefReadOnlyList.
	// The two derived interfaces are necessary since they have incompatible ItemRef property (ref vs ref readonly) and GetEnumerator method
	// (IRefEnumerator vs IRefReadOnlyListEnumerator), the latter which in turn have incompatible Current property (ref vs ref readonly).
	// Note: Classes should implement IRefList or IRefReadOnlyList, not IBaseRefList directly,
	// as ItemRef and the IRefListEnumerator/IRefReadOnlyListEnumerator GetEnumerator are not on this interface itself.
	public interface IBaseRefList<T> : IList<T>, IList
	{
		IRefList<T> AsNonReadOnly();

		IRefReadOnlyList<T> AsReadOnly();

		// Following are only necessary to eliminate ambiguity between IList<T>/ICollection<T> and IList/ICollection properties/methods.
		new T this[int index] { get; set; }
		new int Count { get; }
		new bool IsReadOnly { get; }
		new void Clear();
		new void RemoveAt(int index);
	}

	public interface IBaseRefListEnumerator<T>
	{
		bool MoveNext();

		int CurrentIndex { get; }
	}

	public interface IRefList<T> : IBaseRefList<T>
	{
		// This isn't strictly necessary, but it provides parity with IRefReadOnlyList.
		// (It also allows typeof(IRefList<T>).GetProperty("Item") to return a property rather than null.)
		new T this[int index] { get; set; }

		ref T ItemRef(int index);

		new IRefListEnumerator<T> GetEnumerator();

		// Assertion: IsReadOnly returns false.
		// Assertion: AsNonReadOnly returns this.
	}

	public interface IRefListEnumerator<T> : IBaseRefListEnumerator<T>
	{
		ref T Current { get; }
	}

	public interface IRefReadOnlyList<T> : IBaseRefList<T>
#if !NET35
		, IReadOnlyList<T>
#endif
	{
		// For simple compile-time type safety, hide the indexer set accessor.
		// This isn't foolproof since an instance of this interface can be cast to IBaseRefList<T> or IList<T>,
		// which doesn't hide the indexer set accessor. Thus:
		// Assertion: indexer set accessor throws NotSupportedException.
		new T this[int index] { get; }

		ref readonly T ItemRef(int index);

		new IRefReadOnlyListEnumerator<T> GetEnumerator();

		// Assertion: IsReadOnly returns true.
		// Assertion: AsReadOnly returns this object.
		// Assertion: AsNonReadOnly throws NotSupportedException.

#if !NET35
		// Following are only necessary to eliminate ambiguity between IBaseRefList<T> and IReadOnlyList<T>/IReadOnlyCollection<T> properties/methods.
		new int Count { get; }
#endif
	}

	public interface IRefReadOnlyListEnumerator<T> : IBaseRefListEnumerator<T>
	{
		ref readonly T Current { get; }
	}
}
