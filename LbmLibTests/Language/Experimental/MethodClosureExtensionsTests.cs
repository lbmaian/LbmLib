using System;
using System.Collections.Generic;
using System.Linq;
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

		public partial struct TestStruct
		{
			public int X;

			public TestStruct(int x)
			{
				X = x;
			}

			public override string ToString() => $"TestStruct{{{X}}}";

			public void SimpleInstanceVoidMethod(int y, params string[] ss)
			{
				Logging.Log(y, "y");
				Logging.Log(ss.ToDebugString(), "ss");
			}
		}

		public partial class TestClass
		{
			public int X;

			public TestClass(int x)
			{
				X = x;
			}

			public override string ToString() => $"TestClass{{{X}}}";

			public string SimpleInstanceNonVoidMethod(int y, params string[] ss)
			{
				Logging.Log(y, "y");
				Logging.Log(ss.ToDebugString(), "ss");
				return "ghkj";
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
			using (Logging.With(x => actualLogs.Add(x)))
			{
				//SimpleStaticVoidMethod("mystring", 2, 4L, 100);
				var method = GetType().GetMethod(nameof(SimpleStaticVoidMethod));
				var fixedArguments = new object[] { "hello world", 20 };
				var partialAppliedMethod = method.PartialApply(fixedArguments);
				CollectionAssert.AreEqual(fixedArguments, partialAppliedMethod.GetFixedArguments());
				var nonFixedArguments = new object[] { 40L, 20 };
				partialAppliedMethod.Invoke(null, nonFixedArguments);
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
			CollectionAssert.AreEqual(expectedLogs, actualLogs);
		}

		[Test]
		public void PartialApply_SimpleStaticNonVoidMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(x => actualLogs.Add(x)))
			{
				//SimpleStaticNonVoidMethod("mystring", 2, 4L, 100);
				var method = GetType().GetMethod(nameof(SimpleStaticNonVoidMethod));
				var fixedArguments = new object[] { "hello world", 1, 2L, 3 };
				var partialAppliedMethod = method.PartialApply(fixedArguments);
				CollectionAssert.AreEqual(fixedArguments, partialAppliedMethod.GetFixedArguments());
				var returnValue = partialAppliedMethod.Invoke(null, new object[0]);
				Assert.AreEqual("asdf", returnValue);
				var partialAppliedDelegate = partialAppliedMethod.CreateDelegate<Func<string>>();
				returnValue = partialAppliedDelegate();
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
				"x: 3",
			};
			CollectionAssert.AreEqual(expectedLogs, actualLogs);
		}

		public partial struct TestStruct
		{
			public void FancyInstanceVoidMethod(Type t, string s, out Dictionary<Type, string> dict, in List<int> il,
				Func<TestClass, int, string> func)
			{
				dict = new Dictionary<Type, string>() { { t, s } };
				Logging.Log(t, "t");
				Logging.Log(s, "s");
				Logging.Log(dict, "dict");
				Logging.Log(il, "il");
				Logging.Log(func, "func");
			}
		}

		public partial class TestClass
		{
			public KeyValuePair<Type, string> FancyInstanceNonVoidMethod(Type t, string s, out Dictionary<Type, string> dict, in List<int> il,
				Func<TestClass, int, string> func)
			{
				dict = new Dictionary<Type, string>() { { t, s } };
				Logging.Log(t, "t");
				Logging.Log(s, "s");
				Logging.Log(dict, "dict");
				Logging.Log(il, "il");
				Logging.Log(func, "func");
				foreach (var pair in dict)
					return pair;
				return default;
			}
		}

		public static void FancyStaticVoidMethod(string s, TestStruct v, int y, TestClass c, TestClass @null, List<string> sl, long l, ref int x)
		{
			Logging.Log(s, "s");
			Logging.Log(v.X, "v.X");
			Logging.Log(y, "y");
			Logging.Log(c.X, "c.X");
			Logging.Log(@null, "@null");
			Logging.Log(sl.ToDebugString(), "sl");
			Logging.Log(l, "l");
			Logging.Log(x, "x");
			x *= x;
		}

		public static List<string[]> FancyStaticNonVoidMethod(string s, TestStruct v, int y, TestClass c, TestClass @null, List<string> sl, long l, ref int x)
		{
			FancyStaticVoidMethod(s, v, y, c, @null, sl, l, ref x);
			return sl.Select(z => new string[] { z + "a", z + "b", z + "c" }).ToList();
		}

		delegate void FancyStaticVoidMethod_PartialApply_Delegate(ref int x);

		[Test]
		public void PartialApply_FancyStaticVoidMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(x => actualLogs.Add(x)))
			{
				//var x = 100;
				//FancyStaticVoidMethod("mystring", new TestStruct(1), 2, new TestClass(3), null, new List<string>() { "uiop" }, 4L, ref x);
				var method = GetType().GetMethod(nameof(FancyStaticVoidMethod));
				var fixedArguments = new object[] { "hello world", new TestStruct(10), 20, new TestClass(30), null, new List<string>() { "qwerty" }, 40L };
				var partialAppliedMethod = method.PartialApply(fixedArguments);
				Assert.AreEqual("Void FancyStaticVoidMethod_unbound_helloworld_TestStruct10_20_TestClass30_null_SystemCollectionsGenericList1SystemString_40" +
					"(Int32& x)",
					partialAppliedMethod.ToString());
				Assert.AreEqual("static void LbmLib.Language.Experimental.Tests.MethodClosureExtensionsTests::FancyStaticVoidMethod" +
					"(#hello world#, #TestStruct{10}#, #20#, #TestClass{30}#, #null#, #List<string> { qwerty }#, #40#, ref int x)",
					partialAppliedMethod.ToDebugString());
				CollectionAssert.AreEqual(fixedArguments, partialAppliedMethod.GetFixedArguments());
				CollectionAssert.AreEqual(method.GetParameters().CopyToEnd(fixedArguments.Length), partialAppliedMethod.GetParameters());
				var nonFixedArguments = new object[] { 20 };
				partialAppliedMethod.Invoke(null, nonFixedArguments);
				Assert.AreEqual(20 * 20, nonFixedArguments[0]);
				var partialAppliedDelegate = partialAppliedMethod.CreateDelegate<FancyStaticVoidMethod_PartialApply_Delegate>();
				var x1 = 30;
				partialAppliedDelegate(ref x1);
				Assert.AreEqual(30 * 30, x1);
			}
			var expectedLogs = new[]
			{
				"s: hello world",
				"v.X: 10",
				"y: 20",
				"c.X: 30",
				"@null: null",
				"sl: List<string> { qwerty }",
				"l: 40",
				"x: 20",
				"s: hello world",
				"v.X: 10",
				"y: 20",
				"c.X: 30",
				"@null: null",
				"sl: List<string> { qwerty }",
				"l: 40",
				"x: 30",
			};
			CollectionAssert.AreEqual(expectedLogs, FilterLogs(actualLogs));
		}

		delegate List<string[]> FancyStaticNonVoidMethod_PartialApply_Delegate(TestClass @null, List<string> sl, long l, ref int x);

		[Test]
		public void PartialApply_FancyStaticNonVoidMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(x => actualLogs.Add(x)))
			{
				var method = GetType().GetMethod(nameof(FancyStaticNonVoidMethod));
				var fixedArguments = new object[] { "hi world", new TestStruct(10), 20, new TestClass(30) };
				var partialAppliedMethod = method.PartialApply(fixedArguments);
				Assert.AreEqual("List`1 FancyStaticNonVoidMethod_unbound_hiworld_TestStruct10_20_TestClass30" +
					"(TestClass null, System.Collections.Generic.List`1[System.String] sl, Int64 l, Int32& x)",
					partialAppliedMethod.ToString());
				Assert.AreEqual("static System.Collections.Generic.List<string[]> " +
					"LbmLib.Language.Experimental.Tests.MethodClosureExtensionsTests::FancyStaticNonVoidMethod" +
					"(#hi world#, #TestStruct{10}#, #20#, #TestClass{30}#, " +
					"LbmLib.Language.Experimental.Tests.MethodClosureExtensionsTests::LbmLib.Language.Experimental.Tests.TestClass @null, " +
					"System.Collections.Generic.List<string> sl, long l, ref int x)",
					partialAppliedMethod.ToDebugString());
				CollectionAssert.AreEqual(fixedArguments, partialAppliedMethod.GetFixedArguments());
				CollectionAssert.AreEqual(method.GetParameters().CopyToEnd(fixedArguments.Length), partialAppliedMethod.GetParameters());
				var nonFixedArguments = new object[] { null, new List<string>() { "uiop" }, 40L, 20 };
				var returnValue = (List<string[]>)partialAppliedMethod.Invoke(null, nonFixedArguments);
				var expectedReturnValue = new List<string[]>() { new string[] { "uiopa", "uiopb", "uiopc" } };
				Assert.AreEqual(expectedReturnValue, returnValue);
				Assert.AreEqual(20 * 20, nonFixedArguments[3]);
				var partialAppliedDelegate = partialAppliedMethod.CreateDelegate<FancyStaticNonVoidMethod_PartialApply_Delegate>();
				var x = 30;
				returnValue = partialAppliedDelegate(null, new List<string>() { "uiop" }, 40L, ref x);
				Assert.AreEqual(expectedReturnValue, returnValue);
				Assert.AreEqual(30 * 30, x);
			}
			var expectedLogs = new[]
			{
				"s: hi world",
				"v.X: 10",
				"y: 20",
				"c.X: 30",
				"@null: null",
				"sl: List<string> { uiop }",
				"l: 40",
				"x: 20",
				"s: hi world",
				"v.X: 10",
				"y: 20",
				"c.X: 30",
				"@null: null",
				"sl: List<string> { uiop }",
				"l: 40",
				"x: 30",
			};
			CollectionAssert.AreEqual(expectedLogs, FilterLogs(actualLogs));
		}

		// TODO: Test PartialApply on instance method.

		// TODO: Test Bind on static method => throws exception.

		// TODO: Test Bind on instance method.

		// TODO: Test Bind on PartialApply on static method => throws exception.

		// TODO: Test Bind on PartialApply on instance method.

		// TODO: Test ClosureMethod.MakeGenericMethod somehow.

		static ICollection<string> FilterLogs(IEnumerable<string> logs)
		{
			return logs.Where(x => !x.StartsWith("DEBUG") && !x.StartsWith("Saved dynamically created partial applied method to")).ToList();
		}

		[Test]
		public void CreateDelegate_NonClosureStaticMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(x => actualLogs.Add(x)))
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
			CollectionAssert.AreEqual(expectedLogs, actualLogs);
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
			using (Logging.With(x => actualLogs.Add(x)))
			{
				var method = typeof(TestStruct).GetMethod(nameof(TestStruct.SimpleInstanceVoidMethod));
				var v = new TestStruct(1);
				var closureDelegate = method.CreateDelegate<Action<int, string[]>>(v);
				closureDelegate(3, new[] { "hi", "there" });
			}
			var expectedLogs = new[]
			{
				"y: 3",
				"ss: string[] { hi, there }",
			};
			CollectionAssert.AreEqual(expectedLogs, actualLogs);
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
			using (Logging.With(x => actualLogs.Add(x)))
			{
				// Note: Even null-ing out a variable that holds the only reference to an object doesn't actually allow the object to be
				// finalizable until after the method ends, so putting all the logic that stores delegates into variables into another method.
				ClosureMethod_DelegateRegistry_GC_Internal();
				//Logging.Log("DEBUG before final GC:\n" + ClosureMethod.DelegateRegistry);
				Assert.AreEqual(1, ClosureMethod.DelegateClosures.Where(closure => !(closure is null)).Count());
				TryFullGCFinalization();
				//Logging.Log("DEBUG after final GC:\n" + ClosureMethod.DelegateRegistry);
				Assert.AreEqual(0, ClosureMethod.DelegateClosures.Where(closure => !(closure is null)).Count());
			}
			//Logging.Log(actualLogs.Join("\n"));
		}

		void ClosureMethod_DelegateRegistry_GC_Internal()
		{
			var method = GetType().GetMethod(nameof(FancyStaticNonVoidMethod));
			var partialAppliedDelegate = default(FancyStaticNonVoidMethod_PartialApply_Delegate);
			for (var i = 1; i <= 20; i++)
			{
				var partialAppliedMethod = method.PartialApply("hello world", new TestStruct(10), 20, new TestClass(30));
				partialAppliedDelegate = partialAppliedMethod.CreateDelegate<FancyStaticNonVoidMethod_PartialApply_Delegate>();
				if (i % 5 == 0)
				{
					//Logging.Log($"DEBUG before {i} GC:\n" + ClosureMethod.DelegateRegistry);
					Assert.AreEqual(i == 5 ? 5 : 6, ClosureMethod.DelegateClosures.Where(closure => !(closure is null)).Count());
					TryFullGCFinalization();
					//Logging.Log($"DEBUG after {i} GC:\n" + ClosureMethod.DelegateRegistry);
					Assert.AreEqual(1, ClosureMethod.DelegateClosures.Where(closure => !(closure is null)).Count());
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
