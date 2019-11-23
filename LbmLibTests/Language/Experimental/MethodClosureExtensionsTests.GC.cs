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
		public void ClosureMethod_Registry_GC()
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				fixture.AssertClosureRegistryCountAfterFullGCFinalization(0, "after initial GC");
				var method = typeof(MethodClosureExtensionsTestsFancy).GetMethod(nameof(MethodClosureExtensionsTestsFancy.FancyStaticNonVoidMethod));
				MethodClosureExtensionsTestsFancy.FancyStaticNonVoidMethod_PartialApply_Delegate partialAppliedDelegate1 = null, partialAppliedDelegate2 = null;
				for (var i = 1; i <= 20; i++)
				{
					var partialAppliedMethod = method.PartialApply("hello", "world", new TestStruct(10), new TestStruct(15), 20, 25, new TestClass(30), new TestClass(35));
					partialAppliedDelegate1 = partialAppliedMethod.CreateDelegate<MethodClosureExtensionsTestsFancy.FancyStaticNonVoidMethod_PartialApply_Delegate>();
					partialAppliedDelegate2 = partialAppliedMethod.CreateDelegate<MethodClosureExtensionsTestsFancy.FancyStaticNonVoidMethod_PartialApply_Delegate>();
					if (i % 5 == 0)
					{
						//Logging.Log($"DEBUG before {i} GC:\n{ClosureMethod.Registry}");
						// Note: Since GCs can be triggered at any moment before this point, we can't deterministically determine # active closures are in the registry.
						// We can only deterministically determine the # active closures after a "full" GC and before any further ClosureMethod.CreateDelegate calls.
						fixture.AssertClosureRegistryCountAfterFullGCFinalization(1, $"after GC {i}");
					}
				}
				// Test that the latest partially applied method delegate still works.
				var x = 20;
				partialAppliedDelegate1(null, new List<string>() { "qwerty" }, 40L, ref x);
				Assert.AreEqual(20 * 20, x);
				//Logging.Log($"DEBUG before final GC:\n{ClosureMethod.Registry}");
				// MethodClosureExtensionsFixture's Dispose will call AssertEmptyClosureRegistryAfterTryFullGCFinalization a final time, so don't need to call it here.
			}
		}

		// TODO: Multithreaded test.
	}
}
