using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace LbmLib.Language.Experimental.Tests
{
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
		// MethodClosureExtensionsFixture.Do(fixture =>
		// {
		//     // code that (in)directly includes Logging.Log(...)
		//     fixture.ExpectedLogs = ...;
		// });
		protected sealed class MethodClosureExtensionsFixture
		{
			public readonly List<string> ActualLogs;
			public IEnumerable<string> ExpectedLogs;

			MethodClosureExtensionsFixture()
			{
				ActualLogs = new List<string>();
			}

			// This is the entry point for using this fixture, and a workaround for the issue where local variables in a method
			// are not finalizable after last usage in DEBUG builds (ostensibly so that the debugger still has access to them).
			// The workaround is to ensure the main body of the test method is in its own "local functions" via lambda.
			public static void Do(Action<MethodClosureExtensionsFixture> action)
			{
				var testName = TestContext.CurrentContext.Test.FullName;
				var fixture = new MethodClosureExtensionsFixture();
				var alreadyFailed = false;
				try
				{
					var loggingWithDisposable = Logging.With(log =>
					{
						// Synchronization is necessary since there are multithreaded tests,
						// and finalizers can log and run in a different thread.
						lock (fixture.ActualLogs)
							fixture.ActualLogs.Add(log);
					});
					using (loggingWithDisposable)
					{
						try
						{
							action(fixture);
						}
						catch (Exception)
						{
							alreadyFailed = true;
							throw;
						}
						try
						{
							// As this is called at the end of the test method, any variable (in)directly holding a finalizable object (such as closure owners)
							// should now have those objects be detected as finalizable. Thus, assert an empty closure registry.
							if (!alreadyFailed)
								fixture.AssertClosureRegistryCountAfterFullGCFinalization(0, "after test & final GC: " + testName);
						}
						catch (Exception)
						{
							alreadyFailed = true;
							throw;
						}
					}
				}
				finally
				{
					lock (fixture.ActualLogs)
					{
						Logging.Log(fixture.ActualLogs.Join("\n\t"), "ActualLogs");
						if (!alreadyFailed && !(fixture.ExpectedLogs is null))
							CollectionAssert.AreEqual(fixture.ExpectedLogs, fixture.ActualLogs.Where(log => !log.StartsWith("DEBUG")));
					}
					//Logging.Log("Finished MethodClosureExtensionsFixture.Do for test: " + testName);
				}
			}

			public void WithDebugOnlyFilter(Action action)
			{
				var loggingWithDisposable = Logging.With(log =>
				{
					if (log.StartsWith("DEBUG"))
					{
						lock (ActualLogs)
							ActualLogs.Add(log);
					}
				});
				using (loggingWithDisposable)
					action();
			}

			static readonly object GlobalGCSync = new object();

			// This is an instance field for convenience and parity with AssertClosureRegistryCountAfterFullGCFinalization.
			public readonly object GCSync = GlobalGCSync;

			static void TryFullGCFinalization()
			{
				// Doing a couple full GC iterations, since finalizers themselves create objects that need finalization,
				// which in turn can be GC'ed and need finalizing themselves, and so forth.
				// This isn't fool-proof and is probably overkill, but it should suffice for testing purposes.
				for (var gcIter = 0; gcIter < 3; gcIter++)
				{
					// Garbage collect any finalized objects and identify finalizable objects.
					GC.Collect();
					// Finalize found finalizable objects.
					GC.WaitForPendingFinalizers();
					// XXX: This seems to help the Mono runtime garbage collection out, at least for ensuring finalizers are run.
					Thread.Sleep(0);
				}
			}

			// This is an instance method both for convenience and to encourage usage within the fixture action to take advantage of the
			// guaranteed logging feature.
			public void AssertClosureRegistryCountAfterFullGCFinalization(int expectedCount, string logLabel)
			{
				// If tests are run in parallel, this needs to be thread-safe; hence, the locking here.
				lock (GCSync)
				{
					TryFullGCFinalization();

					// Even with the workaround of wrapping test code in "local functions" via lambdas for DEBUG builds,
					// this isn't sufficient to force the Mono runtime garbage collector to recognize that the ClosureMethod-created delegates
					// are no longer used and thus should be finalizable. At this point, we'll just have to fake it.
					// This no longer properly tests GC behavior with regards to ClosureMethod, but at least we can test that closure registry
					// deregistration works properly.
					if (DebugExtensions.IsRunningOnMono)
					{
						// We can only fully deregister all closure entries (and assert that the closure registry is empty below).
						// So we should only do this if expectedCount is 0.
						if (expectedCount != 0)
							return;
						//Logging.Log($"DEBUG {logLabel}:\n{ClosureMethod.Registry}");
						ClosureMethod.Registry.DeregisterAll(finalizedOnly: false);
					}

					try
					{
						Assert.AreEqual(expectedCount, ClosureMethod.Registry.FixedArgumentsRegistry.Where(closure => !(closure is null)).Count(),
							logLabel + ": ClosureCount");
						if (expectedCount == 0)
							Assert.AreEqual(0, ClosureMethod.Registry.MinimumFreeRegistryKey,
								logLabel + " MinimumFreeRegistryKey");
						//Logging.Log($"DEBUG {logLabel}:\n{ClosureMethod.Registry}");
					}
					catch (Exception)
					{
						Logging.Log($"DEBUG {logLabel}:\n{ClosureMethod.Registry}");
						throw;
					}
				}
			}
		}
	}
}
