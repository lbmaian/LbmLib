using System.Collections;
using System.Collections.Generic;

namespace LbmLib.Language
{
	public interface IRefList<T> : IList<T>, IList
	{
		ref T ItemRef(int index);

		new IRefListEnumerator<T> GetEnumerator();

		// Following are only necessary to eliminate ambiguity between IList<T>/ICollection<T> and IList/ICollection properties/methods.

		new T this[int index] { get; set; }

		new int Count { get; }

		new bool IsReadOnly { get; }

		new void Clear();

		new void RemoveAt(int index);
	}

	public interface IRefListEnumerator<T>
	{
		bool MoveNext();

		ref T Current { get; }

		int CurrentIndex { get; }
	}
}
