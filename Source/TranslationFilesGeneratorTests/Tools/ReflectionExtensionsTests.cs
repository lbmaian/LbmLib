using System.Reflection;
using NUnit.Framework;

namespace TranslationFilesGenerator.Tools.Tests
{
	[TestFixture]
	public class ReflectionExtensionsTests
	{
		static void SampleStaticVoidMethod(ref int x, string s, TestStruct v, int y, TestClass c, TestClass @null, long l)
		{
			Logging.Log(x, "x");
			Logging.Log(s, "s");
			Logging.Log(v.X, "v.X");
			Logging.Log(y, "y");
			Logging.Log(c.X, "c.X");
			Logging.Log(@null, "@null");
			Logging.Log(l, "l");
			x = x * x;
		}

		struct TestStruct
		{
			public int X;

			public TestStruct(int x)
			{
				X = x;
			}
		}

		class TestClass
		{
			public int X;

			public TestClass(int x)
			{
				X = x;
			}
		}

		[Test]
		public void DynamicPartialApplyTest()
		{
			var x = 100;
			SampleStaticVoidMethod(ref x, "mystring", new TestStruct(1), 2, new TestClass(3), null, 4L);
			var method = GetType().GetMethod(nameof(SampleStaticVoidMethod), BindingFlags.Static | BindingFlags.NonPublic);
			var partialAppliedMethod = method.DynamicPartialApply("hello world", new TestStruct(10), 20, new TestClass(30), null, 40L);
			var nonFixedArguments = new object[] { 20 };
			partialAppliedMethod.Invoke(null, new object[] { x });
			Assert.AreEqual(20 * 20, nonFixedArguments[0]);
		}
	}
}
