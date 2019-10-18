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
		public static void ClassInitialize(TestContext context)
		{
			DebugExtensions.DefaultLogger = str => Console.WriteLine(str);
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
			Console.WriteLine(instructions.ToDebugString("before\n\t"));
			instructions.AddTryFinally(method, ilGenerator, new[]
			{
				new CodeInstruction(OpCodes.Ldstr, "Hello world"),
				new CodeInstruction(OpCodes.Call, typeof(DebugExtensions).GetMethod(nameof(DebugExtensions.ConsoleLogged)).MakeGenericMethod(typeof(string))),
				new CodeInstruction(OpCodes.Pop),
			});
			Console.WriteLine(instructions.ToDebugString("after\n\t"));
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
			Console.WriteLine(instructions.ToDebugString("before deoptimize\n\t"));
			instructions.DeoptimizeLocalVarInstructions(method, ilGenerator);
			Console.WriteLine(instructions.ToDebugString("after deoptimize\n\t"));
			instructions.ReoptimizeLocalVarInstructions();
			Console.WriteLine(instructions.ToDebugString("after reoptimize\n\t"));
			CollectionAssert.AreEqual(origInstructions, instructions);
			instructions.InsertRange(instructions.Count - 1, new[]
			{
				new CodeInstruction(OpCodes.Ldstr, "Hello world") { labels = instructions[instructions.Count - 1].labels.PopAll() },
				new CodeInstruction(OpCodes.Call, typeof(DebugExtensions).GetMethod(nameof(DebugExtensions.ConsoleLogged)).MakeGenericMethod(typeof(string))),
				new CodeInstruction(OpCodes.Pop),
			});
			Console.WriteLine(instructions.ToDebugString("after\n\t"));
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
