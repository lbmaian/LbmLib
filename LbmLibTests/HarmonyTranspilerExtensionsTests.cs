using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using NUnit.Framework;

namespace LbmLib.Tests
{
	[TestFixture]
	public class HarmonyTranspilerExtensionsTests
	{
		HarmonyInstance harmony;

		[SetUp]
		public void SetUp()
		{
			harmony = HarmonyInstance.Create("HarmonyTranspilerExtensionsTests");
		}

		[TearDown]
		public void TearDown()
		{
			harmony.UnpatchAll(harmony.Id);
		}

		public static void SampleVoidMethod(IEnumerable<int> enumerable, Action<int> action)
		{
			var r = "";
			foreach (var item in enumerable)
			{
				// Note: The compiler often optimizes away a switch instruction into multiple other branch conditionals, regardless of build optimization setting.
				// The following switch statement is somehow kept as a switch instruction, and I don't know why, but we need to test switch instructions.
				switch (item)
				{
				case 0:
					r = "a";
					break;
				case 1:
					r = "b";
					break;
				case 4:
					r = "c";
					Logging.Log(r);
					return;
				default:
					r = "default";
					break;
				}
				action(item);
			}
			Logging.Log(r);
		}

		public static int SampleNonVoidMethod(IEnumerable<int> enumerable, Action<int> action)
		{
			var r = "";
			foreach (var item in enumerable)
			{
				// Note: The compiler often optimizes away a switch instruction into multiple other branch conditionals, regardless of build optimization setting.
				// The following switch statement is somehow kept as a switch instruction, and I don't know why, but we need to test switch instructions.
				switch (item)
				{
				case 0:
					r = "a";
					break;
				case 1:
					r = "b";
					break;
				case 4:
					r = "c";
					Logging.Log(r);
					return 100;
				default:
					r = "default";
					break;
				}
				action(item);
			}
			Logging.Log(r);
			return -1;
		}

		public static IEnumerable<CodeInstruction> TestTryFinallyTranspiler(IEnumerable<CodeInstruction> instructionEnumerable, MethodBase method, ILGenerator ilGenerator)
		{
			var instructions = instructionEnumerable.AsList();
			instructions.ToDebugString().Log("before");
			instructions.AddTryFinally(method, ilGenerator, HarmonyTranspilerDebugExtensions.StringLogInstructions("hello world"));
			instructions.ToDebugString().Log("after");
			
			return instructions;
		}

		[Test]
		public void AddTryFinallyTestVoidMethod1()
		{
			harmony.Patch(GetType().GetMethod(nameof(SampleVoidMethod)), transpiler: new HarmonyMethod(GetType().GetMethod(nameof(TestTryFinallyTranspiler))));
			var list = new List<string>();
			using (Logging.With(x => list.Add(x)))
			{
				SampleVoidMethod(new[] { 1, 2, 3, 4 }, x => Logging.Log(x));
				CollectionAssert.AreEqual(new[] { "1", "2", "3", "c", "hello world" }, list);
			}
		}

		[Test]
		public void AddTryFinallyTestNonVoidMethod1()
		{
			harmony.Patch(GetType().GetMethod(nameof(SampleNonVoidMethod)), transpiler: new HarmonyMethod(GetType().GetMethod(nameof(TestTryFinallyTranspiler))));
			var list = new List<string>();
			using (Logging.With(x => list.Add(x)))
			{
				Assert.AreEqual(100, SampleNonVoidMethod(new[] { 1, 2, 3, 4 }, x => Logging.Log(x)));
				CollectionAssert.AreEqual(new[] { "1", "2", "3", "c", "hello world" }, list);
			}
		}

		public static IEnumerable<CodeInstruction> TestDeReOptimizeLocalVarTranspiler(IEnumerable<CodeInstruction> instructionEnumerable, MethodBase method, ILGenerator ilGenerator)
		{
			var origInstructions = instructionEnumerable.ToList();
			var instructions = instructionEnumerable.AsList();
			instructions.ToDebugString().Log("before deoptimize");
			instructions.DeoptimizeLocalVarInstructions(method, ilGenerator);
			instructions.ToDebugString().Log("after deoptimize");
			instructions.ReoptimizeLocalVarInstructions();
			instructions.ToDebugString().Log("after reoptimize");
			CollectionAssert.AreEqual(origInstructions, instructions);
			instructions.SafeInsertRange(instructions.Count - 1, HarmonyTranspilerDebugExtensions.StringLogInstructions("hello world"));
			instructions.ToDebugString().Log("after");
			return instructions;
		}

		[Test]
		public void DeoptimizeLocalVarInstructionsTest()
		{
			harmony.Patch(GetType().GetMethod(nameof(SampleVoidMethod)), transpiler: new HarmonyMethod(GetType().GetMethod(nameof(TestDeReOptimizeLocalVarTranspiler))));
			var list = new List<string>();
			using (Logging.With(x => list.Add(x)))
			{
				SampleVoidMethod(new[] { 1, 2, 3, 4 }, x => Logging.Log(x));
				CollectionAssert.AreEqual(new[] { "1", "2", "3", "c", "hello world" }, list);
			}
		}
	}
}
