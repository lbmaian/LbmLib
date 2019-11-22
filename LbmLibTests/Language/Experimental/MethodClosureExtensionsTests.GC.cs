using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace LbmLib.Language.Experimental.Tests
{
	[TestFixture]
	public class MethodClosureExtensionsTestsGC
	{
		[OneTimeSetUp]
		public static void SetUpOnce()
		{
			Logging.DefaultLogger = Logging.ConsoleLogger;
		}

		[Test]
		public void ClosureMethod_DelegateRegistry_GC()
		{
			TryFullGCFinalization();
			var actualLogs = new List<string>();
			using (Logging.With(log => actualLogs.Add(log)))
			{
				// Note: Even null-ing out a variable that holds the only reference to an object doesn't actually allow the object to be
				// finalizable until after the method ends, so putting all the logic that stores delegates into variables into another method.
				ClosureMethod_DelegateRegistry_GC_Internal();
				//Logging.Log("DEBUG before final GC:\n" + ClosureMethod.DelegateRegistry);
				Assert.AreEqual(1, ClosureMethod.DelegateRegistry.Closures.Where(closure => !(closure is null)).Count());
				TryFullGCFinalization();
				//Logging.Log("DEBUG after final GC:\n" + ClosureMethod.DelegateRegistry);
				Assert.AreEqual(0, ClosureMethod.DelegateRegistry.Closures.Where(closure => !(closure is null)).Count());
			}
			//Logging.Log(actualLogs.Join("\n"));
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
					//Logging.Log($"DEBUG before {i} GC:\n" + ClosureMethod.DelegateRegistry);
					Assert.AreEqual(i == 5 ? 5 : 6, ClosureMethod.DelegateRegistry.Closures.Where(closure => !(closure is null)).Count());
					TryFullGCFinalization();
					//Logging.Log($"DEBUG after {i} GC:\n" + ClosureMethod.DelegateRegistry);
					Assert.AreEqual(1, ClosureMethod.DelegateRegistry.Closures.Where(closure => !(closure is null)).Count());
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
