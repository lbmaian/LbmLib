using System.Collections.Generic;

namespace LbmLib.Language
{
	/// <summary>
	/// Represents a variant of <see cref="IEnumerator{T}"/> with an additional <see cref="CurrentIndex"/> property for <see cref="IList{T}"/>s.
	/// </summary>
	/// <typeparam name="T">The type of objects/values to enumerate.</typeparam>
	public interface IListEnumerator<T> : IEnumerator<T>
	{
		/// <summary>Returns the zero-based index of the current element in the <see cref="IList{T}"/> it is from.</summary>
		int CurrentIndex { get; }
	}

	/// <summary>
	/// Provides a variant of <see cref="IEnumerable{T}.GetEnumerator"/> that returns a <see cref="IListEnumerator{T}"/>.
	/// </summary>
	/// <typeparam name="T">The type of elements in the list.</typeparam>
	public interface IListWithListEnumerator<T> : IList<T>
	{
		/// <summary>
		/// An <see cref="IListEnumerator{T}"/> variant of <see cref="IEnumerable{T}.GetEnumerator"/>
		/// that includes a <see cref="IListEnumerator{T}.CurrentIndex"/> property.
		/// </summary>
		/// <returns></returns>
		IListEnumerator<T> GetListEnumerator();
	}
}
