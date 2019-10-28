using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace LbmLib.Tests
{
	[TestFixture]
	public class ReflectionExtensionsTests
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
		}

		public class TestClass
		{
			public int X;

			public TestClass(int x)
			{
				X = x;
			}
		}

		public static void SimpleStaticVoidMethod(string s, int y, long l, int x)
		{
			Logging.Log(s, "s");
			Logging.Log(y, "y");
			Logging.Log(l, "l");
			Logging.Log(x, "x");
		}


		[Test]
		public void DynamicPartialApplyTest_SimpleStaticVoidMethod()
		{
			SimpleStaticVoidMethod("mystring", 2, 4L, 100);
			var method = GetType().GetMethod(nameof(SimpleStaticVoidMethod));
			var partialAppliedMethod = method.DynamicPartialApply("hello world", 20, 40L);
			var nonFixedArguments = new object[] { 20 };
			partialAppliedMethod.Invoke(null, new object[] { 100 });
		}

		public static void SampleStaticVoidMethod(string s, TestStruct v, int y, TestClass c, TestClass @null, long l, ref int x)
		{
			Logging.Log(s, "s");
			Logging.Log(v.X, "v.X");
			Logging.Log(y, "y");
			Logging.Log(c.X, "c.X");
			Logging.Log(@null, "@null");
			Logging.Log(l, "l");
			Logging.Log(x, "x");
			x *= x;
		}

		[Test]
		public void DynamicPartialApplyTest_SampleStaticVoidMethod()
		{
			var x = 100;
			SampleStaticVoidMethod("mystring", new TestStruct(1), 2, new TestClass(3), null, 4L, ref x);
			var method = GetType().GetMethod(nameof(SampleStaticVoidMethod));
			var partialAppliedMethod = method.DynamicPartialApply("hello world", new TestStruct(10), 20, new TestClass(30), null, 40L);
			var nonFixedArguments = new object[] { 20 };
			partialAppliedMethod.Invoke(null, nonFixedArguments);
			Assert.AreEqual(20 * 20, nonFixedArguments[0]);
		}

		[Test]
		public void DynamicPartialApplyTest_SampleStaticVoidMethod_MultipleTimes()
		{
			var list = new List<string>();
			using (Logging.With(x => list.Add(x)))
			{
				var x = 100;
				SampleStaticVoidMethod("mystring", new TestStruct(1), 2, new TestClass(3), null, 4L, ref x);
				var method = GetType().GetMethod(nameof(SampleStaticVoidMethod));
				var partialAppliedMethod = default(MethodInfo);
				for (var i = 0; i < 20; i++)
				{
					partialAppliedMethod = method.DynamicPartialApply("hello world", new TestStruct(10), 20, new TestClass(30), null, 40L);
					if (i % 5 == 0)
					{
						GC.Collect();
						GC.WaitForPendingFinalizers();
					}
				}
				var nonFixedArguments = new object[] { 20 };
				partialAppliedMethod.Invoke(null, nonFixedArguments);
				Assert.AreEqual(20 * 20, nonFixedArguments[0]);
				partialAppliedMethod = null;
				GC.Collect();
				GC.WaitForPendingFinalizers();
			}
			Logging.Log(list.Join("\n"));
		}

		// TODO: Test DynamicPartialApply on method with return value.

		// TODO: Test DynamicPartialApply on instance method.

		// TODO: Test DynamicBind on static method => throws exception.

		// TODO: Test DynamicBind on instance method.

		// TODO: Test DynamicBind on DynamicPartialApply on static method => throws exception.

		// TODO: Test DynamicBind on DynamicPartialApply on instance method.
	}
}
