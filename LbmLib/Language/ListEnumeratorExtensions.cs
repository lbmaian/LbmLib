using System.Collections;
using System.Collections.Generic;

namespace LbmLib.Language
{
	public static class ListEnumeratorExtensions
	{
		public static IListEnumerator<T> GetListEnumerator<T>(this IList<T> list)
		{
			if (list is IListWithListEnumerator<T> listWithListEnumerator)
				return listWithListEnumerator.GetListEnumerator();
			return new WrapperListEnumerator<T>(list.GetEnumerator(), 0);
		}
	}

	struct WrapperListEnumerator<T> : IListEnumerator<T>
	{
		readonly IEnumerator<T> enumerator;
		readonly int startIndex;
		int index;

		public WrapperListEnumerator(IEnumerator<T> enumerator, int startIndex) : this()
		{
			this.enumerator = enumerator;
			this.startIndex = startIndex;
			index = startIndex - 1;
		}

		public T Current => enumerator.Current;

		object IEnumerator.Current => enumerator.Current;

		public int CurrentIndex => index - startIndex;

		public bool MoveNext()
		{
			++index;
			return enumerator.MoveNext();
		}

		public void Dispose()
		{
			enumerator.Dispose();
		}

		void IEnumerator.Reset()
		{
			index = startIndex - 1;
			enumerator.Reset();
		}
	}
}
