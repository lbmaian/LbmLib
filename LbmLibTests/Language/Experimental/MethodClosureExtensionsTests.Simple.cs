using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace LbmLib.Language.Experimental.Tests
{
	// Note: Method and structure fixtures are public so that methods dynamically created via DebugDynamicMethodBuilder have access to them.

	// structs have no inheritance, so using partial struct as a workaround.
	public partial struct TestStruct
	{
		public void SimpleInstanceVoidMethod(int y, params string[] ss)
		{
			Logging.Log(X, "x");
			Logging.Log(y, "y");
			Logging.Log(ss.ToDebugString(), "ss");
		}
	}

	public class TestClassSimple : TestClass
	{
		public TestClassSimple(int x) : base(x)
		{
		}

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

	[TestFixture]
	public class MethodClosureExtensionsTestsSimple
	{
		[OneTimeSetUp]
		public static void SetUpOnce()
		{
			Logging.DefaultLogger = Logging.ConsoleLogger;
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
		public void Control_SimpleStaticVoidMethod([Values] bool emptyPartialApply)
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				SimpleStaticVoidMethod("mystring", 2, 4L, 100);

				var method = typeof(MethodClosureExtensionsTestsSimple).GetMethod(nameof(SimpleStaticVoidMethod));
				if (emptyPartialApply)
				{
					var partialAppliedMethod = method.PartialApply();
					Assert.IsNull(partialAppliedMethod.FixedThisArgument);
					CollectionAssert.AreEqual(new object[0], partialAppliedMethod.FixedArguments);
					method = partialAppliedMethod;
				}

				var returnValue = method.Invoke(null, new object[] { "mystring", 2, 4L, 100 });
				Assert.IsNull(returnValue);

				var @delegate = method.CreateDelegate<Action<string, int, long, int>>();
				@delegate("mystring", 2, 4L, 100);
			}
			var expectedLogs = new[]
			{
				"s: mystring",
				"y: 2",
				"l: 4",
				"x: 100",
				"s: mystring",
				"y: 2",
				"l: 4",
				"x: 100",
				"s: mystring",
				"y: 2",
				"l: 4",
				"x: 100",
			};
			CollectionAssert.AreEqual(expectedLogs, actualLogs.Where(x => !x.StartsWith("DEBUG")));
		}

		[Test]
		public void PartialApply_SimpleStaticVoidMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var method = typeof(MethodClosureExtensionsTestsSimple).GetMethod(nameof(SimpleStaticVoidMethod));
				var fixedArguments = new object[] { "hello world", 20 };
				var partialAppliedMethod = method.PartialApply(fixedArguments);
				Assert.IsNull(partialAppliedMethod.FixedThisArgument);
				CollectionAssert.AreEqual(fixedArguments, partialAppliedMethod.FixedArguments);
				var returnValue = partialAppliedMethod.Invoke(null, new object[] { 40L, 20 });
				Assert.IsNull(returnValue);

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
			CollectionAssert.AreEqual(expectedLogs, actualLogs.Where(x => !x.StartsWith("DEBUG")));
		}

		[Test]
		public void Control_SimpleStaticNonVoidMethod([Values] bool emptyPartialApply)
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var returnValue = SimpleStaticNonVoidMethod("mystring", 2, 4L, 100);
				Assert.AreEqual("asdf", returnValue);

				var method = typeof(MethodClosureExtensionsTestsSimple).GetMethod(nameof(SimpleStaticNonVoidMethod));
				if (emptyPartialApply)
				{
					var partialAppliedMethod = method.PartialApply();
					Assert.IsNull(partialAppliedMethod.FixedThisArgument);
					CollectionAssert.AreEqual(new object[0], partialAppliedMethod.FixedArguments);
					method = partialAppliedMethod;
				}

				returnValue = method.Invoke(null, new object[] { "mystring", 2, 4L, 100 }) as string;
				Assert.AreEqual("asdf", returnValue);

				// Static method can be invoked with a non-null target - target is just ignored in this case.
				returnValue = method.Invoke(this, new object[] { "mystring", 2, 4L, 100 }) as string;
				Assert.AreEqual("asdf", returnValue);

				var @delegate = method.CreateDelegate<Func<string, int, long, int, string>>();
				returnValue = @delegate("mystring", 2, 4L, 100);
				Assert.AreEqual("asdf", returnValue);
			}
			var expectedLogs = new[]
			{
				"s: mystring",
				"y: 2",
				"l: 4",
				"x: 100",
				"s: mystring",
				"y: 2",
				"l: 4",
				"x: 100",
				"s: mystring",
				"y: 2",
				"l: 4",
				"x: 100",
				"s: mystring",
				"y: 2",
				"l: 4",
				"x: 100",
			};
			CollectionAssert.AreEqual(expectedLogs, actualLogs.Where(x => !x.StartsWith("DEBUG")));
		}

		[Test]
		public void PartialApply_SimpleStaticNonVoidMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var method = typeof(MethodClosureExtensionsTestsSimple).GetMethod(nameof(SimpleStaticNonVoidMethod));
				var fixedArguments = new object[] { "hello world", 1, 2L };
				var partialAppliedMethod = method.PartialApply(fixedArguments);
				Assert.IsNull(partialAppliedMethod.FixedThisArgument);
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
			CollectionAssert.AreEqual(expectedLogs, actualLogs.Where(x => !x.StartsWith("DEBUG")));
		}

		[Test]
		public void Control_SimpleInstanceVoidMethod([Values] bool emptyPartialApply)
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var v = new TestStruct(-15);
				v.SimpleInstanceVoidMethod(-5, "home", "alone");

				var method = typeof(TestStruct).GetMethod(nameof(TestStruct.SimpleInstanceVoidMethod));
				if (emptyPartialApply)
				{
					var partialAppliedMethod = method.PartialApply();
					Assert.IsNull(partialAppliedMethod.FixedThisArgument);
					CollectionAssert.AreEqual(new object[0], partialAppliedMethod.FixedArguments);
					method = partialAppliedMethod;
				}

				var returnValue = method.Invoke(v, new object[] { -5, new[] { "home", "alone" } });
				Assert.IsNull(returnValue);

				var closureDelegate = method.CreateDelegate<Action<int, string[]>>(v);
				closureDelegate(-5, new[] { "home", "alone" });
			}
			var expectedLogs = new[]
			{
				"x: -15",
				"y: -5",
				"ss: string[] { home, alone }",
				"x: -15",
				"y: -5",
				"ss: string[] { home, alone }",
				"x: -15",
				"y: -5",
				"ss: string[] { home, alone }",
			};
			CollectionAssert.AreEqual(expectedLogs, actualLogs.Where(x => !x.StartsWith("DEBUG")));
		}

		delegate void SimpleInstanceVoidMethod_PartialApply_Delegate(params string[] ss);

		[Test]
		public void PartialApply_SimpleInstanceVoidMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var v = new TestStruct(15);
				var method = typeof(TestStruct).GetMethod(nameof(TestStruct.SimpleInstanceVoidMethod));
				var fixedArguments = new object[] { 5 };
				var partialAppliedMethod = method.PartialApply(fixedArguments);
				Assert.IsNull(partialAppliedMethod.FixedThisArgument);
				CollectionAssert.AreEqual(fixedArguments, partialAppliedMethod.FixedArguments);
				var returnValue = partialAppliedMethod.Invoke(v, new object[] { new string[] { "hello", "world" } });
				Assert.IsNull(returnValue);

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
			CollectionAssert.AreEqual(expectedLogs, actualLogs.Where(x => !x.StartsWith("DEBUG")));
		}

		[Test]
		public void Control_SimpleInstanceNonVoidMethod([Values] bool emptyPartialApply)
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var c = new TestClassSimple(-15);
				var returnValue = c.SimpleInstanceNonVoidMethod(-5, "home", "alone");
				Assert.AreEqual("ghkj", returnValue);

				var method = typeof(TestClassSimple).GetMethod(nameof(TestClassSimple.SimpleInstanceNonVoidMethod));
				if (emptyPartialApply)
				{
					var partialAppliedMethod = method.PartialApply();
					Assert.IsNull(partialAppliedMethod.FixedThisArgument);
					CollectionAssert.AreEqual(new object[0], partialAppliedMethod.FixedArguments);
					method = partialAppliedMethod;
				}

				returnValue = method.Invoke(c, new object[] { -5, new[] { "home", "alone" } }) as string;
				Assert.AreEqual("ghkj", returnValue);

				var closureDelegate = method.CreateDelegate<Func<int, string[], string>>(c);
				returnValue = closureDelegate(-5, new[] { "home", "alone" });
				Assert.AreEqual("ghkj", returnValue);
			}
			var expectedLogs = new[]
			{
				"x: -15",
				"y: -5",
				"ss: string[] { home, alone }",
				"x: -15",
				"y: -5",
				"ss: string[] { home, alone }",
				"x: -15",
				"y: -5",
				"ss: string[] { home, alone }",
			};
			CollectionAssert.AreEqual(expectedLogs, actualLogs.Where(x => !x.StartsWith("DEBUG")));
		}

		[Test]
		public void PartialApply_SimpleInstanceNonVoidMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var c = new TestClassSimple(15);
				var method = typeof(TestClassSimple).GetMethod(nameof(TestClassSimple.SimpleInstanceNonVoidMethod));
				var fixedArguments = new object[] { 5 };
				var partialAppliedMethod = method.PartialApply(fixedArguments);
				Assert.IsNull(partialAppliedMethod.FixedThisArgument);
				CollectionAssert.AreEqual(fixedArguments, partialAppliedMethod.FixedArguments);

				var returnValue = partialAppliedMethod.Invoke(c, new object[] { new string[] { "hello", "world" } }) as string;
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
			CollectionAssert.AreEqual(expectedLogs, actualLogs.Where(x => !x.StartsWith("DEBUG")));
		}

		[Test]
		public void Control_SimpleVirtualInstanceVoidMethod([Values] bool emptyPartialApply)
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var c = new TestClassSimple(-15);
				c.SimpleVirtualInstanceVoidMethod(-5, "home", "alone");

				var method = typeof(TestClassSimple).GetMethod(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod));
				if (emptyPartialApply)
				{
					var partialAppliedMethod = method.PartialApply();
					Assert.IsNull(partialAppliedMethod.FixedThisArgument);
					CollectionAssert.AreEqual(new object[0], partialAppliedMethod.FixedArguments);
					method = partialAppliedMethod;
				}

				var returnValue = method.Invoke(c, new object[] { -5, new[] { "home", "alone" } });
				Assert.IsNull(returnValue);

				var closureDelegate = method.CreateDelegate<Action<int, string[]>>(c);
				closureDelegate(-5, new[] { "home", "alone" });
			}
			var expectedLogs = new[]
			{
				"x: -15",
				"y: -5",
				"ss: string[] { home, alone }",
				"x: -15",
				"y: -5",
				"ss: string[] { home, alone }",
				"x: -15",
				"y: -5",
				"ss: string[] { home, alone }",
			};
			CollectionAssert.AreEqual(expectedLogs, actualLogs.Where(x => !x.StartsWith("DEBUG")));
		}

		[Test]
		public void PartialApply_SimpleVirtualInstanceVoidMethod()
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var c = new TestClassSimple(15);
				var method = typeof(TestClassSimple).GetMethod(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod));
				var fixedArguments = new object[] { 5 };
				var partialAppliedMethod = method.PartialApply(fixedArguments);
				Assert.IsNull(partialAppliedMethod.FixedThisArgument);
				CollectionAssert.AreEqual(fixedArguments, partialAppliedMethod.FixedArguments);

				var returnValue = partialAppliedMethod.Invoke(c, new object[] { new string[] { "hello", "world" } });
				Assert.IsNull(returnValue);

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
			CollectionAssert.AreEqual(expectedLogs, actualLogs.Where(x => !x.StartsWith("DEBUG")));
		}

		// TODO: Test PartialApply with fixed arguments for all parameters.
		// TODO: Test multiple PartialApply.

		// TODO: Test Bind on instance method.

		// TODO: Test Bind on PartialApply on instance method.

		// TODO: Test PartialApply on Bind on instance method.
	}
}
