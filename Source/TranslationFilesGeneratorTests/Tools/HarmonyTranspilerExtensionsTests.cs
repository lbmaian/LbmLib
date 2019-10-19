using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TranslationFilesGenerator.Tools.Tests
{
	[TestClass]
	public class HarmonyTranspilerExtensionsTests
	{
		static HarmonyInstance harmony;

		[ClassInitialize]
		public static void ClassInitialize(TestContext _)
		{
			Logging.DefaultLogger = Logging.ConsoleLogger;
		}

		[TestInitialize]
		public void TestInitialize()
		{
			harmony = HarmonyInstance.Create("HarmonyTranspilerExtensionsTests");
		}

		[TestCleanup]
		public void TestCleanup()
		{
			harmony.UnpatchAll(harmony.Id);
		}

		public static void SampleVoidMethod(IEnumerable<int> enumerable, Action<int> action)
		{
			foreach (var item in enumerable)
				action(item);
		}

		public static bool SampleNonVoidMethod(IEnumerable<int> enumerable, Func<int, bool> predicate)
		{
			foreach (var item in enumerable)
				if (predicate(item))
					return true;
			return false;
		}

		public static IEnumerable<CodeInstruction> TestTryFinallyTranspiler(IEnumerable<CodeInstruction> instructionEnumerable, MethodBase method, ILGenerator ilGenerator)
		{
			var instructions = instructionEnumerable.AsList();
			instructions.ToDebugString().Log("before");
			instructions.AddTryFinally(method, ilGenerator, HarmonyTranspilerExtensions.StringLogInstructions("hello world"));
			instructions.ToDebugString().Log("after");
			return instructions;
		}

		[TestMethod]
		public void AddTryFinallyTest1()
		{
			harmony.Patch(GetType().GetMethod(nameof(SampleVoidMethod)), transpiler: new HarmonyMethod(GetType().GetMethod(nameof(TestTryFinallyTranspiler))));
			SampleVoidMethod(new[] { 1, 2, 3, 4 }, x => Console.WriteLine(x));
		}

		[TestMethod]
		public void AddTryFinallyTest2()
		{
			harmony.Patch(GetType().GetMethod(nameof(SampleNonVoidMethod)), transpiler: new HarmonyMethod(GetType().GetMethod(nameof(TestTryFinallyTranspiler))));
			Console.WriteLine(SampleNonVoidMethod(new[] { 1, 2, 3, 4 }, x => x % 2 == 0));
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
			instructions.SafeInsertRange(instructions.Count - 1, HarmonyTranspilerExtensions.StringLogInstructions("hello world"));
			instructions.ToDebugString().Log("after");
			return instructions;
		}

		[TestMethod]
		public void DeoptimizeLocalVarInstructionsTest()
		{
			harmony.Patch(GetType().GetMethod(nameof(SampleVoidMethod)), transpiler: new HarmonyMethod(GetType().GetMethod(nameof(TestDeReOptimizeLocalVarTranspiler))));
			SampleVoidMethod(new[] { 1, 2, 3, 4 }, x => Console.WriteLine(x));
		}
	}
}
