using System.Collections.Generic;

namespace LbmLib.Language
{
	/// <summary>
	/// Provides methods that operate on list ranges and do <em>not</em> modify instances of this implementation,
	/// equivalent to such methods on <see cref="List{T}"/>, such as <see cref="List{T}.GetRange(int, int)"/>.
	/// <para>
	/// The <see cref="ListMethodsAsCollectionsInterfacesExtensions.GetRange{T}(IList{T}, int, int)"/> extension method
	/// is a generic implementation of the <c>GetRange(int, int)</c> method, delegating to
	/// <see cref="List{T}.GetRange(int, int)"/> if the list is a <see cref="List{T}"/>, delegating to this interface's
	/// <see cref="GetRange(int, int)"/> if the list implements this interface, or a defaulting to a generic implementation
	/// that works on any <c>IList</c>.
	/// </para>
	/// </summary>
	/// <typeparam name="T">The type of elements in the list.</typeparam>
	/// <remarks>
	/// Ideally would be implemented as a default interface method, but default interface methods requires .NET Core 3.0+.
	/// </remarks>
	public interface IListWithReadOnlyRangeMethods<T> : IList<T>
	{
		/// <summary>
		/// Same interface contract as <see cref="List{T}.GetRange(int, int)"/> but for implementations of this interface.
		/// <para>
		/// This could have a more optimized implementation then using <see cref="IList{T}.this[int]"/> repeatedly,
		/// as the <see cref="ListRangeExtensions.GetRange{T}(IList{T}, int, int)"/> method's
		/// default implementation does (in addition to the creation of a new <see cref="List{T}"/>).
		/// </para>
		/// <para>
		/// If the class also implements <see cref="IListWithRangeView{T, TList}"/>, use
		/// <see cref="IListWithRangeView{T, TList}.GetRangeView(int, int)"/> if you need a view instead of a copy of the list range.
		/// </para>
		/// </summary>
		List<T> GetRange(int index, int count);

		/// <summary>
		/// Same interface constract as <see cref="List{T}.CopyTo(int, T[], int, int)"/> but for implementations of this interface.
		/// <para>
		/// (Although not named like a <c>*Range</c> method, this is effectively a variant of <see cref="GetRange(int, int)"/> that
		/// populates an existing <paramref name="array"/> rather than a new <see cref="List{T}"/> from a list range.)
		/// </para>
		/// <para>
		/// This could have a more optimized implementation then using <see cref="IList{T}.this[int]"/> repeatedly,
		/// as the <see cref="ListRangeExtensions.CopyTo{T}(IList{T}, int, T[], int, int)"/> method's
		/// default implementation does (in addition to the creation of a new array>).
		/// </para>
		/// </summary>
		/// <param name="index"></param>
		/// <param name="array"></param>
		/// <param name="arrayIndex"></param>
		/// <param name="count"></param>
		void CopyTo(int index, T[] array, int arrayIndex, int count);
	}

	/// <summary>
	/// Provides methods that operate on list ranges and modify instances of this interface,
	/// equivalent to such methods on <see cref="List{T}"/>, such as <see cref="List{T}.RemoveRange(int, int)"/>.
	/// <para>
	/// <see cref="ListRangeExtensions"/> provides <see cref="IList{T}"/> extension methods
	/// that generically implement methods in this interface
	/// (e.g. <see cref="ListRangeExtensions.RemoveRange{T}(IList{T}, int, int)"/>), delegating to the
	/// <see cref="List{T}"/> method (e.g. <see cref="List{T}.RemoveRange(int, int)"/> if the list is a <see cref="List{T}"/>,
	/// delegating to this interface's method (e.g. <see cref="RemoveRange(int, int)"/>) if the list implements this interface,
	/// or defaulting to a generic implementation that works on any <c>IList</c>.
	/// </para>
	/// </summary>
	/// <typeparam name="T">The type of elements in the list.</typeparam>
	/// <remarks>
	/// Ideally would be implemented as default interface methods, but default interface methods requires .NET Core 3.0+.
	/// </remarks>
	public interface IListWithNonReadOnlyRangeMethods<T> : IList<T>
	{
		/// <summary>
		/// Same interface contract as <see cref="List{T}.AddRange(IEnumerable{T})"/>
		/// but for implementations of this interface.
		/// <para>
		/// This could have a more optimized implementation then using <see cref="ICollection{T}.Add(T)"/> repeatedly,
		/// as the <see cref="ListRangeExtensions.AddRange{T}(IList{T}, IEnumerable{T})"/> method's
		/// default implementation does.
		/// </para>
		/// </summary>
		void AddRange(IEnumerable<T> collection);

