using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace LbmLib.Language.Experimental.Tests
{
	[TestFixture]
	public class MethodClosureExtensionsTestsGC : MethodClosureExtensionsBase
	{
		[Test]
		[NonParallelizable]
		public void ClosureMethod_DelegateRegistry_GC()
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				AssertEmptyDelegateRegistryAfterTryFullGCFinalization("after initial GC");
				// Note: It seems that even null-ing out a variable that holds the only reference to an object doesn't necessarily allow the object to be
				// finalizable until after the method ends, so putting all the logic that stores delegates into variables into another method.
				ClosureMethod_DelegateRegistry_GC_Internal();
				AssertDelegateRegistryCount("before final GC", 1);
				// MethodClosureExtensionsFixture's Dispose will call AssertEmptyDelegateRegistryAfterTryFullGCFinalization, so don't need to call it here.
			}
		}

		void ClosureMethod_DelegateRegistry_GC_Internal()
		{
			var method = typeof(MethodClosureExtensionsTestsFancy).GetMethod(nameof(MethodClosureExtensionsTestsFancy.FancyStaticNonVoidMethod));
			var partialAppliedDelegate = default(MethodClosureExtensionsTestsFancy.FancyStaticNonVoidMethod_PartialApply_Delegate);
			for (var i = 1; i <= 20; i++)
			{
				var partialAppliedMethod = method.PartialApply("hello", "world", new TestStruct(10), new TestStruct(15), 20, 25, new TestClass(30), new TestClass(35));
				partialAppliedDelegate = partialAppliedMethod.CreateDelegate<MethodClosureExtensionsTestsFancy.FancyStaticNonVoidMethod_PartialApply_Delegate>();
				if (i % 5 == 0)
				{
					AssertDelegateRegistryCount($"before {i} GC", i == 5 ? 5 : 6);
					TryFullGCFinalization();
					AssertDelegateRegistryCount($"after {i} GC", 1);
				}
			}
			// Test that the latest partially applied method delegate still works.
			var x = 20;
			partialAppliedDelegate(null, new List<string>() { "qwerty" }, 40L, ref x);
			Assert.AreEqual(20 * 20, x);
		}

		static void AssertDelegateRegistryCount(string logLabel, int expectedCount)
		{
			try
			{
				Assert.AreEqual(expectedCount, ClosureMethod.DelegateRegistry.Closures.Where(closure => !(closure is null)).Count());
			}
			catch (Exception ex)
			{
				Logging.Log($"DEBUG {logLabel}:\n{ClosureMethod.DelegateRegistry}");
				throw ex;
			}
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

		// This is also called in the MethodClosureExtensionsTests.Base TearDown, and if tests are run in parallel, this needs to be thread-safe;
		// hence, the locking here.
		internal static void AssertEmptyDelegateRegistryAfterTryFullGCFinalization(string logLabel)
		{
			lock (lockObj)
			{
				TryFullGCFinalization();
				try
				{
					Assert.AreEqual(0, ClosureMethod.DelegateRegistry.Closures.Where(closure => !(closure is null)).Count());
					Assert.AreEqual(0, ClosureMethod.DelegateRegistry.MinimumFreeClosureKey);
				}
				catch (Exception ex)
				{
					Logging.Log($"DEBUG {logLabel}:\n{ClosureMethod.DelegateRegistry}");
					throw ex;
				}
			}
		}

		static readonly object lockObj = new object();
	}
}
