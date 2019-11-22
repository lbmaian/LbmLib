using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace LbmLib.Language.Experimental.Tests
{
	// Note: Method and structure fixtures are public so that methods dynamically created via DebugDynamicMethodBuilder have access to them.

	// structs have no inheritance, so using partial struct as a workaround.
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

	public class TestClass
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

	public class MethodClosureExtensionsBase
	{
		[SetUp]
		public void SetUp()
		{
			// Using the ConsoleErrorLogger so that the logs are also written to the Tests output pane in Visual Studio.
			Logging.DefaultLogger = log => Logging.ConsoleErrorLogger(log);

			Logging.Log("Started test: " + TestContext.CurrentContext.Test.FullName);
		}

		[TearDown]
		public void TearDown()
		{
			var testContext = TestContext.CurrentContext;
			Logging.Log("Finished test: " + testContext.Test.FullName);
			var testResult = testContext.Result;
			if (testResult.Outcome == ResultState.Error)
				Logging.Log($"Encountered unexpected exception:\nMessage: {testResult.Message}\nStack Trace:\n{testResult.StackTrace}");
			var nonPassedAssertions = testResult.Assertions.Where(assertion => assertion.Status != AssertionStatus.Passed);
			if (nonPassedAssertions.Any())
				Logging.Log(nonPassedAssertions.Join("\n"));
		}

		// This is a workaround for NUnit not instantiating a new instance of the test class per test,
		// and thus not being thread-safe if the tests need mutable fields.
		// Tests will use the following pattern:
		// using (var fixture = new MethodClosureExtensionsFixture())
		// {
		//     // code that includes Logging.Log(...)
		//     fixture.ExpectedLogs = ...;
		// }
		protected sealed class MethodClosureExtensionsFixture : IDisposable
		{
			public readonly List<string> ActualLogs;
			public IEnumerable<string> ExpectedLogs;

			readonly IDisposable loggingWith;

			public MethodClosureExtensionsFixture()
			{
				ActualLogs = new List<string>();
				loggingWith = Logging.With(log => ActualLogs.Add(log));
			}

			public void Dispose()
			{
				MethodClosureExtensionsTestsGC.AssertEmptyDelegateRegistryAfterTryFullGCFinalization("after test: " + TestContext.CurrentContext.Test.FullName);
				loggingWith.Dispose();
				Logging.Log(ActualLogs.Join("\n\t"), "ActualLogs");
				if (!(ExpectedLogs is null))
					CollectionAssert.AreEqual(ExpectedLogs, ActualLogs.Where(x => !x.StartsWith("DEBUG")));
			}
		}
	}
}
