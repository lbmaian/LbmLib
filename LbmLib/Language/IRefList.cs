using System;
using System.Collections;
using System.Collections.Generic;

namespace LbmLib.Language
{
	/// <summary>
	/// Represents a list that has by-(readonly)-reference-returning <c>ItemRef</c> method and by-(readonly)-reference-yielding enumerator.
	/// <para>
	/// Base interface for <see cref="IRefList{T}"/> and <see cref="IRefReadOnlyList{T}"/>.
	/// The two derived interfaces are necessary since they have incompatible <c>ItemRef</c> property (<c>ref</c> vs <c>ref readonly</c>)
	/// and <c>GetEnumerator</c> method (<c>IRefEnumerator</c> vs <c>IRefReadOnlyListEnumerator</c>),
	/// the latter which in turn have incompatible <c>Current</c> property (<c>ref</c> vs <c>ref readonly</c>).
	/// </para>
	/// <para>
	/// Note: Classes should implement <c>IRefList</c> or <c>IRefReadOnlyList</c>, not <c>IBaseRefList</c> directly,
	/// as the <c>ItemRef</c> method and the <c>IRefListEnumerator/IRefReadOnlyListEnumerator GetEnumerator</c> are not on this interface itself.
	/// </para>
	/// <para>
	/// This interface is public primarily so that methods that can accept any type of reflist can "convert" to either a
	/// non-readonly (<c>IRefList</c>) or readonly (<c>IRefReadOnlyList</c>) variant as needed.
	/// For example, this is done in <see cref="ChainedRefListExtensions.ChainConcat{T}(IBaseRefList{T}, IBaseRefList{T})"/>.
	/// </para>
	/// </summary>
	/// <typeparam name="T">The type of elements in the list.</typeparam>
	public interface IBaseRefList<T> : IList<T>, IList, IListWithReadOnlyRangeMethods<T>
	{
		/// <summary>
		/// If <c>this</c> is an <see cref="IRefList{T}"/>, returns <c>this</c>.
		/// Else, returns an <see cref="IRefList{T}"/> variant of <c>this</c> if possible, or throws a <see cref="NotSupportedException"/>
		/// if not possible.
		/// </summary>
		/// <returns>Returns an <c>IRefList</c> variant of <c>this</c> if possible, or <c>this</c> if already an <c>IRefList</c>.</returns>
		/// <exception cref="NotSupportedException">
		/// Thrown when <c>this</c> is not already an <c>IRefList</c> and does not have a <c>IRefList</c> variant.
		/// </exception>
		IRefList<T> AsNonReadOnly();

		/// <summary>
		/// If <c>this</c> is an <see cref="IRefReadOnlyList{T}"/>, returns <c>this</c>.
		/// Else, returns an <see cref="IRefReadOnlyList{T}"/> variant of <c>this</c>.
		/// </summary>
		/// <returns>Returns an <c>IRefReadOnlyList</c> variant of <c>this</c>, or <c>this</c> if already an <c>IRefReadOnlyList</c>.</returns>
		IRefReadOnlyList<T> AsReadOnly();

		// Following are only necessary to eliminate ambiguity between IList<T>/ICollection<T> and IList/ICollection properties/methods.
		new T this[int index] { get; set; }
		new int Count { get; }
		new bool IsReadOnly { get; }
		new void Clear();
		new void RemoveAt(int index);
	}

	/// <summary>
	/// Represents a list that has by-reference-returning <c>ItemRef</c> method and by-reference-yielding enumerator.
	/// <para>
	/// Implementation assertions:
	/// <list type="bullet">
	///	<item><see cref="IBaseRefList{T}.IsReadOnly"/> returns <c>false</c>.</item>
	///	<item><see cref="IBaseRefList{T}.AsNonReadOnly"/> returns <c>this</c>.</item>
	///	<item>
	///	Should also implement <see cref="IListEx{T}"/>, explicitly implementing
	///	<see cref="IListWithRangeView{T, IListEx{T}}.GetRangeView(int, int)">IListWithRangeView&lt;T, IListEx&lt;T&gt;&gt;.GetRangeView</see>
	///	(this interface itself doesn't implement <see cref="IListEx{T}"/> since it would cause ambiguous call errors for <c>GetRangeView</c>).
	///	</item>
	/// </list>
	/// </para>
	/// </summary>
	/// <typeparam name="T">The type of elements in the list.</typeparam>
	public interface IRefList<T> : IBaseRefList<T>, IListWithRangeView<T, IRefList<T>>, IListWithListEnumerator<T>
	{
		// This isn't strictly necessary, but it provides parity with IRefReadOnlyList.
		// (It also allows typeof(IRefList<T>).GetProperty("Item") to return a property rather than null.)
		new T this[int index] { get; set; }

