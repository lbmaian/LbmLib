using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TranslationFilesGenerator.Tools.Tests
{
	[TestClass]
	public class DebugExtensionsTests
	{
		[ClassInitialize]
		public static void ClassInitialize(TestContext _)
		{
			Logging.DefaultLogger = Logging.ConsoleLogger;
		}

		[TestMethod]
		public void ToDebugStringTestC()
		{
			object obj = new TestC();
			Assert.AreEqual("MyITestA", obj.ToDebugString());
		}

		[TestMethod]
		public void ToDebugStringTestD()
		{
			object obj = new TestD();
			Assert.AreEqual("MyITestA", obj.ToDebugString());
		}

		[TestMethod]
		[ExpectedException(typeof(AmbiguousMatchException))]
		public void ToDebugStringTestE()
		{
			object obj = new TestE();
			obj.ToDebugString();
		}

		[TestMethod]
		public void ToDebugStringTestF()
		{
			object obj = new TestF();
			Assert.AreEqual("MyTestF", obj.ToDebugString());
		}

		[TestMethod]
		[ExpectedException(typeof(AmbiguousMatchException))]
		public void ToDebugStringTestG()
		{
			object obj = new TestG();
			obj.ToDebugString();
		}

		[TestMethod]
		public void ToDebugStringTestH()
		{
			object obj = new TestH();
			Assert.AreEqual("MyTestH", obj.ToDebugString());
		}

		public interface ITestA
		{
		}

		public interface ITestB
		{
		}

		public class TestC : ITestA
		{
		}

		public class TestD : TestC
		{
		}

		public class TestE : TestD, ITestB
		{
		}

		public class TestF : TestD, ITestB
		{
		}

		public class TestG : ITestA, ITestB
		{
		}

		public class TestH : ITestA, ITestB
		{
			public string ToDebugString() => "MyTestH";
		}
	}

	static class DebugExtensionsTestsExtensions
	{
		public static string ToDebugString(this DebugExtensionsTests.ITestA _) => "MyITestA";

		public static string ToDebugString(this DebugExtensionsTests.ITestB _) => "MyITestB";

		public static string ToDebugString(this DebugExtensionsTests.TestF _) => "MyTestF";
	}
}
