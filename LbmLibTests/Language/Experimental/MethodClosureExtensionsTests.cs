using System;
using System.Collections.Generic;
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

		public struct TestStruct
		{
			public int X;

			public TestStruct(int x)
			{
				X = x;
			}

			public void SimpleInstanceVoidMethod(int y, params string[] ss)
			{
				Logging.Log(y, "y");
				Logging.Log(ss.ToDebugString(), "ss");
			}
		}

		public class TestClass
		{
			public int X;

			public TestClass(int x)
			{
				X = x;
			}

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
		public void DynamicPartialApplyTest_SimpleStaticVoidMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(x => actualLogs.Add(x)))
			{
				//SimpleStaticVoidMethod("mystring", 2, 4L, 100);
				var method = GetType().GetMethod(nameof(SimpleStaticVoidMethod));
				var fixedArguments = new object[] { "hello world", 20 };
				var partialAppliedMethod = method.DynamicPartialApply(fixedArguments);
				CollectionAssert.AreEqual(fixedArguments, partialAppliedMethod.GetFixedArguments());
				var nonFixedArguments = new object[] { 40L, 20 };
				partialAppliedMethod.Invoke(null, nonFixedArguments);
			}
			var expectedLogs = new[]
			{
				"s: hello world",
				"y: 20",
				"l: 40",
				"x: 20",
			};
			CollectionAssert.AreEqual(expectedLogs, actualLogs);
		}

		public static void SampleStaticVoidMethod(string s, TestStruct v, int y, TestClass c, TestClass @null, List<string> sl, long l, ref int x)
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

		MethodInfo DynamicPartialApplyTest_SampleStaticVoidMethod(MethodInfo method)
		{
			var fixedArguments = new object[] { "hello world", new TestStruct(10), 20, new TestClass(30), null, new List<string>() { "qwerty" }, 40L };
			var partialAppliedMethod = method.DynamicPartialApply(fixedArguments);
			CollectionAssert.AreEqual(fixedArguments, partialAppliedMethod.GetFixedArguments());
			var nonFixedArguments = new object[] { 20 };
			partialAppliedMethod.Invoke(null, nonFixedArguments);
			Assert.AreEqual(20 * 20, nonFixedArguments[0]);
			return partialAppliedMethod;
		}

		[Test]
		public void DynamicPartialApplyTest_SampleStaticVoidMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(x => actualLogs.Add(x)))
			{
				//var x = 100;
				//SampleStaticVoidMethod("mystring", new TestStruct(1), 2, new TestClass(3), null, new List<string>() { "uiop" }, 4L, ref x);
				DynamicPartialApplyTest_SampleStaticVoidMethod(GetType().GetMethod(nameof(SampleStaticVoidMethod)));
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
			};
			CollectionAssert.AreEqual(expectedLogs, actualLogs);
		}

		[Test]
		public void DynamicPartialApplyTest_ClosureMethodGC()
		{
			var actualLogs = new List<string>();
			using (Logging.With(x => actualLogs.Add(x)))
			{
				var method = GetType().GetMethod(nameof(SampleStaticVoidMethod));
				var partialAppliedMethod = default(MethodInfo);
				for (var i = 0; i < 20; i++)
				{
					partialAppliedMethod = DynamicPartialApplyTest_SampleStaticVoidMethod(method);
					if (i % 5 == 0)
					{
						GC.Collect();
						GC.WaitForPendingFinalizers();
						// Since partialAppliedMethod holds a reference to a ClosureMethod, it won't be GC'ed.
						// TODO: Assert ClosureMethod.Closures data.
					}
				}
				// Test that the latest partially applied method still works.
				var nonFixedArguments = new object[] { 30 };
				partialAppliedMethod.Invoke(null, nonFixedArguments);
				Assert.AreEqual(30 * 30, nonFixedArguments[0]);
				GC.Collect();
				GC.WaitForPendingFinalizers();
				// TODO: Assert ClosureMethod.Closures data.
				partialAppliedMethod = null;
				GC.Collect();
				GC.WaitForPendingFinalizers();
				// TODO: Assert ClosureMethod.Closures data.
			}
			// TODO: Assert actualLogs.
			Logging.Log(actualLogs.Join("\n"));
		}

		// TODO: Test DynamicPartialApply on method with return value.

		// TODO: Test DynamicPartialApply on instance method.

		// TODO: Test DynamicBind on static method => throws exception.

		// TODO: Test DynamicBind on instance method.

		// TODO: Test DynamicBind on DynamicPartialApply on static method => throws exception.

		// TODO: Test DynamicBind on DynamicPartialApply on instance method.

		// TODO: Test DynamicPartialApply on DynamicPartialApply

		// TODO: Test DynamicPartialApply on DynamicBind

		delegate void SampleStaticVoidMethod_PartialApply_Delegate(TestClass @null, List<string> sl, long l, ref int x);

		[Test]
		public void CreateDelegateTest_ClosureStaticMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(x => actualLogs.Add(x)))
			{
				var method = GetType().GetMethod(nameof(SampleStaticVoidMethod));
				var partialAppliedMethod = method.DynamicPartialApply("hello world", new TestStruct(10), 20, new TestClass(30));
				//partialAppliedMethod.Invoke(null, new object[] { null, new List<String>() { "qwerty" }, 40L, 20 });
				var @delegate = (SampleStaticVoidMethod_PartialApply_Delegate)partialAppliedMethod.CreateDelegate(typeof(SampleStaticVoidMethod_PartialApply_Delegate));
				var x = 20;
				@delegate(null, new List<string>() { "qwerty" }, 40L, ref x);
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
			};
			CollectionAssert.AreEqual(expectedLogs, actualLogs);
		}

		// TODO: Test CreateDelegate on DynamicPartialApply on instance method

		[Test]
		public void CreateDelegateTest_NonClosureStaticMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(x => actualLogs.Add(x)))
			{
				var method = GetType().GetMethod(nameof(SimpleStaticVoidMethod));
				var @delegate = (Action<string, int, long, int>)method.CreateDelegate(typeof(Action<string, int, long, int>));
				@delegate("mystring", 2, 4L, 100);
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
		public void CreateDelegateTest_NonClosureStaticMethod_Error()
		{
			var method = GetType().GetMethod(nameof(SimpleStaticVoidMethod));
			Assert.Throws(typeof(ArgumentException), () => method.CreateDelegate(typeof(Action<string, int, long, int>), new object()));
		}

		[Test]
		public void CreateDelegateTest_NonClosureInstanceMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(x => actualLogs.Add(x)))
			{
				var method = typeof(TestStruct).GetMethod(nameof(TestStruct.SimpleInstanceVoidMethod));
				var v = new TestStruct(1);
				var @delegate = (Action<int, string[]>)method.CreateDelegate(typeof(Action<int, string[]>), v);
				@delegate(3, new[] { "hi", "there" });
			}
			var expectedLogs = new[]
			{
				"y: 3",
				"ss: string[] { hi, there }",
			};
			CollectionAssert.AreEqual(expectedLogs, actualLogs);
		}

		[Test]
		public void CreateDelegateTest_NonClosureInstanceMethod_Error()
		{
			var method = typeof(TestStruct).GetMethod(nameof(TestStruct.SimpleInstanceVoidMethod));
			Assert.Throws(typeof(ArgumentException), () => method.CreateDelegate(typeof(Action<int, string[]>)));
		}
	}
}
