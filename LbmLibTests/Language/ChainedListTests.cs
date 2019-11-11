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
			CommonFixedSizeOrReadOnlyChainListTests(left, right, left.ChainConcat(right), isReadOnly: false);
			left = new[] { "a", "b", "c" };
			right = new List<string>() { "c", "d" };
			CommonFixedSizeOrReadOnlyChainListTests(left, right, left.ChainConcat(right), isReadOnly: false);
			left = new List<string>() { "a", "b", "c" };
			right = new[] { "c", "d" };
			CommonFixedSizeOrReadOnlyChainListTests(left, right, left.ChainConcat(right), isReadOnly: false);
		}

		[Test]
		public void ChainConcat_ReadOnlyLists()
		{
			var left = new List<string>() { "a", "b", "c" };
			var right = new List<string>() { "c", "d" };
			var leftReadOnly = left.AsReadOnly();
			var rightReadOnly = right.AsReadOnly();
			CommonFixedSizeOrReadOnlyChainListTests(left, rightReadOnly, left.ChainConcat(rightReadOnly), isReadOnly: true);
			CommonFixedSizeOrReadOnlyChainListTests(leftReadOnly, right, leftReadOnly.ChainConcat(right), isReadOnly: true);
			CommonFixedSizeOrReadOnlyChainListTests(leftReadOnly, rightReadOnly, leftReadOnly.ChainConcat(rightReadOnly), isReadOnly: true);
		}

		static void CommonFixedSizeOrReadOnlyChainListTests(IList<string> left, IList<string> right, IList<string> chained, bool isReadOnly)
		{
			Assert.AreEqual(ChainedListExtensions.IsReadOnly(left) || ChainedListExtensions.IsReadOnly(right), isReadOnly);
			Assert.AreEqual(chained.IsReadOnly, isReadOnly);
			Assert.IsTrue(ChainedListExtensions.IsFixedSize(left) || ChainedListExtensions.IsFixedSize(right));
			Assert.IsTrue(ChainedListExtensions.IsFixedSize(chained));
			AssertChainedListSanity(left, right, chained);
			CollectionAssert.AreEqual(new[] { "a", "b", "c", "c", "d" }, chained);
			Assert.AreEqual(chained.GetType().ToString(), chained.ToString());
			Assert.Throws(typeof(ArgumentOutOfRangeException), () => _ = chained[-1]);
			Assert.Throws(typeof(ArgumentOutOfRangeException), () => _ = chained[chained.Count]);
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
			Assert.Throws(typeof(ArgumentOutOfRangeException), () => _ = chained[-1]);
			Assert.Throws(typeof(ArgumentOutOfRangeException), () => _ = chained[chained.Count]);
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
				Assert.Throws(typeof(ArgumentOutOfRangeException), () => chained[-1] = "error");
				Assert.Throws(typeof(ArgumentOutOfRangeException), () => chained[chained.Count] = "error");
				AssertChainedListSanity(left, right, chained);
				CollectionAssert.AreEqual(new[] { "a", "b", "c2" }, left);
				CollectionAssert.AreEqual(new[] { "c3", "d" }, right);
				CollectionAssert.AreEqual(new[] { "a", "b", "c2", "c3", "d" }, chained);
			}
			Assert.Throws(typeof(NotSupportedException), () => chained.Clear());
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
		public void ChainAppend_List()
		{
			var list = new List<string>() { "a", "b", "c" };
			CollectionAssert.AreEqual(list, list.ChainAppend());
			CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, list.ChainAppend("d"));
			CollectionAssert.AreEqual(new[] { "a", "b", "c", "d", "e" }, list.ChainAppend("d", "e"));
			var itemsToAppend = new[] { "c", "d" };
			CommonFixedSizeOrReadOnlyChainListTests(list, itemsToAppend, list.ChainAppend(itemsToAppend), isReadOnly: false);
		}

		[Test]
		public void ChainAppend_Array()
		{
			var list = new[] { "a", "b", "c" };
			CollectionAssert.AreEqual(list, list.ChainAppend());
			CollectionAssert.AreEqual(new[] { "a", "b", "c", "c" }, list.ChainAppend("c"));
			CollectionAssert.AreEqual(new[] { "a", "b", "c", "c", "d" }, list.ChainAppend("c", "d"));
			var itemsToAppend = new[] { "c", "d" };
			CommonFixedSizeOrReadOnlyChainListTests(list, itemsToAppend, list.ChainAppend(itemsToAppend), isReadOnly: false);
		}

		[Test]
		public void ChainPrepend_List()
		{
			var list = new List<string>() { "c", "d" };
			CollectionAssert.AreEqual(list, list.ChainPrepend());
			CollectionAssert.AreEqual(new[] { "c", "c", "d" }, list.ChainPrepend("c"));
			CollectionAssert.AreEqual(new[] { "a", "b", "c", "c", "d" }, list.ChainPrepend("a", "b", "c"));
			var itemsToPrepend = new[] { "a", "b", "c" };
			CommonFixedSizeOrReadOnlyChainListTests(itemsToPrepend, list, list.ChainPrepend(itemsToPrepend), isReadOnly: false);
		}

		[Test]
		public void ChainPrepend_Array()
		{
			var list = new[] { "c", "d" };
			CollectionAssert.AreEqual(list, list.ChainPrepend());
			CollectionAssert.AreEqual(new[] { "c", "c", "d" }, list.ChainPrepend("c"));
			CollectionAssert.AreEqual(new[] { "a", "b", "c", "c", "d" }, list.ChainPrepend("a", "b", "c"));
			var itemsToPrepend = new[] { "a", "b", "c" };
			CommonFixedSizeOrReadOnlyChainListTests(itemsToPrepend, list, list.ChainPrepend(itemsToPrepend), isReadOnly: false);
		}

		static void AssertChainedListSanity<T>(IList<T> left, IList<T> right, IList<T> chained)
		{
			var concated = left.Concat(right).ToList();
			CollectionAssert.AreEqual(concated, chained);
			Assert.AreEqual(concated.Count, chained.Count);
			for (var index = 0; index < concated.Count; index++)
				Assert.AreEqual(concated[index], chained[index]);
			using (IEnumerator<T> concatedIter = concated.GetEnumerator(), chainedIter = chained.GetEnumerator())
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