		/// <summary>
		/// Same interface contract as <see cref="List{T}.InsertRange(int, IEnumerable{T})"/>
		/// but for implementations of this interface.
		/// <para>
		/// This could have a more optimized implementation then using <see cref="IList{T}.Insert(int, T)"/> repeatedly,
		/// as the <see cref="ListRangeExtensions.InsertRange{T}(IList{T}, int, IEnumerable{T})"/> method's
		/// default implementation does.
		/// </para>
		/// </summary>
		void InsertRange(int index, IEnumerable<T> collection);

		/// <summary>
		/// Same interface contract as <see cref="List{T}.RemoveRange(int, int)"/>
		/// but for implementations of this interface.
		/// <para>
		/// This could have a more optimized implementation then using <see cref="IList{T}.RemoveAt(int)"/> repeatedly,
		/// as the <see cref="ListRangeExtensions.RemoveRange{T}(IList{T}, int, int)"/> method's
		/// default implementation does.
		/// </para>
		/// </summary>
		void RemoveRange(int index, int count);
	}

	/// <summary>
	/// Provides a <see cref="GetRangeView(int, int)"/> method that returns a "view" of a list range.
	/// This contrasts with the <see cref="IListWithReadOnlyRangeMethods{T}.GetRange(int, int)"/> method that returns a copy of a list range.
	/// </summary>
	/// <typeparam name="T">The type of elements in the list.</typeparam>
	/// <typeparam name="TList">The type of list that is returned by <see cref="GetRangeView(int, int)"/>.</typeparam>
	public interface IListWithRangeView<T, out TList> : IList<T> where TList : IList<T>
	{
		/// <summary>
		/// Returns a "view" of a range of this list, where mutations to the view, including element additions and removals,
		/// are reflected in this list.
		/// <para>
		/// The semantics of the returned view become undefined if the backing list (this list) has
		/// insertions or removals done in any way other than via the returned view. For example:
		/// <code>
		/// var view = list.GetRangeView(4, 10);
		/// var valAtIndex4 = view[0];
		/// list.RemoveAt(3);
		/// var valAtSomeIndex = view[0];
		/// // valAtSomeIndex is not guaranteed to refer to the original list[4] anymore,
		/// // nor is it guaranteed to refer to the current list[4].
		/// </code>
		/// </para>
		/// <para>
		/// Implementations may return this list if the view would completely encompass it,
		/// i.e. <c><paramref name="index"/> == 0</c> and <c><paramref name="count"/> == <see cref="ICollection{T}.Count"/></c>.
		/// </para>
		/// <para>
		/// If the class also implements <see cref="IListWithReadOnlyRangeMethods{T}"/>, use
		/// <see cref="IListWithReadOnlyRangeMethods{T}.GetRange(int, int)"/> if you need a copy instead of a view of the list range.
		/// </para>
		/// <para>
		/// This method is inspired from Java's <c>java.util.List.subList</c> method.
		/// </para>
		/// </summary>
		/// <returns>Returns a "view" of a range of this list.</returns>
		/// <param name="index">
		/// 0th index of the view maps to <c>this[index]</c>,
		/// such that <c>index</c> is effectively the inclusive start index.
		/// </param>
		/// <param name="count">
		/// <c>Count</c> of the view maps to <c>this[index + count]</c>,
		/// such that <c>index + count</c> is effectively the exclusive end index.
		/// </param>
		TList GetRangeView(int index, int count);
	}
}
