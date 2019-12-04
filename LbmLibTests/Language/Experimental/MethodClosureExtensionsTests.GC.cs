using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace LbmLib.Language.Experimental.Tests
{
	[TestFixture]
	public class MethodClosureExtensionsTestsGC : MethodClosureExtensionsBase
	{
		[Test]
		[NonParallelizable]
		public void ClosureMethod_Registry_GC([Values] bool gcAtIntervals)
		{
			int delegateCount = 20;
			int gcInterval = 5;
			MethodClosureExtensionsFixture.Do(fixture =>
			{
				fixture.AssertClosureRegistryCountAfterFullGCFinalization(0, "after initial GC");
				var method = typeof(MethodClosureExtensionsTestsFancy).GetMethod(nameof(MethodClosureExtensionsTestsFancy.FancyStaticNonVoidMethod));
				MethodClosureExtensionsTestsFancy.FancyStaticNonVoidMethod_PartialApply_Delegate partialAppliedDelegate1 = null, partialAppliedDelegate2 = null;
				for (var i = 1; i <= delegateCount; i++)
				{
					var partialAppliedMethod = method.PartialApply("hello", "world", new TestStruct(10), new TestStruct(15), 20, 25, new TestClass(30), new TestClass(35));
					partialAppliedDelegate1 = partialAppliedMethod.CreateDelegate<MethodClosureExtensionsTestsFancy.FancyStaticNonVoidMethod_PartialApply_Delegate>();
					partialAppliedDelegate2 = partialAppliedMethod.CreateDelegate<MethodClosureExtensionsTestsFancy.FancyStaticNonVoidMethod_PartialApply_Delegate>();
					if (i % 5 == 0 && gcAtIntervals)
					{
						//Logging.Log($"DEBUG before GC {i / gcInterval}:\n{ClosureMethod.Registry}");
						// Note: Since GCs can be triggered at any moment before this point, we can't deterministically determine # active closures are in the registry.
						// We can only deterministically determine the # active closures after a "full" GC and before any further ClosureMethod.CreateDelegate calls.
						fixture.AssertClosureRegistryCountAfterFullGCFinalization(2, $"after GC {i / gcInterval}");
					}
				}
				if (!gcAtIntervals)
					fixture.AssertClosureRegistryCountAfterFullGCFinalization(2, $"after GC {delegateCount / gcInterval}");
				// Test that the latest partially applied method delegate still works.
				var x = 20;
				partialAppliedDelegate1(null, new List<string>() { "qwerty" }, 40L, ref x);
				Assert.AreEqual(20 * 20, x);
				partialAppliedDelegate2(null, new List<string>() { "qwerty" }, 40L, ref x);
				Assert.AreEqual(20 * 20 * 20 * 20, x);
				//Logging.Log($"DEBUG before final GC:\n{ClosureMethod.Registry}");
				// MethodClosureExtensionsFixture will call AssertEmptyClosureRegistryAfterTryFullGCFinalization a final time, so don't need to call it here.
			});
		}

		[Test]
		[NonParallelizable]
		public void ClosureMethod_Registry_GC_Multithreaded([Values] bool gcAtIntervals)
		{
			var threadCount = 10;
			var delegatePerThreadCount = 10;
			var delegatePerThreadGCInterval = 5;

			MethodClosureExtensionsFixture.Do(fixture =>
			{
				fixture.AssertClosureRegistryCountAfterFullGCFinalization(0, "after initial GC");

				var method = typeof(MethodClosureExtensionsTestsFancy).GetMethod(nameof(MethodClosureExtensionsTestsFancy.FancyStaticNonVoidMethod));
				var partialAppliedMethod = method.PartialApply("hello", "world", new TestStruct(10), new TestStruct(15), 20, 25, new TestClass(30), new TestClass(35));

				var threads = new Thread[threadCount];
				var partialAppliedDelegatePerThread = new MethodClosureExtensionsTestsFancy.FancyStaticNonVoidMethod_PartialApply_Delegate[threadCount];
				var unexpectedExceptionPerThread = new Exception[threadCount];
				var threadStartResetEvent = new ManualResetEvent(false);
				for (var j = 0; j < threadCount; j++)
				{
					// Need a copy of the iteration variable for usage within the thread, since direct access to it is effectively by-ref,
					// such that within the threads, it would only see it as its final value (threadCount).
					var threadIndex = j;
					var thread = new Thread(() =>
					{
						// Wait until all threads are started (threadStartResetEvent.Set() is called in the main thread once all threads are started).
						//Logging.Log($"DEBUG thread {threadIndex} init");
						threadStartResetEvent.WaitOne();

						Logging.Log($"DEBUG thread {threadIndex} started");
						try
						{
							for (var i = 0; i < delegatePerThreadCount; i++)
							{
								// This local function ensures that any (implicit) local variables within its body are finalizable afterwards.
								// This is a workaround for the issue where local variables in a method are not finalizable after last usage in DEBUG builds.
								void CreateTestDelegate()
								{
									// Note: CreateDelegate has to go within the lock (if locking is needed), since it modifies the closure registry.
									partialAppliedDelegatePerThread[threadIndex] =
										partialAppliedMethod.CreateDelegate<MethodClosureExtensionsTestsFancy.FancyStaticNonVoidMethod_PartialApply_Delegate>();
									if (i % delegatePerThreadGCInterval == 0)
									{
										// Test that the partially applied method delegate still works in this thread.
										//Logging.Log($"DEBUG before delegate call {i / delegatePerThreadGCInterval} in thread {threadIndex}");
										var x = threadIndex * i;
										partialAppliedDelegatePerThread[threadIndex](null, new List<string>() { "qwerty" }, x, ref x);
										Assert.AreEqual(threadIndex * i * threadIndex * i, x);
									}
								}
								if (gcAtIntervals)
								{
									lock (fixture.GCSync)
										CreateTestDelegate();
								}
								else
								{
									CreateTestDelegate();
								}

								if (i % delegatePerThreadGCInterval == 0 && gcAtIntervals)
								{
									lock (fixture.GCSync)
									{
										Logging.Log($"DEBUG before GC {i / delegatePerThreadGCInterval} in thread {threadIndex}:\n{ClosureMethod.Registry}");
										var delegateCount = partialAppliedDelegatePerThread.Where(@delegate => !(@delegate is null)).Count();
										var logLabel = $"after GC {i / delegatePerThreadGCInterval} in thread {threadIndex}";
										Assert.Greater(delegateCount, 0, logLabel);
										fixture.AssertClosureRegistryCountAfterFullGCFinalization(delegateCount, logLabel);
									}
								}
							}
						}
						catch (Exception exception)
						{
							unexpectedExceptionPerThread[threadIndex] = exception;
							Logging.Log($"DEBUG exception in thread {threadIndex}:\n{exception}");
							throw;
						}
						finally
						{
							Logging.Log($"DEBUG thread {threadIndex} finished");
						}
					});
					threads[threadIndex] = thread;
					thread.Start();
				}

				var timeoutThread = new Thread(() =>
				{
					var timeout = TimeSpan.FromSeconds(20);
					Thread.Sleep(timeout);
					Logging.Log($"DEBUG timeout after {timeout.Seconds} seconds - aborting all test threads");
					foreach (var thread in threads)
					{
						if (thread.IsAlive)
							thread.Abort();
					}
				});
				timeoutThread.Start();

				threadStartResetEvent.Set();
				foreach (var thread in threads)
					thread.Join();

				for (var threadIndex = 0; threadIndex < threadCount; threadIndex++)
				{
					var exception = unexpectedExceptionPerThread[threadIndex];
					if (!(exception is null))
					{
						// Wrapping in a new exception to preserve the stack trace (since throw sets thrown exception's stack trace).
						throw new Exception("Unexpected exception", exception);
					}
				}

				lock (fixture.GCSync)
				{
					//Logging.Log($"DEBUG before GC after all threads joined:\n{ClosureMethod.Registry}");
					fixture.AssertClosureRegistryCountAfterFullGCFinalization(threadCount, "after GC after all threads joined");
				}

				for (var threadIndex = 0; threadIndex < threadCount; threadIndex++)
				{
					// Test that the latest partially applied method delegate still works.
					//Logging.Log($"DEBUG before final delegate call created in thread {threadIndex} and called in main thread");
					var x = threadIndex;
					partialAppliedDelegatePerThread[threadIndex](null, new List<string>() { "qwerty" }, 40L, ref x);
					Assert.AreEqual(threadIndex * threadIndex, x);
					// TODO: Why is this now necessary? Shouldn't partialAppliedDelegatePerThread going out of scope be sufficient?
					partialAppliedDelegatePerThread[threadIndex] = null;
				}
				//lock (fixture.GCSync)
				//{
				//	Logging.Log($"DEBUG before final GC:\n{ClosureMethod.Registry}");
				//	// MethodClosureExtensionsFixture will call AssertEmptyClosureRegistryAfterTryFullGCFinalization a final time,
				//	// so don't need to call it here.
				//}
			});
		}
	}
}
