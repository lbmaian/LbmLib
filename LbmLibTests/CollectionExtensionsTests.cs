using System.Collections.Generic;
using NUnit.Framework;

namespace LbmLib.Tests
{
	[TestFixture]
	public class CollectionExtensionsTests
	{
		[Test]
		public void FindIndexTest()
		{
			IList<int> list = new List<int>() { 1, 1, 2, 1, 2, 3, 1, 2, 3, 4, 1, 2, 3, 4, 5, 1, 2, 3, 3 };
			Assert.AreEqual(7, list.FindIndex(x => x == 2, x => x == 3, x => x == 4));
			Assert.AreEqual(-1, list.FindIndex(x => x == 2, x => x == 3, x => x == 5));
			Assert.AreEqual(0, list.FindIndex(x => x == 1, x => x == 1));
			Assert.AreEqual(0, list.FindIndex(0, x => x == 1, x => x == 1));
			Assert.AreEqual(-1, list.FindIndex(1, x => x == 1, x => x == 1));
			Assert.AreEqual(3, list.FindIndex(2, x => x == 1, x => x == 2, x => x == 3));
			Assert.AreEqual(3, list.FindIndex(3, x => x == 1, x => x == 2, x => x == 3));
			Assert.AreEqual(6, list.FindIndex(4, x => x == 1, x => x == 2, x => x == 3));
			Assert.AreEqual(1, list.FindIndex(1, 2, x => x == 1, x => x == 2));
			Assert.AreEqual(-1, list.FindIndex(2, 2, x => x == 1, x => x == 2));
			Assert.AreEqual(-1, list.FindIndex(1, 1, x => x == 1, x => x == 2));
		}

		[Test]
		public void FindLastIndexTest()
		{
			IList<int> list = new List<int>() { 1, 1, 2, 1, 2, 3, 1, 2, 3, 4, 1, 2, 3, 4, 5, 1, 2, 3, 3 };
			Assert.AreEqual(11, list.FindLastIndex(x => x == 2, x => x == 3, x => x == 4));
			Assert.AreEqual(-1, list.FindLastIndex(x => x == 2, x => x == 3, x => x == 5));
			Assert.AreEqual(17, list.FindLastIndex(x => x == 3, x => x == 3));
			Assert.AreEqual(17, list.FindLastIndex(17 + 2 - 1, x => x == 3, x => x == 3));
			Assert.AreEqual(-1, list.FindLastIndex(16 + 2 - 1, x => x == 3, x => x == 3));
			Assert.AreEqual(15, list.FindLastIndex(16 + 3 - 1, x => x == 1, x => x == 2, x => x == 3));
			Assert.AreEqual(15, list.FindLastIndex(15 + 3 - 1, x => x == 1, x => x == 2, x => x == 3));
			Assert.AreEqual(10, list.FindLastIndex(14 + 3 - 1, x => x == 1, x => x == 2, x => x == 3));
			Assert.AreEqual(15, list.FindLastIndex(15 + 3 - 1, 3, x => x == 1, x => x == 2, x => x == 3));
			Assert.AreEqual(-1, list.FindLastIndex(14 + 3 - 1, 3, x => x == 1, x => x == 2, x => x == 3));
			Assert.AreEqual(-1, list.FindLastIndex(15 + 3 - 1, 2, x => x == 1, x => x == 2, x => x == 3));
		}
	}
}
