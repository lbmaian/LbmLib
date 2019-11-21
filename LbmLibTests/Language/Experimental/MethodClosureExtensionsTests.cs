using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace LbmLib.Language.Experimental.Tests
{
	[TestFixture]
	public class MethodClosureExtensionsTests
	{
		[OneTimeSetUp]
		public static void SetUpOnce()
		{
			Logging.DefaultLogger = Logging.ConsoleLogger;
		}

		// Note: Following test method and structure fixtures are public so that methods dynamically created via DebugDynamicMethodBuilder have access to them.

#pragma warning disable CA1034 // Nested types should not be visible
		public partial struct TestStruct
		{
			public int X;

			public TestStruct(int x)
			{
				X = x;
			}

			public override bool Equals(object obj) => obj is TestStruct test && X == test.X;

			public override int GetHashCode() => -1830369473 + X.GetHashCode();

			public override string ToString() => $"TestStruct{{{X}}}";

			public static bool operator ==(TestStruct left, TestStruct right)
			{
				return left.Equals(right);
			}

			public static bool operator !=(TestStruct left, TestStruct right)
			{
				return !(left == right);
			}
		}

		public partial class TestClass
		{
			public int X;

			public TestClass(int x)
			{
				X = x;
			}

			public override bool Equals(object obj) => obj is TestClass test && X == test.X;

			public override int GetHashCode() => -1830369473 + X.GetHashCode();

			public override string ToString() => $"TestClass{{{X}}}";

			public static bool operator ==(TestClass left, TestClass right)
			{
				return left is null ? right is null : left.Equals(right);
			}

			public static bool operator !=(TestClass left, TestClass right)
			{
				return !(left == right);
			}
		}
#pragma warning restore CA1034 // Nested types should not be visible

		public partial struct TestStruct
		{
			public void SimpleInstanceVoidMethod(int y, params string[] ss)
			{
				Logging.Log(X, "x");
				Logging.Log(y, "y");
				Logging.Log(ss.ToDebugString(), "ss");
			}
		}

		public partial class TestClass
		{
			public string SimpleInstanceNonVoidMethod(int y, params string[] ss)
			{
				Logging.Log(X, "x");
				Logging.Log(y, "y");
				Logging.Log(ss.ToDebugString(), "ss");
				return "ghkj";
			}

			public virtual void SimpleVirtualInstanceVoidMethod(int y, params string[] ss)
			{
				Logging.Log(X, "x");
				Logging.Log(y, "y");
				Logging.Log(ss.ToDebugString(), "ss");
			}
		}

		public static void SimpleStaticVoidMethod(string s, int y, long l, int x)
		{
			Logging.Log(s, "s");
			Logging.Log(y, "y");
			Logging.Log(l, "l");
			Logging.Log(x, "x");
		}

		public static string SimpleStaticNonVoidMethod(string s, int y, long l, int x)
		{
			SimpleStaticVoidMethod(s, y, l, x);
			return "asdf";
		}

		[Test]
		public void PartialApply_SimpleStaticVoidMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				//SimpleStaticVoidMethod("mystring", 2, 4L, 100);
				var method = GetType().GetMethod(nameof(SimpleStaticVoidMethod));
				var fixedArguments = new object[] { "hello world", 20 };
				var partialAppliedMethod = method.PartialApply(fixedArguments);
				CollectionAssert.AreEqual(fixedArguments, partialAppliedMethod.FixedArguments);
				partialAppliedMethod.Invoke(null, new object[] { 40L, 20 });
				var partialAppliedDelegate = partialAppliedMethod.CreateDelegate<Action<long, int>>();
				partialAppliedDelegate(30L, 10);
			}
			var expectedLogs = new[]
			{
				"s: hello world",
				"y: 20",
				"l: 40",
				"x: 20",
				"s: hello world",
				"y: 20",
				"l: 30",
				"x: 10",
			};
			CollectionAssert.AreEqual(expectedLogs, FilterLogs(actualLogs));
		}

		[Test]
		public void PartialApply_SimpleStaticNonVoidMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				//SimpleStaticNonVoidMethod("mystring", 2, 4L, 100);
				var method = GetType().GetMethod(nameof(SimpleStaticNonVoidMethod));
				var fixedArguments = new object[] { "hello world", 1, 2L };
				var partialAppliedMethod = method.PartialApply(fixedArguments);
				CollectionAssert.AreEqual(fixedArguments, partialAppliedMethod.FixedArguments);
				var returnValue = partialAppliedMethod.Invoke(null, new object[] { 3 });
				Assert.AreEqual("asdf", returnValue);
				// Static method can be invoked with a non-null target - target is just ignored in this case.
				returnValue = partialAppliedMethod.Invoke(this, new object[] { 5 });
				Assert.AreEqual("asdf", returnValue);
				var partialAppliedDelegate = partialAppliedMethod.CreateDelegate<Func<int, string>>();
				returnValue = partialAppliedDelegate(7);
				Assert.AreEqual("asdf", returnValue);
			}
			var expectedLogs = new[]
			{
				"s: hello world",
				"y: 1",
				"l: 2",
				"x: 3",
				"s: hello world",
				"y: 1",
				"l: 2",
				"x: 5",
				"s: hello world",
				"y: 1",
				"l: 2",
				"x: 7",
			};
			CollectionAssert.AreEqual(expectedLogs, FilterLogs(actualLogs));
		}

		[Test]
		public void PartialApply_StaticMethod_Error()
		{
			var method = GetType().GetMethod(nameof(SimpleStaticNonVoidMethod));
			var partialAppliedMethod = method.PartialApply("hello world", 1, 2L);
			// Static method delegate cannot be invoked with a target.
			Assert.Throws(typeof(ArgumentException), () => partialAppliedMethod.CreateDelegate<Func<int, string>>(this));
			// Invoked with too few parameters.
			Assert.Throws(typeof(TargetParameterCountException), () => partialAppliedMethod.Invoke(null, new object[0]));
			// Invoked with too many parameters.
			Assert.Throws(typeof(TargetParameterCountException), () => partialAppliedMethod.Invoke(null, new object[] { 3, 4 }));
			// Invoked with invalid parameter type.
			Assert.Throws(typeof(ArgumentException), () => partialAppliedMethod.Invoke(null, new object[] { "string" }));
			// Invalid delegate type.
			Assert.Throws(typeof(ArgumentException), () => partialAppliedMethod.CreateDelegate<Func<string, int>>());
		}

		delegate void SimpleInstanceVoidMethod_PartialApply_Delegate(params string[] ss);

		[Test]
		public void PartialApply_SimpleInstanceVoidMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var v = new TestStruct(15);
				//v.SimpleInstanceVoidMethod(5, "hello", "world");
				var method = typeof(TestStruct).GetMethod(nameof(TestStruct.SimpleInstanceVoidMethod));
				var fixedArguments = new object[] { 5 };
				var partialAppliedMethod = method.PartialApply(fixedArguments);
				CollectionAssert.AreEqual(fixedArguments, partialAppliedMethod.FixedArguments);
				partialAppliedMethod.Invoke(v, new object[] { new string[] { "hello", "world" } });
				var partialAppliedDelegate = partialAppliedMethod.CreateDelegate<SimpleInstanceVoidMethod_PartialApply_Delegate>(v);
				partialAppliedDelegate("hi", "there");
			}
			var expectedLogs = new[]
			{
				"x: 15",
				"y: 5",
				"ss: string[] { hello, world }",
				"x: 15",
				"y: 5",
				"ss: string[] { hi, there }",
			};
			CollectionAssert.AreEqual(expectedLogs, FilterLogs(actualLogs));
		}

		[Test]
		public void PartialApply_SimpleInstanceNonVoidMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var c = new TestClass(15);
				//c.SimpleInstanceNonVoidMethod(5, "hello", "world");
				var method = typeof(TestClass).GetMethod(nameof(TestClass.SimpleInstanceNonVoidMethod));
				var fixedArguments = new object[] { 5 };
				var partialAppliedMethod = method.PartialApply(fixedArguments);
				CollectionAssert.AreEqual(fixedArguments, partialAppliedMethod.FixedArguments);
				var returnValue = (string)partialAppliedMethod.Invoke(c, new object[] { new string[] { "hello", "world" } });
				Assert.AreEqual("ghkj", returnValue);
				var partialAppliedDelegate = partialAppliedMethod.CreateDelegate<Func<string[], string>>(c);
				returnValue = partialAppliedDelegate(new string[] { "hi", "there" });
				Assert.AreEqual("ghkj", returnValue);
			}
			var expectedLogs = new[]
			{
				"x: 15",
				"y: 5",
				"ss: string[] { hello, world }",
				"x: 15",
				"y: 5",
				"ss: string[] { hi, there }",
			};
			CollectionAssert.AreEqual(expectedLogs, FilterLogs(actualLogs));
		}

		[Test]
		public void PartialApply_SimpleVirtualInstanceVoidMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var c = new TestClass(15);
				//c.SimpleVirtualInstanceVoidMethod(5, "hello", "world");
				var method = typeof(TestClass).GetMethod(nameof(TestClass.SimpleVirtualInstanceVoidMethod));
				var fixedArguments = new object[] { 5 };
				var partialAppliedMethod = method.PartialApply(fixedArguments);
				CollectionAssert.AreEqual(fixedArguments, partialAppliedMethod.FixedArguments);
				partialAppliedMethod.Invoke(c, new object[] { new string[] { "hello", "world" } });
				var partialAppliedDelegate = partialAppliedMethod.CreateDelegate<SimpleInstanceVoidMethod_PartialApply_Delegate>(c);
				partialAppliedDelegate("hi", "there");
			}
			var expectedLogs = new[]
			{
				"x: 15",
				"y: 5",
				"ss: string[] { hello, world }",
				"x: 15",
				"y: 5",
				"ss: string[] { hi, there }",
			};
			CollectionAssert.AreEqual(expectedLogs, FilterLogs(actualLogs));
		}

		[Test]
		public void PartialApply_InstanceMethod_Error()
		{
			var v = new TestStruct(15);
			var method = typeof(TestStruct).GetMethod(nameof(TestStruct.SimpleInstanceVoidMethod));
			var partialAppliedMethod = method.PartialApply(5);
			// Instance method cannot be invoked without a target.
			Assert.Throws(typeof(TargetException), () => partialAppliedMethod.Invoke(null, new object[] { 3 }));
			// Instance method cannot be invoked with an invalid target.
			Assert.Throws(typeof(TargetException), () => partialAppliedMethod.Invoke(this, new object[] { 3 }));
			// Invoked with too few parameters.
			Assert.Throws(typeof(TargetParameterCountException), () => partialAppliedMethod.Invoke(v, new object[0]));
			// Invoked with too many parameters.
			Assert.Throws(typeof(TargetParameterCountException), () => partialAppliedMethod.Invoke(v, new object[] { 3, 4 }));
			// Invoked with invalid parameter type.
			Assert.Throws(typeof(ArgumentException), () => partialAppliedMethod.Invoke(v, new object[] { "string" }));
			// Instance method delegate cannot be invoked without a target.
			Assert.Throws(typeof(ArgumentException), () => partialAppliedMethod.CreateDelegate<Action<string, string>>());
			// Instance method delegate cannot be invoked with an invalid target.
			Assert.Throws(typeof(ArgumentException), () => partialAppliedMethod.CreateDelegate<Action<string, string>>(this));
			// Invalid delegate type.
			Assert.Throws(typeof(ArgumentException), () => partialAppliedMethod.CreateDelegate<Func<string, int>>(v));
		}

		public partial struct TestStruct
		{
			public void FancyInstanceVoidMethod(Type t, float x, out int y, out Dictionary<Type, string> dict,
				in List<int> il, Func<TestClass, int, string> func, int z, string s)
			{
				dict = new Dictionary<Type, string>() { { t, s } };
				Logging.Log(t, "t");
				Logging.Log(x, "x");
				y = 10;
				Logging.Log(y, "y");
				Logging.Log(dict, "dict");
				Logging.Log(il, "il");
				Logging.Log(func, "func");
				Logging.Log(z, "z");
				Logging.Log(s, "s");
			}
		}

		public partial class TestClass
		{
			public KeyValuePair<Type, string> FancyInstanceNonVoidMethod(Type t, float x, ref int y, out Dictionary<Type, string> dict,
				in List<int> il, Func<TestClass, int, string> func, int z, string s)
			{
				dict = new Dictionary<Type, string>() { { t, s } };
				Logging.Log(t, "t");
				Logging.Log(x, "x");
				y = 10;
				Logging.Log(y, "y");
				Logging.Log(dict, "dict");
				Logging.Log(il, "il");
				Logging.Log(func, "func");
				Logging.Log(z, "z");
				Logging.Log(s, "s");
				foreach (var pair in dict)
					return pair;
				return default;
			}
		}

		public static void FancyStaticVoidMethod(string s1, ref string s2, TestStruct v1, ref TestStruct v2, int y1, in int y2, TestClass c1, in TestClass c2,
			TestClass @null, List<string> slist, long l, ref int x)
		{
			Logging.Log(s1, "s1");
			Logging.Log(s2, "s2");
			Logging.Log(v1.X, "v1.X");
			Logging.Log(v2.X, "v2.X");
			Logging.Log(y1, "y1");
			Logging.Log(y2, "y2");
			Logging.Log(c1.X, "c1.X");
			Logging.Log(c2.X, "c2.X");
			Logging.Log(@null, "@null");
			Logging.Log(slist.ToDebugString(), "slist");
			Logging.Log(l, "l");
			Logging.Log(x, "x");
			s2 += "fancy1";
			v2 = new TestStruct(1234);
			slist.Add(s1);
			x *= x;
		}

		public static List<string[]> FancyStaticNonVoidMethod(string s1, out string s2, TestStruct v1, out TestStruct v2, int y1, ref int y2, TestClass c1, ref TestClass c2,
			TestClass @null, List<string> slist, long l, ref int x)
		{
			s2 = "fancy2";
			v2 = new TestStruct(4321);
			FancyStaticVoidMethod(s1, ref s2, v1, ref v2, y1, y2, c1, c2, @null, slist, l, ref x);
			y2++;
			return slist.Select(z => new string[] { z + "a", z + "b", z + "c" }).ToList();
		}

		[Test]
		public void Control_FancyStaticVoidMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var s = "start";
				var v = new TestStruct(-1);
				var slist = new List<string>() { "asdf" };
				var x = 100;
				FancyStaticVoidMethod("mystring", ref s, new TestStruct(1), ref v, 2, 4, new TestClass(3), new TestClass(5),
					null, slist, 4L, ref x);
				Assert.AreEqual("startfancy1", s);
				Assert.AreEqual(1234, v.X);
				Assert.AreEqual(new[] { "asdf", "mystring" }, slist);
				Assert.AreEqual(100 * 100, x);
			}
			var expectedLogs = new[]
			{
				"s1: mystring",
				"s2: start",
				"v1.X: 1",
				"v2.X: -1",
				"y1: 2",
				"y2: 4",
				"c1.X: 3",
				"c2.X: 5",
				"@null: null",
				"slist: List<string> { asdf }",
				"l: 4",
				"x: 100",
			};
			CollectionAssert.AreEqual(expectedLogs, actualLogs);
		}

		delegate void FancyStaticVoidMethod_PartialApply_Delegate(ref int x);

		[Test]
		public void PartialApply_FancyStaticVoidMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var method = GetType().GetMethod(nameof(FancyStaticVoidMethod));
				var fixedArguments = new object[] { "hello world", "start", new TestStruct(10), new TestStruct(15), 20, 25, new TestClass(30), new TestClass(35),
					null, new List<string>() { "qwerty" }, 40L };
				var partialAppliedMethod = method.PartialApply(fixedArguments);
				Assert.AreEqual("Void FancyStaticVoidMethod_unbound_helloworld_start_TestStruct10_TestStruct15_20_25_TestClass30_TestClass35_" +
					"null_SystemCollectionsGenericList1SystemString_40(Int32& x)",
					partialAppliedMethod.ToString());
				Assert.AreEqual("static void FancyStaticVoidMethod" +
					"(string s1: #hello world#, ref string s2: #start#, TestStruct v1: #TestStruct{10}#, ref TestStruct v2: #TestStruct{15}#, int y1: #20#, in int y2: #25#, " +
					"TestClass c1: #TestClass{30}#, in TestClass c2: #TestClass{35}#, TestClass @null: #null#, " +
					"List<string> slist: #List<string> { qwerty }#, long l: #40#, ref int x)",
					partialAppliedMethod.ToDebugString(false, false));
				CollectionAssert.AreEqual(fixedArguments, partialAppliedMethod.FixedArguments);
				CollectionAssert.AreEqual(method.GetParameters().CopyToEnd(fixedArguments.Length), partialAppliedMethod.GetParameters());

				var nonFixedArguments = new object[] { 20 };
				partialAppliedMethod.Invoke(null, nonFixedArguments);
				var expectedFixedArguments = new object[] { "hello world", "startfancy1", new TestStruct(10), new TestStruct(1234), 20, 25, new TestClass(30), new TestClass(35),
					null, new List<string>() { "qwerty", "hello world" }, 40L };
				CollectionAssert.AreEqual(expectedFixedArguments, fixedArguments);
				var expectedNonFixedArguments = new object[] { 20 * 20 };
				CollectionAssert.AreEqual(expectedNonFixedArguments, nonFixedArguments);

				var partialAppliedDelegate = partialAppliedMethod.CreateDelegate<FancyStaticVoidMethod_PartialApply_Delegate>();
				var x = 30;
				partialAppliedDelegate(ref x);
				expectedFixedArguments[1] = "startfancy1fancy1";
				expectedFixedArguments[9] = new List<string>() { "qwerty", "hello world", "hello world" };
				CollectionAssert.AreEqual(expectedFixedArguments, fixedArguments);
				Assert.AreEqual(30 * 30, x);
			}
			var expectedLogs = new[]
			{
				"s1: hello world",
				"s2: start",
				"v1.X: 10",
				"v2.X: 15",
				"y1: 20",
				"y2: 25",
				"c1.X: 30",
				"c2.X: 35",
				"@null: null",
				"slist: List<string> { qwerty }",
				"l: 40",
				"x: 20",
				"s1: hello world",
				"s2: startfancy1",
				"v1.X: 10",
				"v2.X: 1234",
				"y1: 20",
				"y2: 25",
				"c1.X: 30",
				"c2.X: 35",
				"@null: null",
				"slist: List<string> { qwerty, hello world }",
				"l: 40",
				"x: 30",
			};
			CollectionAssert.AreEqual(expectedLogs, FilterLogs(actualLogs));
		}

		[Test]
		public void Control_FancyStaticNonVoidMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var y = 4;
				var c = new TestClass(5);
				var x = 100;
				var returnValue = FancyStaticNonVoidMethod("mystring", out var s, new TestStruct(1), out var v, 2, ref y, new TestClass(3), ref c,
					null, new List<string>() { "asdf" }, 4L, ref x);
				var expectedReturnValue = new List<string[]>() { new[] { "asdfa", "asdfb", "asdfc" }, new[] { "mystringa", "mystringb", "mystringc" } };
				CollectionAssert.AreEqual(expectedReturnValue, returnValue);
				Assert.AreEqual("fancy2fancy1", s);
				Assert.AreEqual(1234, v.X);
				Assert.AreEqual(5, y);
				Assert.AreEqual(5, c.X);
				Assert.AreEqual(100 * 100, x);
			}
			var expectedLogs = new[]
			{
				"s1: mystring",
				"s2: fancy2",
				"v1.X: 1",
				"v2.X: 4321",
				"y1: 2",
				"y2: 4",
				"c1.X: 3",
				"c2.X: 5",
				"@null: null",
				"slist: List<string> { asdf }",
				"l: 4",
				"x: 100",
			};
			CollectionAssert.AreEqual(expectedLogs, actualLogs);
		}

		delegate List<string[]> FancyStaticNonVoidMethod_PartialApply_Delegate(TestClass @null, List<string> sl, long l, ref int x);

		[Test]
		public void PartialApply_FancyStaticNonVoidMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var method = GetType().GetMethod(nameof(FancyStaticNonVoidMethod));
				var fixedArguments = new object[] { "hi world", "start", new TestStruct(10), new TestStruct(15), 20, 25, new TestClass(30), new TestClass(35) };
				var partialAppliedMethod = method.PartialApply(fixedArguments);
				Assert.AreEqual("List`1 FancyStaticNonVoidMethod_unbound_hiworld_start_TestStruct10_TestStruct15_20_25_TestClass30_TestClass35" +
					"(TestClass null, System.Collections.Generic.List`1[System.String] slist, Int64 l, Int32& x)",
					partialAppliedMethod.ToString());
				Assert.AreEqual("static List<string[]> FancyStaticNonVoidMethod" +
					"(string s1: #hi world#, out string s2: #start#, TestStruct v1: #TestStruct{10}#, out TestStruct v2: #TestStruct{15}#, int y1: #20#, ref int y2: #25#, " +
					"TestClass c1: #TestClass{30}#, ref TestClass c2: #TestClass{35}#, TestClass @null, List<string> slist, long l, ref int x)",
					partialAppliedMethod.ToDebugString(false, false));
				CollectionAssert.AreEqual(fixedArguments, partialAppliedMethod.FixedArguments);
				CollectionAssert.AreEqual(method.GetParameters().CopyToEnd(fixedArguments.Length), partialAppliedMethod.GetParameters());

				var nonFixedArguments = new object[] { null, new List<string>() { "uiop" }, 40L, 20 };
				var returnValue = (List<string[]>)partialAppliedMethod.Invoke(null, nonFixedArguments);
				var expectedReturnValue = new List<string[]>() { new[] { "uiopa", "uiopb", "uiopc" }, new[] { "hi worlda", "hi worldb", "hi worldc" } };
				CollectionAssert.AreEqual(expectedReturnValue, returnValue);
				var expectedFixedArguments = new object[] { "hi world", "fancy2fancy1", new TestStruct(10), new TestStruct(1234), 20, 26, new TestClass(30), new TestClass(35) };
				CollectionAssert.AreEqual(expectedFixedArguments, fixedArguments);
				var expectedNonFixedArguments = new object[] { null, new List<string>() { "uiop", "hi world" }, 40L, 20 * 20 };
				CollectionAssert.AreEqual(expectedNonFixedArguments, nonFixedArguments);

				var partialAppliedDelegate = partialAppliedMethod.CreateDelegate<FancyStaticNonVoidMethod_PartialApply_Delegate>();
				var slist = new List<string>() { "asdf" };
				var x = 30;
				returnValue = partialAppliedDelegate(null, slist, 40L, ref x);
				expectedReturnValue = new List<string[]>() { new[] { "asdfa", "asdfb", "asdfc" }, new[] { "hi worlda", "hi worldb", "hi worldc" } };
				Assert.AreEqual(expectedReturnValue, returnValue);
				expectedFixedArguments[5] = 27;
				CollectionAssert.AreEqual(expectedFixedArguments, fixedArguments);
				CollectionAssert.AreEqual(new List<string>() { "asdf", "hi world" }, slist);
				Assert.AreEqual(30 * 30, x);
			}
			var expectedLogs = new[]
			{
				"s1: hi world",
				"s2: fancy2",
				"v1.X: 10",
				"v2.X: 4321",
				"y1: 20",
				"y2: 25",
				"c1.X: 30",
				"c2.X: 35",
				"@null: null",
				"slist: List<string> { uiop }",
				"l: 40",
				"x: 20",
				"s1: hi world",
				"s2: fancy2",
				"v1.X: 10",
				"v2.X: 4321",
				"y1: 20",
				"y2: 26",
				"c1.X: 30",
				"c2.X: 35",
				"@null: null",
				"slist: List<string> { asdf }",
				"l: 40",
				"x: 30",
			};
			CollectionAssert.AreEqual(expectedLogs, FilterLogs(actualLogs));
		}

		// TODO: Test PartialApply with 0 fixed arguments.

		// TODO: Test PartialApply with fixed arguments for all parameters.

		// TODO: Test PartialApply on simple/fancy void instance method.

		// TODO: Test PartialApply on simple/fancy non-void instance method.

		// TODO: Test Bind on static method => throws exception.

		// TODO: Test Bind on instance method.

		// TODO: Test Bind on PartialApply on static method => throws exception.

		// TODO: Test Bind on PartialApply on instance method.

		// TODO: Test ClosureMethod.MakeGenericMethod on non-GenericMethodDefinition method => throws exception.

		// TODO: Test ClosureMethod.MakeGenericMethod on GenericMethodDefinition method.

		static ICollection<string> FilterLogs(IEnumerable<string> logs)
		{
			return logs.Where(x => !x.StartsWith("DEBUG") && !x.StartsWith("Saved dynamically created partial applied method to")).ToList();
		}

		[Test]
		public void CreateDelegate_NonClosureStaticMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var method = GetType().GetMethod(nameof(SimpleStaticVoidMethod));
				var closureDelegate = method.CreateDelegate<Action<string, int, long, int>>();
				closureDelegate("mystring", 2, 4L, 100);
			}
			var expectedLogs = new[]
			{
				"s: mystring",
				"y: 2",
				"l: 4",
				"x: 100",
			};
			CollectionAssert.AreEqual(expectedLogs, FilterLogs(actualLogs));
		}

		[Test]
		public void CreateDelegate_NonClosureStaticMethod_Error()
		{
			var method = GetType().GetMethod(nameof(SimpleStaticVoidMethod));
			Assert.Throws(typeof(ArgumentException), () => method.CreateDelegate<Action<string, int, long, int>>(new object()));
		}

		[Test]
		public void CreateDelegate_NonClosureInstanceMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var method = typeof(TestStruct).GetMethod(nameof(TestStruct.SimpleInstanceVoidMethod));
				var v = new TestStruct(1);
				var closureDelegate = method.CreateDelegate<Action<int, string[]>>(v);
				closureDelegate(3, new[] { "hi", "there" });
			}
			var expectedLogs = new[]
			{
				"x: 1",
				"y: 3",
				"ss: string[] { hi, there }",
			};
			CollectionAssert.AreEqual(expectedLogs, FilterLogs(actualLogs));
		}

		[Test]
		public void CreateDelegate_NonClosureInstanceMethod_Error()
		{
			var method = typeof(TestStruct).GetMethod(nameof(TestStruct.SimpleInstanceVoidMethod));
			Assert.Throws(typeof(ArgumentException), () => method.CreateDelegate<Action<int, string[]>>());
		}

		[Test]
		public void ClosureMethod_DelegateRegistry_GC()
		{
			TryFullGCFinalization();
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				// Note: Even null-ing out a variable that holds the only reference to an object doesn't actually allow the object to be
				// finalizable until after the method ends, so putting all the logic that stores delegates into variables into another method.
				ClosureMethod_DelegateRegistry_GC_Internal();
				//Logging.Log("DEBUG before final GC:\n" + ClosureMethod.DelegateRegistry);
				Assert.AreEqual(1, ClosureMethod.DelegateRegistry.Closures.Where(closure => !(closure is null)).Count());
				TryFullGCFinalization();
				//Logging.Log("DEBUG after final GC:\n" + ClosureMethod.DelegateRegistry);
				Assert.AreEqual(0, ClosureMethod.DelegateRegistry.Closures.Where(closure => !(closure is null)).Count());
			}
			//Logging.Log(actualLogs.Join("\n"));
		}

		void ClosureMethod_DelegateRegistry_GC_Internal()
		{
			var method = GetType().GetMethod(nameof(FancyStaticNonVoidMethod));
			var partialAppliedDelegate = default(FancyStaticNonVoidMethod_PartialApply_Delegate);
			for (var i = 1; i <= 20; i++)
			{
				var partialAppliedMethod = method.PartialApply("hello", "world", new TestStruct(10), new TestStruct(15), 20, 25, new TestClass(30), new TestClass(35));
				partialAppliedDelegate = partialAppliedMethod.CreateDelegate<FancyStaticNonVoidMethod_PartialApply_Delegate>();
				if (i % 5 == 0)
				{
					//Logging.Log($"DEBUG before {i} GC:\n" + ClosureMethod.DelegateRegistry);
					Assert.AreEqual(i == 5 ? 5 : 6, ClosureMethod.DelegateRegistry.Closures.Where(closure => !(closure is null)).Count());
					TryFullGCFinalization();
					//Logging.Log($"DEBUG after {i} GC:\n" + ClosureMethod.DelegateRegistry);
					Assert.AreEqual(1, ClosureMethod.DelegateRegistry.Closures.Where(closure => !(closure is null)).Count());
				}
			}
			// Test that the latest partially applied method delegate still works.
			var x = 20;
			partialAppliedDelegate(null, new List<string>() { "qwerty" }, 40L, ref x);
			Assert.AreEqual(20 * 20, x);
		}

		static void TryFullGCFinalization()
		{
			// This probably isn't fool-proof (what happens if finalizers themselves create objects that need finalization?),
			// but it suffices for our unit testing purposes.
			// Garbage collect any finalized objects and identify finalizable objects.
			GC.Collect();
			// Finalize found finalizable objects.
			GC.WaitForPendingFinalizers();
			// Garbage collect just-finalized objects.
			GC.Collect();
		}
	}
}
