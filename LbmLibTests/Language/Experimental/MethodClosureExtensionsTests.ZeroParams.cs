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
		public TestStruct ZeroParamsInstanceNonVoidMethod()
		{
			Logging.Log(X, "ZeroParamsInstanceNonVoidMethod");
			return this;
		}
	}

	public class TestClassZeroParams : TestClass
	{
		public TestClassZeroParams(int x) : base(x)
		{
		}

		public virtual TestClass ZeroParamsVirtualInstanceNonVoidMethod()
		{
			Logging.Log(X, "ZeroParamsVirtualInstanceNonVoidMethod");
			return this;
		}
	}

	[TestFixture]
	public class MethodClosureExtensionsTestsZeroParams
	{
		[OneTimeSetUp]
		public static void SetUpOnce()
		{
			Logging.DefaultLogger = Logging.ConsoleLogger;
		}

		public static void ZeroParamsStaticVoidMethod()
		{
			Logging.Log("ZeroParamsStaticVoidMethod");
		}

		public static double ZeroParamsStaticNonVoidMethod()
		{
			Logging.Log("ZeroParamsStaticNonVoidMethod");
			return Math.PI;
		}

		[Test]
		public void PartialApply_ZeroParamsStaticVoidMethod([Values] bool additionalEmptyPartialApply)
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var method = typeof(MethodClosureExtensionsTestsZeroParams).GetMethod(nameof(ZeroParamsStaticVoidMethod));
				method = method.PartialApply();
				if (additionalEmptyPartialApply)
					method = method.PartialApply();
				var closureMethod = (ClosureMethod)method;
				Assert.IsNull(closureMethod.FixedThisArgument);
				CollectionAssert.AreEqual(new object[0], closureMethod.FixedArguments);

				var returnValue = method.Invoke(null, new object[0]);
				Assert.IsNull(returnValue);

				var @delegate = method.CreateDelegate<Action>();
				@delegate();
			}
			var expectedLogs = new[]
			{
				"ZeroParamsStaticVoidMethod",
				"ZeroParamsStaticVoidMethod",
			};
			CollectionAssert.AreEqual(expectedLogs, actualLogs.Where(x => !x.StartsWith("DEBUG")));
		}

		[Test]
		public void PartialApply_ZeroParamsStaticNonVoidMethod([Values] bool additionalEmptyPartialApply)
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var method = typeof(MethodClosureExtensionsTestsZeroParams).GetMethod(nameof(ZeroParamsStaticNonVoidMethod));
				method = method.PartialApply();
				if (additionalEmptyPartialApply)
					method = method.PartialApply();
				var closureMethod = (ClosureMethod)method;
				Assert.IsNull(closureMethod.FixedThisArgument);
				CollectionAssert.AreEqual(new object[0], closureMethod.FixedArguments);

				var returnValue = (double)method.Invoke(null, new object[0]);
				Assert.AreEqual(Math.PI, returnValue);

				var @delegate = method.CreateDelegate<Func<double>>();
				returnValue = @delegate();
				Assert.AreEqual(Math.PI, returnValue);
			}
			var expectedLogs = new[]
			{
				"ZeroParamsStaticNonVoidMethod",
				"ZeroParamsStaticNonVoidMethod",
			};
			CollectionAssert.AreEqual(expectedLogs, actualLogs.Where(x => !x.StartsWith("DEBUG")));
		}

		[Test]
		public void PartialApply_ZeroParamsInstanceNonVoidMethod([Values] bool additionalEmptyPartialApply)
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var v = new TestStruct(7);
				var method = typeof(TestStruct).GetMethod(nameof(TestStruct.ZeroParamsInstanceNonVoidMethod));
				method = method.PartialApply();
				if (additionalEmptyPartialApply)
					method = method.PartialApply();
				var closureMethod = (ClosureMethod)method;
				Assert.IsNull(closureMethod.FixedThisArgument);
				CollectionAssert.AreEqual(new object[0], closureMethod.FixedArguments);

				var returnValue = (TestStruct)method.Invoke(v, new object[0]);
				Assert.AreEqual(v, returnValue);

				var @delegate = method.CreateDelegate<Func<TestStruct>>(v);
				returnValue = @delegate();
				Assert.AreEqual(v, returnValue);
			}
			var expectedLogs = new[]
			{
				"ZeroParamsInstanceNonVoidMethod: 7",
				"ZeroParamsInstanceNonVoidMethod: 7",
			};
			CollectionAssert.AreEqual(expectedLogs, actualLogs.Where(x => !x.StartsWith("DEBUG")));
		}

		[Test]
		public void PartialApply_ZeroParamsVirtualInstanceNonVoidMethod([Values] bool additionalEmptyPartialApply)
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var c = new TestClassZeroParams(9);
				var method = typeof(TestClassZeroParams).GetMethod(nameof(TestClassZeroParams.ZeroParamsVirtualInstanceNonVoidMethod));
				method = method.PartialApply();
				if (additionalEmptyPartialApply)
					method = method.PartialApply();
				var closureMethod = (ClosureMethod)method;
				Assert.IsNull(closureMethod.FixedThisArgument);
				CollectionAssert.AreEqual(new object[0], closureMethod.FixedArguments);

				var returnValue = method.Invoke(c, new object[0]) as TestClass;
				Assert.AreSame(c, returnValue);

				var @delegate = method.CreateDelegate<Func<TestClass>>(c);
				returnValue = @delegate();
				Assert.AreSame(c, returnValue);
			}
			var expectedLogs = new[]
			{
				"ZeroParamsVirtualInstanceNonVoidMethod: 9",
				"ZeroParamsVirtualInstanceNonVoidMethod: 9",
			};
			CollectionAssert.AreEqual(expectedLogs, actualLogs.Where(x => !x.StartsWith("DEBUG")));
		}

		public void Bind_ZeroParamsInstanceNonVoidMethod([Values] bool emptyPartialApplyBefore, [Values] bool emptyPartialApplyAfter)
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var v = new TestStruct(11);
				var method = typeof(TestStruct).GetMethod(nameof(TestStruct.ZeroParamsInstanceNonVoidMethod));
				if (emptyPartialApplyBefore)
					method = method.PartialApply();
				method = method.Bind(v);
				if (emptyPartialApplyAfter)
					method = method.PartialApply();
				var closureMethod = (ClosureMethod)method;
				Assert.AreEqual(v, closureMethod.FixedThisArgument);
				CollectionAssert.AreEqual(new object[0], closureMethod.FixedArguments);

				var returnValue = (TestStruct)method.Invoke(null, new object[0]);
				Assert.AreEqual(v, returnValue);

				var @delegate = method.CreateDelegate<Func<TestStruct>>();
				returnValue = @delegate();
				Assert.AreEqual(v, returnValue);
			}
			var expectedLogs = new[]
			{
				"ZeroParamsInstanceNonVoidMethod: 11",
				"ZeroParamsInstanceNonVoidMethod: 11",
			};
			CollectionAssert.AreEqual(expectedLogs, actualLogs.Where(x => !x.StartsWith("DEBUG")));
		}

		[Test]
		public void Bind_ZeroParamsVirtualInstanceNonVoidMethod([Values] bool emptyPartialApplyBefore, [Values] bool emptyPartialApplyAfter)
		{
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				var c = new TestClassZeroParams(13);
				var method = typeof(TestClassZeroParams).GetMethod(nameof(TestClassZeroParams.ZeroParamsVirtualInstanceNonVoidMethod));
				if (emptyPartialApplyBefore)
					method = method.PartialApply();
				method = method.Bind(c);
				if (emptyPartialApplyAfter)
					method = method.PartialApply();
				var closureMethod = (ClosureMethod)method;
				Assert.AreSame(c, closureMethod.FixedThisArgument);
				CollectionAssert.AreEqual(new object[0], closureMethod.FixedArguments);

				var returnValue = method.Invoke(null, new object[0]) as TestClass;
				Assert.AreSame(c, returnValue);

				var @delegate = method.CreateDelegate<Func<TestClass>>();
				returnValue = @delegate();
				Assert.AreSame(c, returnValue);
			}
			var expectedLogs = new[]
			{
				"ZeroParamsVirtualInstanceNonVoidMethod: 13",
				"ZeroParamsVirtualInstanceNonVoidMethod: 13",
			};
			CollectionAssert.AreEqual(expectedLogs, actualLogs.Where(x => !x.StartsWith("DEBUG")));
		}
	}
}
