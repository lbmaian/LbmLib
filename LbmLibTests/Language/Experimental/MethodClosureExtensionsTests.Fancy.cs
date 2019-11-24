using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace LbmLib.Language.Experimental.Tests
{
	// structs have no inheritance, so using partial struct as a workaround.
	public partial struct TestStruct
	{
		public void FancyInstanceVoidMethod(Type t, float x, out int y, out Dictionary<Type, string> dict,
			in List<int> il, Func<TestStruct, int, string> func, int z, string s)
		{
			Logging.Log(X, "X");
			X++;
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

	public class TestClassFancy : TestClass
	{
		public TestClassFancy(int x) : base(x)
		{
		}

		public KeyValuePair<Type, string> FancyInstanceNonVoidMethod(Type t, float x, ref int y, out Dictionary<Type, string> dict,
			in IList<int> il, Func<TestClass, int, string> func, int z, string s)
		{
			Logging.Log(X, "X");
			X++;
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

	[TestFixture]
	public class MethodClosureExtensionsTestsFancy : MethodClosureExtensionsBase
	{
		public static void FancyStaticVoidMethod(string s1, ref string s2, TestStruct v1, ref TestStruct v2, int y1, in int y2, TestClass c1, in TestClass c2,
			TestClass @null, IList<string> slist, long l, ref int x)
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
			v1.X++; // won't be reflected in the caller due to value type
			v2 = new TestStruct(1234);
			c1.X++;
			slist.Add(s1);
			x *= x;
		}

		public static List<string[]> FancyStaticNonVoidMethod(string s1, out string s2, TestStruct v1, out TestStruct v2, int y1, ref int y2, TestClass c1, ref TestClass c2,
			TestClass @null, IList<string> slist, long l, ref int x)
		{
			s2 = "fancy2";
			v2 = new TestStruct(4321);
			FancyStaticVoidMethod(s1, ref s2, v1, ref v2, y1, y2, c1, c2, @null, slist, l, ref x);
			y2++;
			return slist.Select(z => new string[] { z + "a", z + "b", z + "c" }).ToList();
		}

		public enum InvocationType
		{
			DirectCall,
			Invoke,
			Delegate,
		}

		delegate void FancyStaticVoidMethod_Delegate(string s1, ref string s2, TestStruct v1, ref TestStruct v2, int y1, in int y2, TestClass c1, in TestClass c2,
			TestClass @null, IList<string> slist, long l, ref int x);

		[TestCase(InvocationType.DirectCall, false)]
		[TestCase(InvocationType.Invoke, false)]
		[TestCase(InvocationType.Invoke, true)]
		[TestCase(InvocationType.Delegate, false)]
		[TestCase(InvocationType.Delegate, true)]
		public void Control_FancyStaticVoidMethod(InvocationType invocationType, bool emptyPartialApply)
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				var method = typeof(MethodClosureExtensionsTestsFancy).GetMethod(nameof(FancyStaticVoidMethod));
				if (emptyPartialApply)
				{
					var partialAppliedMethod = method.PartialApply();
					Assert.IsNull(partialAppliedMethod.FixedThisArgument);
					CollectionAssert.AreEqual(new object[0], partialAppliedMethod.FixedArguments);
					method = partialAppliedMethod;
				}

				var s = "start";
				var v1 = new TestStruct(1);
				var v2 = new TestStruct(-1);
				var c1 = new TestClass(3);
				var slist = new List<string>() { "asdf" };
				var x = 100;
				if (invocationType == InvocationType.DirectCall)
				{
					FancyStaticVoidMethod("mystring", ref s, v1, ref v2, 2, 4, c1, new TestClass(5), null, slist, 4L, ref x);
				}
				else if (invocationType == InvocationType.Delegate)
				{
					method.CreateDelegate<FancyStaticVoidMethod_Delegate>()("mystring", ref s, v1, ref v2, 2, 4, c1, new TestClass(5), null, slist, 4L, ref x);
				}
				else // if (invocationType == InvocationType.Invoke)
				{
					var arguments = new object[] { "mystring", s, v1, v2, 2, 4, c1, new TestClass(5), null, slist, 4L, x };
					method.Invoke(null, arguments);
					s = arguments[1] as string;
					v1 = (TestStruct)arguments[2];
					v2 = (TestStruct)arguments[3];
					c1 = arguments[6] as TestClass;
					slist = arguments[9] as List<string>;
					x = (int)arguments[11];
				}
				Assert.AreEqual("startfancy1", s);
				Assert.AreEqual(new TestStruct(1), v1);
				Assert.AreEqual(new TestStruct(1234), v2);
				Assert.AreEqual(new TestClass(4), c1);
				Assert.AreEqual(new[] { "asdf", "mystring" }, slist);
				Assert.AreEqual(100 * 100, x);

				fixture.ExpectedLogs = new[]
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
			}
		}

		delegate void FancyStaticVoidMethod_PartialApply_Delegate(ref int x);

		[Test]
		public void PartialApply_FancyStaticVoidMethod()
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				var method = typeof(MethodClosureExtensionsTestsFancy).GetMethod(nameof(FancyStaticVoidMethod));
				var fixedArguments = new object[] { "hello world", "start", new TestStruct(10), new TestStruct(15), 20, 25, new TestClass(30), new TestClass(35),
					null, new List<string>() { "qwerty" }, 40L };
				var partialAppliedMethod = method.PartialApply(fixedArguments);
				Assert.AreEqual("Void FancyStaticVoidMethod_unbound_helloworld_start_TestStruct10_TestStruct15_20_25_TestClass30_TestClass35_" +
					"null_SystemCollectionsGenericList1SystemString_40(Int32& x)",
					partialAppliedMethod.ToString());
				Assert.AreEqual("static void FancyStaticVoidMethod" +
					"(string s1: #hello world#, ref string s2: #start#, TestStruct v1: #TestStruct{10}#, ref TestStruct v2: #TestStruct{15}#, int y1: #20#, in int y2: #25#, " +
					"TestClass c1: #TestClass{30}#, in TestClass c2: #TestClass{35}#, TestClass @null: #null#, " +
					"IList<string> slist: #List<string> { qwerty }#, long l: #40#, ref int x)",
					partialAppliedMethod.ToDebugString(false, false));
				Assert.IsNull(partialAppliedMethod.FixedThisArgument);
				CollectionAssert.AreEqual(fixedArguments, partialAppliedMethod.FixedArguments);
				CollectionAssert.AreEqual(method.GetParameters().CopyToEnd(fixedArguments.Length), partialAppliedMethod.GetParameters());

				var nonFixedArguments = new object[] { 20 };
				var returnValue = partialAppliedMethod.Invoke(null, nonFixedArguments);
				Assert.IsNull(returnValue);
				var expectedFixedArguments = new object[] { "hello world", "startfancy1", new TestStruct(10), new TestStruct(1234), 20, 25, new TestClass(31), new TestClass(35),
					null, new List<string>() { "qwerty", "hello world" }, 40L };
				CollectionAssert.AreEqual(expectedFixedArguments, fixedArguments);
				var expectedNonFixedArguments = new object[] { 20 * 20 };
				CollectionAssert.AreEqual(expectedNonFixedArguments, nonFixedArguments);

				var x = 30;
				partialAppliedMethod.CreateDelegate<FancyStaticVoidMethod_PartialApply_Delegate>()(ref x);
				expectedFixedArguments[1] = "startfancy1fancy1";
				expectedFixedArguments[6] = new TestClass(32);
				expectedFixedArguments[9] = new List<string>() { "qwerty", "hello world", "hello world" };
				CollectionAssert.AreEqual(expectedFixedArguments, fixedArguments);
				Assert.AreEqual(30 * 30, x);

				fixture.ExpectedLogs = new[]
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
					"c1.X: 31",
					"c2.X: 35",
					"@null: null",
					"slist: List<string> { qwerty, hello world }",
					"l: 40",
					"x: 30",
				};
			}
		}

		delegate List<string[]> FancyStaticNonVoidMethod_Delegate(string s1, out string s2, TestStruct v1, out TestStruct v2, int y1, ref int y2, TestClass c1, ref TestClass c2,
			TestClass @null, IList<string> slist, long l, ref int x);

		[TestCase(InvocationType.DirectCall, false)]
		[TestCase(InvocationType.Invoke, false)]
		[TestCase(InvocationType.Invoke, true)]
		[TestCase(InvocationType.Delegate, false)]
		[TestCase(InvocationType.Delegate, true)]
		public void Control_FancyStaticNonVoidMethod(InvocationType invocationType, bool emptyPartialApply)
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				var method = typeof(MethodClosureExtensionsTestsFancy).GetMethod(nameof(FancyStaticNonVoidMethod));
				if (emptyPartialApply)
				{
					var partialAppliedMethod = method.PartialApply();
					Assert.IsNull(partialAppliedMethod.FixedThisArgument);
					CollectionAssert.AreEqual(new object[0], partialAppliedMethod.FixedArguments);
					method = partialAppliedMethod;
				}

				var s = "start";
				var v1 = new TestStruct(1);
				var v2 = new TestStruct(-1);
				var y = 4;
				var c1 = new TestClass(3);
				var c2 = new TestClass(5);
				var slist = new List<string>() { "asdf" };
				var x = 100;
				List<string[]> returnValue;
				if (invocationType == InvocationType.DirectCall)
				{
					returnValue = FancyStaticNonVoidMethod("mystring", out s, v1, out v2, 2, ref y, c1, ref c2, null, slist, 4L, ref x);
				}
				else if (invocationType == InvocationType.Delegate)
				{
					returnValue = method.CreateDelegate<FancyStaticNonVoidMethod_Delegate>()("mystring", out s, v1, out v2, 2, ref y, c1, ref c2, null, slist, 4L, ref x);
				}
				else // if (invocationType == InvocationType.Invoke)
				{
					var arguments = new object[] { "mystring", s, v1, v2, 2, y, c1, c2, null, slist, 4L, x };
					returnValue = method.Invoke(null, arguments) as List<string[]>;
					s = arguments[1] as string;
					v1 = (TestStruct)arguments[2];
					v2 = (TestStruct)arguments[3];
					y = (int)arguments[5];
					c1 = arguments[6] as TestClass;
					c2 = arguments[7] as TestClass;
					slist = arguments[9] as List<string>;
					x = (int)arguments[11];
				}
				var expectedReturnValue = new List<string[]>() { new[] { "asdfa", "asdfb", "asdfc" }, new[] { "mystringa", "mystringb", "mystringc" } };
				CollectionAssert.AreEqual(expectedReturnValue, returnValue);
				Assert.AreEqual("fancy2fancy1", s);
				Assert.AreEqual(new TestStruct(1), v1);
				Assert.AreEqual(new TestStruct(1234), v2);
				Assert.AreEqual(5, y);
				Assert.AreEqual(new TestClass(4), c1);
				Assert.AreEqual(new TestClass(5), c2);
				Assert.AreEqual(new[] { "asdf", "mystring" }, slist);
				Assert.AreEqual(100 * 100, x);

				fixture.ExpectedLogs = new[]
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
			}
		}

		// Also used in MethodClosureExtensionsTests.GC.
		internal delegate List<string[]> FancyStaticNonVoidMethod_PartialApply_Delegate(TestClass @null, IList<string> sl, long l, ref int x);

		[Test]
		public void PartialApply_FancyStaticNonVoidMethod()
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				var method = typeof(MethodClosureExtensionsTestsFancy).GetMethod(nameof(FancyStaticNonVoidMethod));
				var fixedArguments = new object[] { "hi world", "start", new TestStruct(10), new TestStruct(15), 20, 25, new TestClass(30), new TestClass(35) };
				var partialAppliedMethod = method.PartialApply(fixedArguments);
				Assert.AreEqual("List`1 FancyStaticNonVoidMethod_unbound_hiworld_start_TestStruct10_TestStruct15_20_25_TestClass30_TestClass35" +
					"(LbmLib.Language.Experimental.Tests.TestClass null, System.Collections.Generic.IList`1[System.String] slist, Int64 l, Int32& x)",
					partialAppliedMethod.ToString());
				Assert.AreEqual("static List<string[]> FancyStaticNonVoidMethod" +
					"(string s1: #hi world#, out string s2: #start#, TestStruct v1: #TestStruct{10}#, out TestStruct v2: #TestStruct{15}#, int y1: #20#, ref int y2: #25#, " +
					"TestClass c1: #TestClass{30}#, ref TestClass c2: #TestClass{35}#, TestClass @null, IList<string> slist, long l, ref int x)",
					partialAppliedMethod.ToDebugString(false, false));
				Assert.IsNull(partialAppliedMethod.FixedThisArgument);
				CollectionAssert.AreEqual(fixedArguments, partialAppliedMethod.FixedArguments);
				CollectionAssert.AreEqual(method.GetParameters().CopyToEnd(fixedArguments.Length), partialAppliedMethod.GetParameters());

				var nonFixedArguments = new object[] { null, new List<string>() { "uiop" }, 40L, 20 };
				var returnValue = partialAppliedMethod.Invoke(null, nonFixedArguments) as List<string[]>;
				var expectedReturnValue = new List<string[]>() { new[] { "uiopa", "uiopb", "uiopc" }, new[] { "hi worlda", "hi worldb", "hi worldc" } };
				CollectionAssert.AreEqual(expectedReturnValue, returnValue);
				var expectedFixedArguments = new object[] { "hi world", "fancy2fancy1", new TestStruct(10), new TestStruct(1234), 20, 26, new TestClass(31), new TestClass(35) };
				CollectionAssert.AreEqual(expectedFixedArguments, fixedArguments);
				var expectedNonFixedArguments = new object[] { null, new List<string>() { "uiop", "hi world" }, 40L, 20 * 20 };
				CollectionAssert.AreEqual(expectedNonFixedArguments, nonFixedArguments);

				var slist = new List<string>() { "asdf" };
				var x = 30;
				returnValue = partialAppliedMethod.CreateDelegate<FancyStaticNonVoidMethod_PartialApply_Delegate>()(null, slist, 40L, ref x);
				expectedReturnValue = new List<string[]>() { new[] { "asdfa", "asdfb", "asdfc" }, new[] { "hi worlda", "hi worldb", "hi worldc" } };
				Assert.AreEqual(expectedReturnValue, returnValue);
				expectedFixedArguments[5] = 27;
				expectedFixedArguments[6] = new TestClass(32);
				CollectionAssert.AreEqual(expectedFixedArguments, fixedArguments);
				CollectionAssert.AreEqual(new List<string>() { "asdf", "hi world" }, slist);
				Assert.AreEqual(30 * 30, x);

				fixture.ExpectedLogs = new[]
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
					"c1.X: 31",
					"c2.X: 35",
					"@null: null",
					"slist: List<string> { asdf }",
					"l: 40",
					"x: 30",
				};
			}
		}

		// TODO: Test PartialApply on void instance method.

		// TODO: Test PartialApply on non-void instance method.

		// TODO: Test PartialApply with fixed arguments for all parameters.
		// TODO: Test multiple PartialApply.

		// TODO: Test Bind on instance method.

		// TODO: Test Bind on PartialApply on instance method.

		// TODO: Test PartialApply on Bind on instance method.
	}
}