		/// <summary>
		/// Returns by reference to the element at the specified <paramref name="index"/>,
		/// which can be used like <see cref="this[int]"/> (the indexer) to get or set the element,
		/// or be used to pass on as by-ref <c>ref/out/in</c> parameter.
		/// </summary>
		/// <param name="index">The zero-based index of the element to get a reference to.</param>
		/// <returns>Returns by reference to the element at the specified index.</returns>
		ref T ItemRef(int index);

		/// <summary>Returns an <see cref="IRefListEnumerator{T}"/> variant of the enumerator.</summary>
		/// <returns>Returns an <c>IRefListEnumerator</c> variant of the enumerator.</returns>
		new IRefListEnumerator<T> GetEnumerator();
	}

	/// <summary>
	/// Represents an <see cref="IEnumerator{T}"/> variant for <see cref="IRefList{T}"/>,
	/// which <see cref="Current"/> property returns by reference to the current object/value.
	/// </summary>
	/// <typeparam name="T">The type of objects/values to enumerate.</typeparam>
	public interface IRefListEnumerator<T> : IListEnumerator<T>
	{
		/// <summary>
		/// Returns by reference to the current element, which can be used to get or set the element,
		/// or be used to pass on as a by-ref <c>ref/out/in</c> parameter.
		/// If the reference is set to a value, the change is reflected in <c>list[this.CurrentIndex]</c>,
		/// where <c>list</c> is the <c>IRefList</c> the element is from.
		/// </summary>
		new ref T Current { get; }
	}

	/// <summary>
	/// Represents a list that has by-readonly-reference-returning <c>ItemRef</c> method and by-readonly-reference-yielding enumerator.
	/// <para>
	/// Implementation assertions:
	/// <list type="bullet">
	/// <item><see cref="this[int]"/> set accessor throws <see cref="NotSupportedException"/>.</item>
	///	<item><see cref="IBaseRefList{T}.IsReadOnly"/> returns <c>true</c>.</item>
	///	<item><see cref="IBaseRefList{T}.AsReadOnly"/> returns <c>this</c>.</item>
	///	<item><see cref="IBaseRefList{T}.AsNonReadOnly"/> throws <see cref="NotSupportedException"/></item>
	///	<item>
	///	Should also implement <see cref="IReadOnlyListEx{T}"/>, explicitly implementing
	///	<see cref="IListWithRangeView{T, IReadOnlyListEx{T}}.GetRangeView(int, int)">
	///	IListWithRangeView&lt;T, IReadOnlyListEx&lt;T&gt;&gt;.GetRangeView</see>
	///	(this interface itself doesn't implement <see cref="IReadOnlyListEx{T}"/>
	///	since it would cause ambiguous call errors for <c>GetRangeView</c>).
	///	</item>
	/// </list>
	/// </para>
	/// </summary>
	/// <typeparam name="T">The type of elements in the list.</typeparam>
	public interface IRefReadOnlyList<T> : IBaseRefList<T>, IListWithRangeView<T, IRefReadOnlyList<T>>, IListWithListEnumerator<T>
	{
		// For simple compile-time type safety, hide the indexer set accessor.
		// This isn't foolproof since an instance of this interface can be cast to IBaseRefList<T> or IList<T>,
		// which doesn't hide the indexer set accessor. Thus:
		// Assertion: indexer set accessor throws NotSupportedException.
		new T this[int index] { get; }

		/// <summary>
		/// Returns by readonly reference to the element at the specified <paramref name="index"/>,
		/// which can be used like <see cref="this[int]"/> (the indexer) to get or set the element,
		/// or be used to pass on as a by-ref <c>in</c> parameter.
		/// </summary>
		/// <param name="index">The zero-based index of the element to return by readonly reference to.</param>
		/// <returns>Returns by readonly reference to the element at the specified index.</returns>
		ref readonly T ItemRef(int index);

		/// <summary>Returns an <see cref="IRefReadOnlyListEnumerator{T}"/> variant of the enumerator.</summary>
		/// <returns>Returns an <c>IRefReadOnlyListEnumerator</c> variant of the enumerator.</returns>
		new IRefReadOnlyListEnumerator<T> GetEnumerator();

#if !NET35
		// Following are only necessary to eliminate ambiguity between IBaseRefList<T> and
		// IReadOnlyList<T>/IReadOnlyCollection<T> properties/methods.
		new int Count { get; }
#endif
	}

	/// <summary>
	/// Represents an <see cref="IEnumerator{T}"/> variant for <see cref="IRefReadOnlyList{T}"/>,
	/// which <see cref="Current"/> property returns by readonly reference to the current object/value.
	/// </summary>
	/// <typeparam name="T">The type of objects/values to enumerate.</typeparam>
	public interface IRefReadOnlyListEnumerator<T> : IListEnumerator<T>
	{
		/// <summary>
		/// Returns by readonly reference to the current element, which can be used to get the element,
		/// or be used to pass on as a by-ref <c>in</c> parameter.
		/// </summary>
		new ref readonly T Current { get; }
	}
}
