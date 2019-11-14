using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
		public void ChainConcat_FixedSizeLists()
		{
			// Note: Following is not only testing runtime behavior, but also that the static binding of the appropriate ChainConcat extension method is correct.
			// The arrays/lists need to redefined for each sub-test since they're mutated.
			{
				string[] leftArray = new[] { "a", "b", "c" };
				string[] rightArray = new[] { "c", "d" };
				CommonFixedSizeOrReadOnlyChainListTests(leftArray, rightArray, leftArray.ChainConcat(rightArray), typeof(ChainedFixedSizeRefList<string>));
			}
			{
				string[] leftArray = new[] { "a", "b", "c" };
				IBaseRefList<string> rightBaseRefList = new[] { "c", "d" }.AsRefList();
				CommonFixedSizeOrReadOnlyChainListTests(leftArray, rightBaseRefList, leftArray.ChainConcat(rightBaseRefList), typeof(ChainedFixedSizeRefList<string>));
			}
			{
				string[] leftArray = new[] { "a", "b", "c" };
				IRefList<string> rightRefList = new[] { "c", "d" }.AsRefList();
				CommonFixedSizeOrReadOnlyChainListTests(leftArray, rightRefList, leftArray.ChainConcat(rightRefList), typeof(ChainedFixedSizeRefList<string>));
			}
			{
				string[] leftArray = new[] { "a", "b", "c" };
				List<string> rightList = new List<string>() { "c", "d" };
				CommonFixedSizeOrReadOnlyChainListTests(leftArray, rightList, leftArray.ChainConcat(rightList), typeof(ChainedFixedSizeList<string>));
			}
			{
				IBaseRefList<string> leftBaseRefList = new[] { "a", "b", "c" }.AsRefList();
				string[] rightArray = new[] { "c", "d" };
				CommonFixedSizeOrReadOnlyChainListTests(leftBaseRefList, rightArray, leftBaseRefList.ChainConcat(rightArray), typeof(ChainedFixedSizeRefList<string>));
			}
			{
				IBaseRefList<string> leftBaseRefList = new[] { "a", "b", "c" }.AsRefList();
				IBaseRefList<string> rightBaseRefList = new[] { "c", "d" }.AsRefList();
				CommonFixedSizeOrReadOnlyChainListTests(leftBaseRefList, rightBaseRefList, leftBaseRefList.ChainConcat(rightBaseRefList), typeof(ChainedFixedSizeRefList<string>));
			}
			{
				IBaseRefList<string> leftBaseRefList = new[] { "a", "b", "c" }.AsRefList();
				IRefList<string> rightRefList = new[] { "c", "d" }.AsRefList();
				CommonFixedSizeOrReadOnlyChainListTests(leftBaseRefList, rightRefList, leftBaseRefList.ChainConcat(rightRefList), typeof(ChainedFixedSizeRefList<string>));
			}
			{
				IBaseRefList<string> leftBaseRefList = new[] { "a", "b", "c" }.AsRefList();
				List<string> rightList = new List<string>() { "c", "d" };
				CommonFixedSizeOrReadOnlyChainListTests(leftBaseRefList, rightList, leftBaseRefList.ChainConcat(rightList), typeof(ChainedFixedSizeList<string>));
			}
			{
				IRefList<string> leftRefList = new[] { "a", "b", "c" }.AsRefList();
				string[] rightArray = new[] { "c", "d" };
				CommonFixedSizeOrReadOnlyChainListTests(leftRefList, rightArray, leftRefList.ChainConcat(rightArray), typeof(ChainedFixedSizeRefList<string>));
			}
			{
				IRefList<string> leftRefList = new[] { "a", "b", "c" }.AsRefList();
				IBaseRefList<string> rightBaseRefList = new[] { "c", "d" }.AsRefList();
				CommonFixedSizeOrReadOnlyChainListTests(leftRefList, rightBaseRefList, leftRefList.ChainConcat(rightBaseRefList), typeof(ChainedFixedSizeRefList<string>));
			}
			{
				IRefList<string> leftRefList = new[] { "a", "b", "c" }.AsRefList();
				IRefList<string> rightRefList = new[] { "c", "d" }.AsRefList();
				CommonFixedSizeOrReadOnlyChainListTests(leftRefList, rightRefList, leftRefList.ChainConcat(rightRefList), typeof(ChainedFixedSizeRefList<string>));
			}
			{
				IRefList<string> leftRefList = new[] { "a", "b", "c" }.AsRefList();
				List<string> rightList = new List<string>() { "c", "d" };
				CommonFixedSizeOrReadOnlyChainListTests(leftRefList, rightList, leftRefList.ChainConcat(rightList), typeof(ChainedFixedSizeList<string>));
			}
			{
				List<string> leftList = new List<string>() { "a", "b", "c" };
				string[] rightArray = new[] { "c", "d" };
				CommonFixedSizeOrReadOnlyChainListTests(leftList, rightArray, leftList.ChainConcat(rightArray), typeof(ChainedFixedSizeList<string>));
			}
			{
				List<string> leftList = new List<string>() { "a", "b", "c" };
				IBaseRefList<string> rightBaseRefList = new[] { "c", "d" }.AsRefList();
				CommonFixedSizeOrReadOnlyChainListTests(leftList, rightBaseRefList, leftList.ChainConcat(rightBaseRefList), typeof(ChainedFixedSizeList<string>));
			}
			{
				List<string> leftList = new List<string>() { "a", "b", "c" };
				IRefList<string> rightRefList = new[] { "c", "d" }.AsRefList();
				CommonFixedSizeOrReadOnlyChainListTests(leftList, rightRefList, leftList.ChainConcat(rightRefList), typeof(ChainedFixedSizeList<string>));
			}
		}

		[Test]
		public void ChainConcat_ReadOnlyLists()
		{
			string[] leftArray = new[] { "a", "b", "c" };
			List<string> leftList = new List<string>(leftArray);
			IBaseRefList<string> leftBaseRefList = leftArray.AsRefList();
			IRefList<string> leftRefList = leftArray.AsRefList();
			IRefReadOnlyList<string> leftRefReadOnlyList = leftArray.AsRefReadOnlyList();
			ReadOnlyCollection<string> leftReadOnlyList = leftArray.AsReadOnly(); // or leftList.AsReadOnly()

			string[] rightArray = new[] { "c", "d" };
			List<string> rightList = new List<string>(rightArray);
			IBaseRefList<string> rightBaseRefList = rightArray.AsRefList();
			IRefList<string> rightRefList = rightArray.AsRefList();
			IRefReadOnlyList<string> rightRefReadOnlyList = rightArray.AsRefReadOnlyList();
			ReadOnlyCollection<string> rightReadOnlyList = rightArray.AsReadOnly(); // or rightList.AsReadOnly()

			// Note: Following is not only testing runtime behavior, but also that the static binding of the appropriate ChainConcat extension method is correct.
			CommonFixedSizeOrReadOnlyChainListTests(leftArray, rightRefReadOnlyList, leftArray.ChainConcat(rightRefReadOnlyList), typeof(ChainedRefReadOnlyList<string>));
			CommonFixedSizeOrReadOnlyChainListTests(leftArray, rightReadOnlyList, leftArray.ChainConcat(rightReadOnlyList), typeof(ChainedReadOnlyList<string>));
			CommonFixedSizeOrReadOnlyChainListTests(leftList, rightRefReadOnlyList, leftList.ChainConcat(rightRefReadOnlyList), typeof(ChainedReadOnlyList<string>));
			CommonFixedSizeOrReadOnlyChainListTests(leftList, rightReadOnlyList, leftList.ChainConcat(rightReadOnlyList), typeof(ChainedReadOnlyList<string>));
			CommonFixedSizeOrReadOnlyChainListTests(leftBaseRefList, rightRefReadOnlyList, leftBaseRefList.ChainConcat(rightRefReadOnlyList), typeof(ChainedRefReadOnlyList<string>));
			CommonFixedSizeOrReadOnlyChainListTests(leftBaseRefList, rightReadOnlyList, leftBaseRefList.ChainConcat(rightReadOnlyList), typeof(ChainedReadOnlyList<string>));
			CommonFixedSizeOrReadOnlyChainListTests(leftRefList, rightRefReadOnlyList, leftRefList.ChainConcat(rightRefReadOnlyList), typeof(ChainedRefReadOnlyList<string>));
			CommonFixedSizeOrReadOnlyChainListTests(leftRefList, rightReadOnlyList, leftRefList.ChainConcat(rightReadOnlyList), typeof(ChainedReadOnlyList<string>));
			CommonFixedSizeOrReadOnlyChainListTests(leftRefReadOnlyList, rightArray, leftRefReadOnlyList.ChainConcat(rightArray), typeof(ChainedRefReadOnlyList<string>));
			CommonFixedSizeOrReadOnlyChainListTests(leftRefReadOnlyList, rightList, leftRefReadOnlyList.ChainConcat(rightList), typeof(ChainedReadOnlyList<string>));
			CommonFixedSizeOrReadOnlyChainListTests(leftRefReadOnlyList, rightBaseRefList, leftRefReadOnlyList.ChainConcat(rightBaseRefList), typeof(ChainedRefReadOnlyList<string>));
			CommonFixedSizeOrReadOnlyChainListTests(leftRefReadOnlyList, rightRefList, leftRefReadOnlyList.ChainConcat(rightRefList), typeof(ChainedRefReadOnlyList<string>));
			CommonFixedSizeOrReadOnlyChainListTests(leftRefReadOnlyList, rightRefReadOnlyList, leftRefReadOnlyList.ChainConcat(rightRefReadOnlyList), typeof(ChainedRefReadOnlyList<string>));
			CommonFixedSizeOrReadOnlyChainListTests(leftRefReadOnlyList, rightReadOnlyList, leftRefReadOnlyList.ChainConcat(rightReadOnlyList), typeof(ChainedReadOnlyList<string>));
			CommonFixedSizeOrReadOnlyChainListTests(leftReadOnlyList, rightArray, leftReadOnlyList.ChainConcat(rightArray), typeof(ChainedReadOnlyList<string>));
			CommonFixedSizeOrReadOnlyChainListTests(leftReadOnlyList, rightList, leftReadOnlyList.ChainConcat(rightList), typeof(ChainedReadOnlyList<string>));
			CommonFixedSizeOrReadOnlyChainListTests(leftReadOnlyList, rightBaseRefList, leftReadOnlyList.ChainConcat(rightBaseRefList), typeof(ChainedReadOnlyList<string>));
			CommonFixedSizeOrReadOnlyChainListTests(leftReadOnlyList, rightRefList, leftReadOnlyList.ChainConcat(rightRefList), typeof(ChainedReadOnlyList<string>));
			CommonFixedSizeOrReadOnlyChainListTests(leftReadOnlyList, rightRefReadOnlyList, leftReadOnlyList.ChainConcat(rightRefReadOnlyList), typeof(ChainedReadOnlyList<string>));
			CommonFixedSizeOrReadOnlyChainListTests(leftReadOnlyList, rightReadOnlyList, leftReadOnlyList.ChainConcat(rightReadOnlyList), typeof(ChainedReadOnlyList<string>));
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
				if (chained is IBaseRefList<string>)
				{
					Assert.IsInstanceOf(typeof(IRefReadOnlyList<string>), chained);
					var chainedRefList = (IRefReadOnlyList<string>)chained;
					Assert.AreEqual(chainedRefList.ItemRef(1), "b");
					Assert.AreEqual(chainedRefList.ItemRef(4), "d");
					// Following line results in a compile-time error due to readonly ItemRef, as expected:
					//chainedRefList.ItemRef(1) = "b2";
					Assert.AreSame(chainedRefList, chainedRefList.AsReadOnly());
					Assert.Throws(typeof(NotSupportedException), () => chainedRefList.AsNonReadOnly());
					AssertChainedListSanity(left, right, chained);
					CollectionAssert.AreEqual(new[] { "a", "b", "c" }, left);
					CollectionAssert.AreEqual(new[] { "c", "d" }, right);
					CollectionAssert.AreEqual(new[] { "a", "b", "c", "c", "d" }, chained);
				}
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
				if (chained is IBaseRefList<string>)
				{
					Assert.IsInstanceOf(typeof(IRefList<string>), chained);
					var chainedRefList = (IRefList<string>)chained;
					Assert.AreEqual(chainedRefList.ItemRef(1), "b");
					Assert.AreEqual(chainedRefList.ItemRef(4), "d");
					chainedRefList.ItemRef(1) = "b2";
					chainedRefList.ItemRef(4) = "d2";
					Assert.IsInstanceOf(typeof(IRefReadOnlyList<string>), chainedRefList.AsReadOnly());
					Assert.AreSame(chainedRefList, chainedRefList.AsNonReadOnly());
					AssertChainedListSanity(left, right, chained);
					CollectionAssert.AreEqual(new[] { "a", "b2", "c2" }, left);
					CollectionAssert.AreEqual(new[] { "c3", "d2" }, right);
					CollectionAssert.AreEqual(new[] { "a", "b2", "c2", "c3", "d2" }, chained);
				}
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

			if (chained is IBaseRefList<T> chainedBaseRefList)
			{
				var chainedRefReadOnlyList = chainedBaseRefList.AsReadOnly();
				using (var concatedIter = concated.GetEnumerator())
				{
					var chainedRefIter = chainedRefReadOnlyList.GetEnumerator();
					Assert.IsInstanceOf(typeof(IRefReadOnlyListEnumerator<T>), chainedRefIter);
					try
					{
						while (true)
						{
							var concatedIterHasMore = concatedIter.MoveNext();
							var chainedIterHasMore = chainedRefIter.MoveNext();
							Assert.AreEqual(concatedIterHasMore, chainedIterHasMore);
							if (!chainedIterHasMore)
								break;
							Assert.AreEqual(concatedIter.Current, chainedRefIter.Current);
						}
					}
					finally
					{
						if (chainedRefIter is IDisposable disposable)
							disposable.Dispose();
					}
				}
				// Following is just to check that foreach syntax works as expected.
				var foreachList = new List<T>();
				foreach (ref readonly var itemRef in chainedRefReadOnlyList)
					foreachList.Add(itemRef);
				CollectionAssert.AreEqual(concated, foreachList);
			}
			if (chained is IRefList<T> chainedRefList)
			{
				using (var concatedIter = concated.GetEnumerator())
				{
					var chainedRefIter = chainedRefList.GetEnumerator();
					Assert.IsInstanceOf(typeof(IRefListEnumerator<T>), chainedRefIter);
					try
					{
						while (true)
						{
							var concatedIterHasMore = concatedIter.MoveNext();
							var chainedIterHasMore = chainedRefIter.MoveNext();
							Assert.AreEqual(concatedIterHasMore, chainedIterHasMore);
							if (!chainedIterHasMore)
								break;
							Assert.AreEqual(concatedIter.Current, chainedRefIter.Current);
						}
					}
					finally
					{
						if (chainedRefIter is IDisposable disposable)
							disposable.Dispose();
					}
				}
				// Following is just to check that foreach syntax works as expected.
				var foreachList = new List<T>();
				foreach (ref var itemRef in chainedRefList)
					foreachList.Add(itemRef);
				CollectionAssert.AreEqual(concated, foreachList);
			}
		}
	}
}
