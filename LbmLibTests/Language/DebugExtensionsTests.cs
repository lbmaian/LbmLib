using System;
using System.Collections.Generic;
using System.Linq;
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

		[TestCaseSource(nameof(ToDebugStringCases))]
		public string ToDebugStringTest(object obj)
		{
			return obj.ToDebugString();
		}

		[TestCaseSource(nameof(TypeToDebugStringCase))]
		public string TypeToDebugStringTest(bool includeNamespace, bool includeDeclaringType, object obj)
		{
			if (obj is Type type)
				return type.ToDebugString(includeNamespace, includeDeclaringType);
			else if (obj is FieldInfo field)
				return field.ToDebugString(includeNamespace, includeDeclaringType);
			else if (obj is MethodInfo method)
				return method.ToDebugString(includeNamespace, includeDeclaringType);
			else
				throw new NotSupportedException();
		}

		static readonly TestCaseData[] NonTypeToDebugStringCases =
		{
			new TestCaseData(true).Returns("true"),
			new TestCaseData(false).Returns("false"),
			new TestCaseData(null).Returns("null"),
			new TestCaseData(1).Returns("1"),
			new TestCaseData(1.5).Returns("1.5"),
			new TestCaseData(new List<int>() { 1, 2, 3, 4 }).Returns("List<int> { 1, 2, 3, 4 }"),
		};

		static readonly TestCaseData[] TypeToDebugStringCase =
		{
			new TestCaseData(true, true, typeof(void)).Returns("void"),
			new TestCaseData(true, true, typeof(Dictionary<string, Func<int?[,,][], object>>)).Returns(
				"System.Collections.Generic.Dictionary<string, Func<int?[,,][], object>>"),
			new TestCaseData(true, true, typeof(DebugExtensionsTests).GetMethod(nameof(TestMethodSignature))).Returns(
				"System.Collections.Generic.KeyValuePair<K, V> LbmLib.Language.Tests.DebugExtensionsTests:TestMethodSignature(" +
				"int[,,][][,] a, in string b, out System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<K, V>> c, ref double? d, " +
				"params System.Collections.Generic.Dictionary<K, V>[] e)"),
			new TestCaseData(true, true, typeof(Func<int, long, string>)).Returns("Func<int, long, string>"),
			new TestCaseData(false, false, typeof(TestMethodSignatureDelegate<float, long>)).Returns(
				"delegate KeyValuePair<float, long> TestMethodSignatureDelegate<float, long>(int[,,][][,] a, in string b, " +
				"out List<KeyValuePair<float, long>> c, ref double? d, params Dictionary<float, long>[] e)"),
		};

		static readonly IEnumerable<TestCaseData> ToDebugStringCases = Enumerable.Concat(
			NonTypeToDebugStringCases,
			TypeToDebugStringCase
				.Where(testCaseData => (bool)testCaseData.Arguments[0] && (bool)testCaseData.Arguments[1]) // includeNamespace && includeDeclaringType
				.Select(testCaseData => new TestCaseData(testCaseData.Arguments[2]).Returns(testCaseData.ExpectedResult))); // type/field/method

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

		[Test]
		public void ToDebugStringTestI()
		{
			object obj = new TestI();
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

		public class TestI : TestH
		{
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
