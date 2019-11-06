using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace LbmLib.Language.Tests
{
	[TestFixture]
	public class DebugExtensionsTests
	{
		[OneTimeSetUp]
		public static void SetUpOnce()
		{
			Logging.DefaultLogger = Logging.ConsoleLogger;
		}

		public KeyValuePair<K, V> TestMethodSignature<K, V>(int[,,][][,] a, in string b, out List<KeyValuePair<K, V>> c, ref double? d, params Dictionary<K, V>[] e)
		{
			c = null;
			return default;
		}

		delegate KeyValuePair<K, V> TestMethodSignatureDelegate<K, V>(int[,,][][,] a, in string b, out List<KeyValuePair<K, V>> c, ref double? d, params Dictionary<K, V>[] e);

		// TODO: Split into multiple tests.
		[Test]
		public void ToDebugStringTests()
		{
			Assert.AreEqual("true", true.ToDebugString());
			Assert.AreEqual("false", false.ToDebugString());
			Assert.AreEqual("null", ((object)null).ToDebugString());
			Assert.AreEqual("1", 1.ToDebugString());
			Assert.AreEqual("1.5", 1.5.ToDebugString());
			Assert.AreEqual("void", typeof(void).ToDebugString());
			Assert.AreEqual("List<int> { 1, 2, 3, 4 }", new List<int>() { 1, 2, 3, 4 }.ToDebugString());
			Assert.AreEqual("System.Collections.Generic.Dictionary<string, Func<int?[,,][], object>>",
				typeof(Dictionary<string, Func<int?[,,][], object>>).ToDebugString());
			Assert.AreEqual("System.Collections.Generic.KeyValuePair<K, V> LbmLib.Language.Tests.DebugExtensionsTests::TestMethodSignature(" +
				"int[,,][][,] a, in string b, out System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<K, V>> c, ref double? d, " +
				"params System.Collections.Generic.Dictionary<K, V>[] e)",
				GetType().GetMethod(nameof(TestMethodSignature)).ToDebugString());
			Assert.AreEqual("Func<int, long, string>", typeof(Func<int, long, string>).ToDebugString());
			Assert.AreEqual("delegate KeyValuePair<float, long> TestMethodSignatureDelegate<float, long>(int[,,][][,] a, in string b, " +
				"out List<KeyValuePair<float, long>> c, ref double? d, params Dictionary<float, long>[] e)",
				typeof(TestMethodSignatureDelegate<float, long>).ToDebugString(includeNamespace: false, includeDeclaringType: false));
		}

		[Test]
		public void ToDebugStringTestEnumerable()
		{
			object obj = new[] { 1, 2, 3, 4 };
			Assert.AreEqual("int[] { 1, 2, 3, 4 }", obj.ToDebugString());
		}

		[Test]
		public void ToDebugStringTestC()
		{
			object obj = new TestC();
			Assert.AreEqual("MyITestA", obj.ToDebugString());
		}

		[Test]
		public void ToDebugStringTestD()
		{
			object obj = new TestD();
			Assert.AreEqual("MyITestA", obj.ToDebugString());
		}

		[Test]
		public void ToDebugStringTestE()
		{
			object obj = new TestE();
			Assert.Throws<AmbiguousMatchException>(() => obj.ToDebugString());
		}

		[Test]
		public void ToDebugStringTestF()
		{
			object obj = new TestF();
			Assert.AreEqual("MyTestF", obj.ToDebugString());
		}

		[Test]
		public void ToDebugStringTestG()
		{
			object obj = new TestG();
			Assert.Throws<AmbiguousMatchException>(() => obj.ToDebugString());
		}

		[Test]
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

		[Test]
		public void ToDebugStringTestGenE()
		{
			object obj = new TestGenE<Type>();
			Assert.AreEqual("MyITestGenA", obj.ToDebugString());
		}

		public interface ITestGenA<T, U, V>
		{
		}

		public interface ITestGenB<X>
		{
		}

		public interface ITestGenC<T, V> : ITestGenA<T, int, V>, ITestGenB<T>
		{
		}

		public interface ITestGenD<V> : ITestGenC<string, V>, ITestGenB<string>
		{
		}

		public class TestGenE<T> : ITestGenD<T>
		{
		}

		public class TestGenF : TestGenE<float[]>, ITestGenC<int, string>
		{
		}

		[Test]
		public void ToDebugStringTestCodeInstruction()
		{
			object obj = new[] { new global::Harmony.CodeInstruction(System.Reflection.Emit.OpCodes.Ret) };
			Assert.AreEqual("0: ret", obj.ToDebugString());
		}
	}

	static class DebugExtensionsTestsExtensions
	{
		public static string ToDebugString(this DebugExtensionsTests.ITestA _) => "MyITestA";

		public static string ToDebugString(this DebugExtensionsTests.ITestB _) => "MyITestB";

		public static string ToDebugString(this DebugExtensionsTests.TestF _) => "MyTestF";

		public static string ToDebugString<T, U, V>(this DebugExtensionsTests.ITestGenA<T, U, V> _) => "MyITestGenA";
	}
}
