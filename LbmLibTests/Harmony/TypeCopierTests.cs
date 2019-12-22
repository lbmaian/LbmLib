using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Harmony;
using LbmLib.Language;
using NUnit.Framework;

namespace LbmLib.Harmony.Tests
{
	[TestFixture]
	public class TypeCopierTests
	{
		[SetUp]
		public void SetUp()
		{
			// Using the ConsoleErrorLogger so that the logs are also written to the Tests output pane in Visual Studio.
			Logging.DefaultLogger = log => Logging.ConsoleErrorLogger(log);
		}

		[Test]
		public void Test()
		{
			var saveDirectory = Path.Combine(Directory.GetCurrentDirectory(), "DebugAssembly");
			var patchedAssembly = new TypeCopier()
				.AddOriginalType(typeof(TestStaticClass1))
				.AddMethodTranspiler(typeof(TestStaticClass1).TypeInitializer,
					typeof(TypeCopierTests).GetMethod(nameof(StaticConstructorTranspiler), AccessTools.all))
				.CreateAssembly(saveDirectory);
			var patchedType = patchedAssembly.GetType(typeof(TestStaticClass1).FullName);
			Logging.Log(patchedType.ToDebugString());
			RuntimeHelpers.RunClassConstructor(patchedType.TypeHandle);
		}

		static IEnumerable<CodeInstruction> StaticConstructorTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			foreach (var instruction in instructions)
			{
				if (instruction.operand is "Foo")
					yield return new CodeInstruction(OpCodes.Ldstr, "Baz");
				else
					yield return instruction;
			}
		}

		static class TestStaticClass1
		{
			//static readonly List<Sample<string, int>> sampleField;
			static readonly List<object> sampleField;

			struct Sample<T, R>
			{
				public T Value { get; }
				public Sample(T value) => Value = value;
			}

			static TestStaticClass1()
			{
				sampleField = new List<object>() { new Sample<string, int>("Foo") };
				Logging.Log(SampleToString<string>());
				Logging.Log(typeof(TestStaticClass2).ToDebugString());
				//TestStaticClass2.Bar();
				//Action action = TestStaticClass2.Bar;
				//Logging.Log(action);
			}

			static string SampleToString<T>() => sampleField.Join(sample => ((Sample<string, int>)sample).Value, ", ");
		}

		static class TestStaticClass2
		{
			static TestStaticClass2()
			{
				Bar();
			}

			public static void Bar() => Logging.Log("Bar");
		}
	}
}
