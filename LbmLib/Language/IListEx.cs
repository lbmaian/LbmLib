using System.Collections;
using System.Collections.Generic;

namespace LbmLib.Language
{
	/// <summary>
	/// Represents a non-readonly <see cref="IList{T}"/> with all <see cref="List{T}"/><c>.*Range</c> methods and an
	/// <see cref="IListEx{T}"/>-returning <see cref="IListWithRangeView{T, TList}.GetRangeView(int, int)"/> method.
	/// <para>
	/// This is provided mostly more convenience, and ideally shouldn't exist, but C# lacks intersection types ala Java,
	/// such that it's impossible to have as a return type: <c>IList&lt;T&gt; &amp; IListWithReadOnlyRangeMethods&lt;T&gt; &amp;
	/// IListWithRangeView&lt;T, IListEx&lt;T&gt;&gt; &amp; IListWithNonReadOnlyRangeMethods&lt;T&gt;</c>.
	/// </para>
	/// </summary>
	/// <typeparam name="T">The type of elements in the list.</typeparam>
	public interface IListEx<T> : IList<T>, IList, IListWithReadOnlyRangeMethods<T>, IListWithRangeView<T, IListEx<T>>,
		IListWithListEnumerator<T>, IListWithNonReadOnlyRangeMethods<T>
	{
	}

	/// <summary>
	/// Represents a readonly <see cref="IList{T}"/> with the <see cref="List{T}.GetRange(int, int)"/> method
	/// (the only non-mutable <see cref="List{T}"/><c>*Range</c> method) and an <see cref="IReadOnlyListEx{T}"/>-returning
	/// <see cref="IListWithRangeView{T, TList}.GetRangeView(int, int)"/> method..
	/// <para>
	/// This is provided mostly more convenience, and ideally shouldn't exist, but C# lacks intersection types ala Java,
	/// such that it's impossible to have as a return type: <c>IList&lt;T&gt; &amp; IListWithReadOnlyRangeMethods&lt;T&gt; &amp;
	/// IListWithRangeView&lt;T, IReadOnlyListEx&lt;T&gt;&gt;</c>.
	/// </para>
	/// </summary>
	/// <typeparam name="T">The type of elements in the list.</typeparam>
	public interface IReadOnlyListEx<T> : IList<T>, IList, IListWithReadOnlyRangeMethods<T>, IListWithRangeView<T, IReadOnlyListEx<T>>,
		IListWithListEnumerator<T>
#if !NET35
		// Note: IReadOnlyList does not imply that the list is readonly. Rather, it provides a readonly view on the list.
		// It's generally not a good idea to have classes implement IReadOnlyList and not implement IList,
		// since most methods, including extension methods, require IList parameters even if they don't modify the list
		// (and having method overloads for both IList and IReadOnlyList tends to cause ambiguous call errors).
		// Even List (a definitely mutable list) and ReadOnlyCollection (a definitely readonly list) classes implement both
		// IList and IReadOnlyList!
		, IReadOnlyList<T>
#endif
	{
	}
}
