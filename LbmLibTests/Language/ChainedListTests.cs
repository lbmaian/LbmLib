using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace LbmLib.Language.Tests
{
	[TestFixture]
	public class ChainedListTests
	{
		[Test]
		public void ChainConcat_Lists()
		{
			var left = new List<string> { "a", "b", "c" };
			var right = new List<string> { "c", "d" };
			var chained = left.ChainConcat(right);
			Assert.AreEqual(typeof(ChainedList<string>), chained.GetType());
			Assert.IsFalse(chained.IsReadOnly);
			Assert.IsFalse(ChainedListExtensions.IsFixedSize(chained));
			AssertChainedListSanity(left, right, chained);
			CollectionAssert.AreEqual(new[] { "a", "b", "c", "c", "d" }, chained);
			Assert.AreEqual(chained.GetType().ToString(), chained.ToString());
			chained.Add("e");
			AssertChainedListSanity(left, right, chained);
			Assert.AreEqual(2, chained.IndexOf("c"));
			Assert.AreEqual(4, chained.IndexOf("d"));
			Assert.AreEqual(-1, chained.IndexOf("nothere"));
			Assert.IsTrue(chained.Remove("c"));
			AssertChainedListSanity(left, right, chained);
			Assert.IsTrue(chained.Remove("c"));
			AssertChainedListSanity(left, right, chained);
			Assert.IsFalse(chained.Remove("nothere"));
			AssertChainedListSanity(left, right, chained);
			chained.Insert(3, "d2");
			AssertChainedListSanity(left, right, chained);
			chained.Insert(2, "c2");
			AssertChainedListSanity(left, right, chained);
			chained.Insert(chained.Count, "f");
			AssertChainedListSanity(left, right, chained);
			Assert.Throws(typeof(ArgumentOutOfRangeException), () => chained.Insert(chained.Count + 1, "error"));
			CollectionAssert.AreEqual(new[] { "a", "b", "c2" }, left);
			CollectionAssert.AreEqual(new[] { "d", "d2", "e", "f" }, right);
			chained.RemoveAt(2);
			AssertChainedListSanity(left, right, chained);
			chained.RemoveAt(2);
			AssertChainedListSanity(left, right, chained);
			Assert.Throws(typeof(ArgumentOutOfRangeException), () => chained.RemoveAt(chained.Count));
			CollectionAssert.AreEqual(new[] { "a", "b" }, left);
			CollectionAssert.AreEqual(new[] { "d2", "e", "f" }, right);
			var array1 = new string[chained.Count];
			chained.CopyTo(array1, 0);
			CollectionAssert.AreEqual(new[] { "a", "b", "d2", "e", "f" }, array1);
			var array2 = new string[chained.Count + 2];
			chained.CopyTo(array2, 2);
			CollectionAssert.AreEqual(new[] { null, null, "a", "b", "d2", "e", "f" }, array2);
			Assert.Throws(typeof(ArgumentOutOfRangeException), () => _ = chained[-1]);
			Assert.Throws(typeof(ArgumentOutOfRangeException), () => _ = chained[chained.Count]);
			chained[1] = "b2";
			chained[2] = "d3";
			Assert.Throws(typeof(ArgumentOutOfRangeException), () => chained[-1] = "error");
			Assert.Throws(typeof(ArgumentOutOfRangeException), () => chained[chained.Count] = "error");
			AssertChainedListSanity(left, right, chained);
			CollectionAssert.AreEqual(new[] { "a", "b2" }, left);
			CollectionAssert.AreEqual(new[] { "d3", "e", "f" }, right);
			chained.Clear();
			AssertChainedListSanity(left, right, chained);
			CollectionAssert.AreEqual(new string[0], left);
			CollectionAssert.AreEqual(new string[0], right);
			CollectionAssert.AreEqual(new string[0], chained);
		}

		[Test]
		public void ChainConcat_Arrays()
		{
			IList<string> left, right;
			left = new[] { "a", "b", "c" };
			right = new[] { "c", "d" };
			CommonFixedSizeOrReadOnlyChainListTests(left, right, left.ChainConcat(right), typeof(ChainedFixedSizeRefList<string>));
			left = new[] { "a", "b", "c" };
			right = new List<string>() { "c", "d" };
			CommonFixedSizeOrReadOnlyChainListTests(left, right, left.ChainConcat(right), typeof(ChainedFixedSizeList<string>));
			left = new List<string>() { "a", "b", "c" };
			right = new[] { "c", "d" };
			CommonFixedSizeOrReadOnlyChainListTests(left, right, left.ChainConcat(right), typeof(ChainedFixedSizeList<string>));
		}

		[Test]
		public void ChainConcat_ReadOnlyLists()
		{
			var left = new List<string>() { "a", "b", "c" };
			var right = new List<string>() { "c", "d" };
			var leftReadOnly = left.AsReadOnly();
			var rightReadOnly = right.AsReadOnly();
			CommonFixedSizeOrReadOnlyChainListTests(left, rightReadOnly, left.ChainConcat(rightReadOnly), typeof(ChainedReadOnlyList<string>));
			CommonFixedSizeOrReadOnlyChainListTests(leftReadOnly, right, leftReadOnly.ChainConcat(right), typeof(ChainedReadOnlyList<string>));
			CommonFixedSizeOrReadOnlyChainListTests(leftReadOnly, rightReadOnly, leftReadOnly.ChainConcat(rightReadOnly), typeof(ChainedReadOnlyList<string>));
		}

		static void CommonFixedSizeOrReadOnlyChainListTests(IList<string> left, IList<string> right, IList<string> chained, Type expectedChainedListType)
		{
			Assert.AreEqual(expectedChainedListType, chained.GetType());
			var isReadOnly = expectedChainedListType.Name.Contains("ReadOnly");
			Assert.AreEqual(ChainedListExtensions.IsReadOnly(left) || ChainedListExtensions.IsReadOnly(right), isReadOnly);
			Assert.AreEqual(chained.IsReadOnly, isReadOnly);
			Assert.IsTrue(ChainedListExtensions.IsFixedSize(left) || ChainedListExtensions.IsFixedSize(right));
			Assert.IsTrue(ChainedListExtensions.IsFixedSize(chained));
			AssertChainedListSanity(left, right, chained);
			CollectionAssert.AreEqual(new[] { "a", "b", "c", "c", "d" }, chained);
			Assert.AreEqual(chained.GetType().ToString(), chained.ToString());
			AssertThrowsOutOfRangeException(() => _ = chained[-1]);
			AssertThrowsOutOfRangeException(() => _ = chained[chained.Count]);
			Assert.Throws(typeof(NotSupportedException), () => chained.Add("e"));
			Assert.AreEqual(2, chained.IndexOf("c"));
			Assert.AreEqual(4, chained.IndexOf("d"));
			Assert.AreEqual(-1, chained.IndexOf("nothere"));
			Assert.Throws(typeof(NotSupportedException), () => chained.Remove("c"));
			Assert.Throws(typeof(NotSupportedException), () => chained.Insert(3, "d2"));
			Assert.Throws(typeof(NotSupportedException), () => chained.Insert(chained.Count, "f"));
			Assert.Throws(typeof(NotSupportedException), () => chained.RemoveAt(2));
			Assert.Throws(typeof(NotSupportedException), () => chained.RemoveAt(chained.Count));
			var array1 = new string[chained.Count];
			chained.CopyTo(array1, 0);
			CollectionAssert.AreEqual(new[] { "a", "b", "c", "c", "d" }, array1);
			var array2 = new string[chained.Count + 2];
			chained.CopyTo(array2, 2);
			CollectionAssert.AreEqual(new[] { null, null, "a", "b", "c", "c", "d" }, array2);
			AssertThrowsOutOfRangeException(() => _ = chained[-1]);
			AssertThrowsOutOfRangeException(() => _ = chained[chained.Count]);
			if (isReadOnly)
			{
				Assert.Throws(typeof(NotSupportedException), () => chained[1] = "b2");
				Assert.Throws(typeof(NotSupportedException), () => chained[2] = "d3");
				Assert.Throws(typeof(NotSupportedException), () => chained[-1] = "error");
				Assert.Throws(typeof(NotSupportedException), () => chained[chained.Count] = "error");
				AssertChainedListSanity(left, right, chained);
				CollectionAssert.AreEqual(new[] { "a", "b", "c" }, left);
				CollectionAssert.AreEqual(new[] { "c", "d" }, right);
				CollectionAssert.AreEqual(new[] { "a", "b", "c", "c", "d" }, chained);
			}
			else
			{
				chained[2] = "c2";
				chained[3] = "c3";
				AssertThrowsOutOfRangeException(() => chained[-1] = "error");
				AssertThrowsOutOfRangeException(() => chained[chained.Count] = "error");
				AssertChainedListSanity(left, right, chained);
				CollectionAssert.AreEqual(new[] { "a", "b", "c2" }, left);
				CollectionAssert.AreEqual(new[] { "c3", "d" }, right);
				CollectionAssert.AreEqual(new[] { "a", "b", "c2", "c3", "d" }, chained);
			}
			Assert.Throws(typeof(NotSupportedException), () => chained.Clear());
		}

		// Annoyingly, contained arrays throw IndexOutOfRangeException instead of ArgumentOutOfException,
		// and out-of-range checks are delegated to the contained ILists for performance reasons
		// (rather than explicit out-of-range checks that would effectively translate IndexOutOfRangeException into ArgumentOutOfException)
		// and that users should not be catching such exceptions (rather than doing their own out-of-range checks as needed),
		// so just check whether one of the two exceptions is thrown.
		static void AssertThrowsOutOfRangeException(TestDelegate testDelegate)
		{
			try
			{
				testDelegate();
				return;
			}
			catch (IndexOutOfRangeException)
			{
				return;
			}
			catch (ArgumentOutOfRangeException)
			{
				return;
			}
			catch (Exception)
			{
				Assert.Throws(typeof(ArgumentOutOfRangeException), testDelegate);
			}
			Assert.Throws(typeof(ArgumentOutOfRangeException), testDelegate);
		}

		[Test]
		public void ChainConcat_ToString()
		{
			var leftA = new[] { "a", "b", "c" };
			var rightA = new[] { "c", "d" };
			var chainedA = leftA.ChainConcat(rightA);
			Assert.AreEqual(chainedA.GetType().ToString(), chainedA.ToString());
			var leftB = new List<string>(leftA);
			var rightB = new List<string>(rightA);
			var chainedB = leftB.ChainConcat(rightB);
			Assert.AreEqual(chainedB.GetType().ToString(), chainedB.ToString());
			var leftC = new MyList1(leftA);
			var rightC = new MyList1(rightA);
			var chainedC = leftC.ChainConcat(rightC);
			Assert.AreEqual("a,b,c,c,d", chainedC.ToString());
			var leftD = new MyList2(leftA);
			var rightD = new MyList2(rightA);
			var chainedD = leftD.ChainConcat(rightD);
			Assert.AreEqual("a, b, c, c, d", chainedD.ToString());
			var leftE = new MyList3(leftA);
			var rightE = new MyList3(rightA);
			var chainedE = leftE.ChainConcat(rightE);
			Assert.AreEqual("a|b|c,c|d", chainedE.ToString());
			var chainedF = leftA.ChainConcat(chainedD);
			Assert.AreEqual(chainedF.GetType().ToString(), chainedF.ToString());
			var chainedG = chainedD.ChainConcat(rightA);
			Assert.AreEqual(chainedG.GetType().ToString(), chainedG.ToString());
			var chainedH = chainedC.ChainConcat(chainedD);
			Assert.AreEqual("a,b,c,c,d, a, b, c, c, d", chainedH.ToString());
		}

		class MyList1 : List<string>
		{
			internal MyList1(IEnumerable<string> enumerable) : base(enumerable)
			{
			}

			public override string ToString() => this.Join(",");
		}

		class MyList2 : List<string>
		{
			internal MyList2(IEnumerable<string> enumerable) : base(enumerable)
			{
			}

			public override string ToString() => this.Join(", ");
		}

		class MyList3 : List<string>
		{
			internal MyList3(IEnumerable<string> enumerable) : base(enumerable)
			{
			}

			public override string ToString() => this.Join("|");
		}

		[Test]
		public void ChainAppend_Array()
		{
			var array = new[] { "a", "b", "c" };
			CollectionAssert.AreEqual(array, array.ChainAppend());
			CollectionAssert.AreEqual(new[] { "a", "b", "c", "c" }, array.ChainAppend("c"));
			CollectionAssert.AreEqual(new[] { "a", "b", "c", "c", "d" }, array.ChainAppend("c", "d"));
			var itemsToAppend = new[] { "c", "d" };
			CommonFixedSizeOrReadOnlyChainListTests(array, itemsToAppend, array.ChainAppend(itemsToAppend), typeof(ChainedFixedSizeRefList<string>));
		}

		[Test]
		public void ChainPrepend_Array()
		{
			var array = new[] { "c", "d" };
			CollectionAssert.AreEqual(array, array.ChainPrepend());
			CollectionAssert.AreEqual(new[] { "c", "c", "d" }, array.ChainPrepend("c"));
			CollectionAssert.AreEqual(new[] { "a", "b", "c", "c", "d" }, array.ChainPrepend("a", "b", "c"));
			var itemsToPrepend = new[] { "a", "b", "c" };
			CommonFixedSizeOrReadOnlyChainListTests(itemsToPrepend, array, array.ChainPrepend(itemsToPrepend), typeof(ChainedFixedSizeRefList<string>));
		}

		static void AssertChainedListSanity<T>(IList<T> left, IList<T> right, IList<T> chained)
		{
			var concated = left.Concat(right).ToList();
			CollectionAssert.AreEqual(concated, chained);
			Assert.IsTrue(chained.Equals(chained));
			Assert.AreEqual(concated.Count, chained.Count);
			for (var index = 0; index < concated.Count; index++)
				Assert.AreEqual(concated[index], chained[index]);
			using (var concatedIter = concated.GetEnumerator())
			{
				if (chained is IRefList<T> chainedRefList)
				{
					var chainedIter = chainedRefList.GetEnumerator();
					try
					{
						while (true)
						{
							var concatedIterHasMore = concatedIter.MoveNext();
							var chainedIterHasMore = chainedIter.MoveNext();
							Assert.AreEqual(concatedIterHasMore, chainedIterHasMore);
							if (!chainedIterHasMore)
								break;
							Assert.AreEqual(concatedIter.Current, chainedIter.Current);
						}
					}
					finally
					{
						if (chainedIter is IDisposable disposable)
							disposable.Dispose();
					}
				}
				else
				{
					using (var chainedIter = chained.GetEnumerator())
					{
						while (true)
						{
							var concatedIterHasMore = concatedIter.MoveNext();
							var chainedIterHasMore = chainedIter.MoveNext();
							Assert.AreEqual(concatedIterHasMore, chainedIterHasMore);
							if (!chainedIterHasMore)
								break;
							Assert.AreEqual(concatedIter.Current, chainedIter.Current);
						}
					}
				}
			}
		}
	}
}
