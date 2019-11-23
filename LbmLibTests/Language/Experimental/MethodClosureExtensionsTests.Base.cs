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
		//     // code that (in)directly includes Logging.Log(...)
		//     fixture.ExpectedLogs = ...;
		// }
		public sealed class MethodClosureExtensionsFixture : IDisposable
		{
			public readonly List<string> ActualLogs;
			public IEnumerable<string> ExpectedLogs;

			readonly IDisposable loggingWithDisposable;
			readonly bool childFilter;

			public MethodClosureExtensionsFixture()
			{
				ActualLogs = new List<string>();
				loggingWithDisposable = Logging.With(log =>
				{
					// Synchronization is necessary since finalizers can log and they run in a different thread.
					lock (ActualLogs)
						ActualLogs.Add(log);
				});
				childFilter = false;
			}

			MethodClosureExtensionsFixture(List<string> actualLogs, IDisposable loggingWithDisposable, bool childFilter)
			{
				ActualLogs = actualLogs;
				this.loggingWithDisposable = loggingWithDisposable;
				this.childFilter = childFilter;
			}

			public static bool IsRunningOnMono { get; } = !(Type.GetType("Mono.Runtime") is null);

			public void Dispose()
			{
				// As this is called at the end of the test method, any variable (in)directly holding a finalizable object (such as closure owners)
				// should now have those objects be detected as finalizable. Thus, assert an empty closure registry.
				// Exception: Mono runtime apparently isn't as aggressive with this detection and still doesn't see such objects as finalizable
				// until after the method ends, so don't assert an empty closure registry in this case.
				if (!IsRunningOnMono)
					AssertClosureRegistryCountAfterFullGCFinalization(0, "after test: " + TestContext.CurrentContext.Test.FullName);

				loggingWithDisposable.Dispose();
				if (!childFilter)
				{
					lock (ActualLogs)
					{
						Logging.Log(ActualLogs.Join("\n\t"), "ActualLogs");
						if (!(ExpectedLogs is null))
							CollectionAssert.AreEqual(ExpectedLogs, ActualLogs.Where(log => !log.StartsWith("DEBUG")));
					}
				}
			}

			public MethodClosureExtensionsFixture DebugOnlyFilter()
			{
				return new MethodClosureExtensionsFixture(ActualLogs,
					Logging.With(log =>
					{
						if (log.StartsWith("DEBUG"))
						{
							lock (ActualLogs)
								ActualLogs.Add(log);
						}
					}),
					childFilter: true);
			}

			readonly object gcLockObj = new object();

			public void AssertClosureRegistryCountAfterFullGCFinalization(int expectedCount, string logLabel = null)
			{
				// If tests are run in parallel, this needs to be thread-safe; hence, the locking here.
				lock (gcLockObj)
				{
					// Doing a couple full GC iterations, since finalizers themselves create objects that need finalization,
					// which in turn can be GC'ed and need finalizing themselves, and so forth.
					// This isn't fool-proof and is probably overkill, but it should suffice for testing purposes.
					for (var gcIter = 0; gcIter < 3; gcIter++)
					{
						// Garbage collect any finalized objects and identify finalizable objects.
						GC.Collect(generation: 2);
						// Finalize found finalizable objects.
						GC.WaitForPendingFinalizers();
					}

					try
					{
						Assert.AreEqual(expectedCount, ClosureMethod.Registry.FixedArgumentsRegistry.Where(closure => !(closure is null)).Count(),
							logLabel + " ClosureCount");
						if (expectedCount == 0)
							Assert.AreEqual(0, ClosureMethod.Registry.MinimumFreeRegistryKey,
								logLabel + " MinimumFreeRegistryKey");
						//Logging.Log($"DEBUG {logLabel}:\n{ClosureMethod.Registry}");
					}
					catch (Exception ex)
					{
						Logging.Log($"DEBUG {logLabel}:\n{ClosureMethod.Registry}");
						throw ex;
					}
				}
			}
		}
	}
}
