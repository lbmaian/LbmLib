using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
			// In MS .NET 3.5 (or rather CLR 2.0), Assembly.GetType isn't getting the nested type given the full name of the nested class.
			// Workaround is to Assembly.GetType the top-level class, then get the nested class the old-fashioned way.
			//var patchedType = patchedAssembly.GetType(typeof(TestStaticClass1).FullName);
			var patchedType = patchedAssembly.GetType(typeof(TestStaticClass1).DeclaringType.FullName)
				.GetNestedType(typeof(TestStaticClass1).Name, AccessTools.all);
			Logging.Log(patchedType.ToDebugString());
			Logging.Log("Calling patched static constructor");
			RuntimeHelpers.RunClassConstructor(patchedType.TypeHandle);
			Logging.Log("Calling orig static constructor");
			RuntimeHelpers.RunClassConstructor(typeof(TestStaticClass1).TypeHandle);
		}

		[Explicit]
		[Test]
		public unsafe void BoxTest()
		{
			// In MS .NET, TypedReference IntPtr field for the referenced object is the first field.
			// In Mono .NET, that field is the second field, after a RuntimeTypeHandle field.
			var typedRefValueFieldOffset = !(Type.GetType("Mono.Runtime") is null) ? sizeof(RuntimeTypeHandle) : 0;
			Logging.Log("typedRefValueFieldOffset: " + typedRefValueFieldOffset);

			var methodof_GCHandle_GetHandleValue = typeof(GCHandle).GetMethod("GetHandleValue", BindingFlags.Instance | BindingFlags.NonPublic);
			var methodof_GCHandle_InternalGet = typeof(GCHandle).GetMethod("InternalGet", BindingFlags.Static | BindingFlags.NonPublic);
			Logging.Log(methodof_GCHandle_GetHandleValue);
			var sampleBox = (object)new TestStruct() { X = 1234, Y = 4321 };
			var sampleHandle = GCHandle.Alloc(sampleBox, GCHandleType.Pinned);
			Logging.Log($"(IntPtr)sampleHandle: 0x{(long)(IntPtr)sampleHandle:x16}");
			var sampleHandleValue = (IntPtr)methodof_GCHandle_GetHandleValue.Invoke(sampleHandle, new object[0]);
			Logging.Log($"sampleHandle.GetHandleValue(): 0x{sampleHandleValue:x16}");
			var sampleHandleTarget = methodof_GCHandle_InternalGet.Invoke(null, new object[] { sampleHandleValue });
			Logging.Log($"GCHandle.InternalGet(sampleHandle.GetHandleValue()): {sampleHandleTarget}");
			Logging.Log($"sampleHandle.Target: {sampleHandle.Target}");
			Logging.Log($"sampleHandle.AddrOfPinnedObject(): 0x{(long)sampleHandle.AddrOfPinnedObject():x16}");
			try
			{
				// Note: This TypedReference represents a reference to the variable sampleObj itself, not the object reference that sampleObj is.
				// TypedReference's internal value needs to be dereferenced to get the actual object reference.
				var typedRefBoxRef = __makeref(sampleBox);
				// Following boxPtr initialization is equivalent to:
				//TypedReference* typedRefBoxRefAddr = &typedRefBoxRef;
				//void* typedRefBoxRefIntPtrAddr = (byte*)typedRefBoxRefAddr + typedRefValueFieldOffset;
				//IntPtr typedRefBoxRefIntPtr = *(IntPtr*)typedRefBoxRefIntPtrAddr;
				//void* boxRefAddr = typedRefBoxRefIntPtr.ToPointer();
				//// Choice of pointer type is technically arbitrary, but IntPtr is the "safe" version of a pointer,
				//// which GCHandle.AddrOfPinnedObject() also returns.
				//IntPtr boxAddr = *(IntPtr*)boxRefAddr;
				var boxPtr = **(IntPtr**)((byte*)&typedRefBoxRef + typedRefValueFieldOffset);
				var boxDataPtr = sampleHandle.AddrOfPinnedObject();
				Logging.Log($"boxPtr: 0x{(long)boxPtr:x16}");
				Logging.Log($"boxDataPtr: 0x{(long)boxDataPtr:x16}");
				var objHeaderSize = (int)(boxDataPtr.ToInt64() - boxPtr.ToInt64());
				Logging.Log("objHeaderSize: " + objHeaderSize);
				var structPtr = (TestStruct*)((byte*)boxPtr + objHeaderSize);
				Console.Error.Flush();
				Logging.Log("structPtr->X: " + structPtr->X);
				Logging.Log("structPtr->Y: " + structPtr->Y);
			}
			finally
			{
				sampleHandle.Free();
			}
		}

		[Explicit]
		[Test]
		public unsafe void TypedReferenceTest()
		{
			Logging.Log("IntPtr.Size: " + IntPtr.Size);
			Logging.Log("sizeof(IntPtr): " + sizeof(IntPtr));
			Logging.Log("Marshal.SizeOf(typeof(RuntimeTypeHandle)): " + Marshal.SizeOf(typeof(RuntimeTypeHandle)));
			Logging.Log("sizeof(RuntimeTypeHandle): " + sizeof(RuntimeTypeHandle));
			// Following line crashes on Mono for some reason:
			//Logging.Log("Marshal.SizeOf(typeof(TypedReference)): " + Marshal.SizeOf(typeof(TypedReference)));
			Logging.Log("sizeof(TypedReference): " + sizeof(TypedReference));

			bool isMono = !(Type.GetType("Mono.Runtime") is null);
			// Mono.RuntimeClassHandle was only added in Mono 4.8, so can't be relied on.
			//Type typeof_MonoRuntimeClassHandle = null;
			//ConstructorInfo methodof_MonoRuntimeClassHandle_ctor = null;
			//MethodInfo methodof_MonoRuntimeClassHandle_GetTypeHandle = null;
			//if (isMono)
			//{
			//	typeof_MonoRuntimeClassHandle = Type.GetType("Mono.RuntimeClassHandle");
			//	Logging.Log(typeof_MonoRuntimeClassHandle);
			//	methodof_MonoRuntimeClassHandle_ctor = typeof_MonoRuntimeClassHandle.GetConstructor(AccessTools.all, null, new[] { typeof(IntPtr) }, null);
			//	Logging.Log(methodof_MonoRuntimeClassHandle_ctor);
			//	methodof_MonoRuntimeClassHandle_GetTypeHandle= typeof_MonoRuntimeClassHandle.GetMethod("GetTypeHandle", AccessTools.all);
			//	Logging.Log(methodof_MonoRuntimeClassHandle_GetTypeHandle);
			//}

			Logging.Log("typeof(TestStruct).TypeHandle.Value: " + typeof(TestStruct).TypeHandle.Value);

			byte* addr;

			var s = new TestStruct(123, 321);
			var tr0 = __makeref(s);
			addr = (byte*)&tr0;
			if (isMono)
			{
				Logging.Log("type(RuntimeTypeHandle): " + *(RuntimeTypeHandle*)addr);
				Logging.Log("type(Type): " + Type.GetTypeFromHandle(*(RuntimeTypeHandle*)addr));
				addr += sizeof(RuntimeTypeHandle);
				Logging.Log((IntPtr)addr);
			}
			var valuePtr = *(IntPtr*)addr;
			Logging.Log("Value/value: " + valuePtr);
			addr += sizeof(IntPtr);
			Logging.Log((IntPtr)addr);
			var klassPtr = *(IntPtr*)addr;
			Logging.Log("Type/klass: " + klassPtr);
			//if (isMono)
			//{
			//	var monoRuntimeClassHandle = methodof_MonoRuntimeClassHandle_ctor.Invoke(new object[] { klassPtr });
			//	Logging.Log("Type/klass(Mono.RuntimeHandle): " + monoRuntimeClassHandle);
			//	var runtimeTypeHandle = (RuntimeTypeHandle)methodof_MonoRuntimeClassHandle_GetTypeHandle.Invoke(monoRuntimeClassHandle, new object[0]);
			//	Logging.Log("Type/klass(Mono.RuntimeHandle => RuntimeTypeHandle): " + runtimeTypeHandle);
			//	Logging.Log("Type/klass(Mono.RuntimeHandle => RuntimeTypeHandle => Type): " + Type.GetTypeFromHandle(runtimeTypeHandle));
			//}
			Logging.Log("__reftype: " + __reftype(tr0));
			Logging.Log("__refvalue: " + __refvalue(tr0, TestStruct));

			//var os = (object)s;
			//var tr1 = __makeref(os);
			//Logging.Log("__reftype: " + __reftype(tr1));
			//Logging.Log("__refvalue: " + __refvalue(tr1, object));

			//TestClass1 nul = null;
			//var tr2 = __makeref(nul);
			//Logging.Log("__reftype: " + __reftype(tr2));
			//Logging.Log("__refvalue: " + __refvalue(tr2, TestClass1));

			var tr = new TypedReference();
			addr = (byte*)&tr;
			if (isMono)
			{
				*(RuntimeTypeHandle*)addr = typeof(TestStruct).TypeHandle;
				Logging.Log("type(RuntimeTypeHandle): " + *(RuntimeTypeHandle*)addr);
				addr += sizeof(RuntimeTypeHandle);
				Logging.Log((IntPtr)addr);
			}
			*(IntPtr*)addr = (IntPtr)(&s);
			Logging.Log("Value/value: " + *(IntPtr*)addr);
			addr += sizeof(IntPtr);
			Logging.Log((IntPtr)addr);
			if (isMono)
				*(IntPtr*)addr = klassPtr;
			else
				*(IntPtr*)addr = typeof(TestStruct).TypeHandle.Value;
			Logging.Log("Type/klass: " + *(IntPtr*)addr);
			Logging.Log("__reftype: " + __reftype(tr));
			Logging.Log("__refvalue: " + __refvalue(tr, TestStruct));
		}

		[Explicit]
		[Test]
		public unsafe void TypedReferenceTest2()
		{
			byte* addr;

			var c = new TestClass1();
			c.X = 1234;
			var tr0 = __makeref(c);
			addr = (byte*)&tr0;
			Logging.Log((IntPtr)addr);
			var valuePtr = *(IntPtr*)addr;
			Logging.Log("Value: " + valuePtr);
			addr += sizeof(IntPtr);
			Logging.Log((IntPtr)addr);
			var typePtr = *(IntPtr*)addr;
			Logging.Log("Type: " + typePtr);

			var tr = new TypedReference();
			addr = (byte*)&tr;
			Logging.Log((IntPtr)addr);
			*(IntPtr*)addr = valuePtr;
			Logging.Log("Value: " + *(IntPtr*)addr);
			addr += sizeof(IntPtr);
			Logging.Log((IntPtr)addr);
			*(IntPtr*)addr = typeof(TestClass1).TypeHandle.Value;
			Logging.Log("Type: " + *(IntPtr*)addr);
			Logging.Log("__reftype: " + __reftype(tr));
			Logging.Log("__refvalue: " + __refvalue(tr, TestClass1).X);
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

			static unsafe TestStaticClass1()
			{
				sampleField = new List<object>() { new Sample<string, int>("Foo") };
				Logging.Log(SampleToString<string>());
				Logging.Log(typeof(TestStaticClass2).ToDebugString());
				//TestStaticClass2.Bar(); // call (static)
				Action<int> action1 = TestStaticClass2.Bar; // ldftn
				action1(10);
				object testObj = NewTestClass(); // accessible call
				Action<int> action2 = ((TestClass1)testObj).Bar; // ldvirtfn
				action2(30);
				var c = (TestClass1)testObj; // castclass (valid type)
				var isinstSuccess = testObj as TestClass1; // isinst (valid type)
				Logging.Log(isinstSuccess is null, "isinstSuccess is null");
				var isinstFailure = testObj as string; // isinst (invalid type)
				Logging.Log(isinstFailure is null, "isinstFailure is null");
				try
				{
					Logging.Log((string)testObj); // castclass (invalid type)
					throw new InvalidOperationException("invalid cast NOT detected");
				}
				catch (InvalidCastException)
				{
					Logging.Log("invalid cast detected");
				}
				var carr = new TestClass1[2]; // newarr
				carr[0] = c; // stelem.ref
				c = carr[0]; // ldelem.ref
				//c.Foo(123, out var cfooy, new List<string>() { "lol" }); // call (object, out param)
				//Logging.Log(cfooy, "cfooy");
				//c.Bar(20); // callvirt (object)
				Logging.Log(c.X); // ldfld (object)
				try
				{
					Logging.Log(carr[1].X); // ldelem.ref, ldfld (null)
					throw new InvalidOperationException("NRE NOT detected");
				}
				catch (NullReferenceException)
				{
					Logging.Log("NRE detected");
				}
				try
				{
					carr[1].X = 100; // ldelem.ref, stfld (null)
					throw new InvalidOperationException("NRE NOT detected");
				}
				catch (NullReferenceException)
				{
					Logging.Log("NRE detected");
				}
				//var s0 = TestStaticClass2.Baz(40); // call
				//Logging.Log(s0.X, "s0.X"); // ldfld (value type)
				//Logging.Log(s0.Y, "s0.Y"); // ldfld (value type)
				var s1 = new TestStruct() // initobj
				{
					X = 1234, // stfld (value type)
					Y = 4321, // stfld (value type)
				};
				Logging.Log(s1.X, "s1.X"); // ldfld (value type)
				Logging.Log(s1.Y, "s1.Y"); // ldfld (value type)
				var sarr = new TestStruct[] { s1 }; // newarr, stelem
				s1 = sarr[0]; // ldelem
				Logging.Log(s1.X, "s1.X"); // ldfld (value type)
				Logging.Log(s1.Y, "s1.Y"); // ldfld (value type)
				var os = (object)s1; // box
				var s2 = (TestStruct)os; // unbox.any
				//Logging.Log(s2.Foo(), "s2"); // call (value type)
#pragma warning disable CS0183 // 'is' expression's given expression is always of the provided type
				Logging.Log(s1 is TestStruct, "s1 is TestStruct"); // compiles to true (ldc.i4.1), not isinst
#pragma warning restore CS0183 // 'is' expression's given expression is always of the provided type
#pragma warning disable CS0184 // 'is' expression's given expression is never of the provided type
				Logging.Log(s1 is TestClass1, "s1 is TestClass1"); // compiles to false (ldc.i4.0), not isinst
#pragma warning restore CS0184 // 'is' expression's given expression is never of the provided type
				Logging.Log(os is TestStruct, "os is TestStruct"); // isinst (object type is value type)
				//staticField = new TestClass1(); // accessible stsfld, newobj (object)
				staticField = (TestClass1)NewTestClass(); // castclass
				Logging.Log(staticField.X, "staticField.X"); // ldfld (object)
				staticField.X = 5; // stfld (object)
				Logging.Log(staticField.X, "staticField.X"); // ldfld (object)
				staticField.structField = new TestStruct() { X = 99, Y = 999 }; // stfld (object), initobj, stfld (value type)
				var s3 = staticField.structField; // ldfld (object)
				//Logging.Log(s3.ToString(), "s3"); // callvirt (value type)
				//Logging.Log(staticField.structField.X, "staticField.structField.X"); // ldflda
				//TestClass1.staticStructField = new TestStruct(11, 7); // stsfld, newobj (value type)
				//Logging.Log(TestClass1.staticStructField.X, "TestClass1.staticStructField.X");
				//Logging.Log(TestClass1.staticStructField.Y, "TestClass1.staticStructField.Y");
				var tr = __makeref(s3); // mkrefany
				Logging.Log(__reftype(tr), "__reftype(__makeref(s3))"); // refanytype (value type)
				s3 = __refvalue(tr, TestStruct); // refanyval (value type)
				Logging.Log((object)s3, "__refvalue(__makeref(s3))"); // box (just to avoid Logging.Log<TestStruct>)
				try
				{
					var tmp = __refvalue(tr, TestClass1); // refanyval (different type)
				}
				catch (InvalidCastException)
				{
					Logging.Log("__refvalue requires same type");
				}
				tr = __makeref(c);
				Logging.Log(__reftype(tr), "__reftype(__makeref(c))"); // refanytype (object)
				c = __refvalue(tr, TestClass1); // refanyval (object)
				Logging.Log(c.X, "__refvalue(__makeref(c)).X");
				try
				{
					var tmp = __refvalue(tr, TestInterface); // refanyval (different type)
				}
				catch (InvalidCastException)
				{
					Logging.Log("__refvalue requires same type");
				}
				Logging.Log(sizeof(TestStruct), "sizeof(TestStruct)");
				Logging.Log("END");
			}

			static string SampleToString<T>() => sampleField.Join(sample => ((Sample<string, int>)sample).Value, ", ");

			static readonly TestClass1 staticField;
		}

		static class TestStaticClass2
		{
			static TestStaticClass2()
			{
				Logging.Log("Bar");
			}

			public static void Bar(int x)
			{
				Logging.Log("TestStaticClass2.Bar: " + x);
			}

			public static TestStruct Baz(int x)
			{
				Logging.Log("TestStaticClass2.Baz: " + x);
				return new TestStruct() { X = x };
			}
		}

		public interface TestInterface
		{
			void Bar(int x);
		}

		public static TestInterface NewTestClass() => new TestClass2() { X = -1000 };

		class TestClass1 : TestInterface
		{
			public TestClass1()
			{
				X = -2;
			}

			protected internal void Foo(int x, out int y, List<string> slist)
			{
				Logging.Log($"TestClass1.Foo: {x}, {slist.Join()}");
				y = x;
			}

			public virtual void Bar(int x) => Logging.Log("TestClass1.Bar: " + x);

			public int X;

			internal TestStruct structField;

			internal static TestStruct staticStructField;
		}

		class TestClass2 : TestClass1
		{
			public override void Bar(int x) => Logging.Log("TestClass2.Bar: " + x);
		}

		internal struct TestStruct
		{
			public int X;
			public int Y;

			internal TestStruct(int x, int y)
			{
				X = x;
				Y = y;
			}

			internal string Foo() => $"TestStruct: {X}, {Y}";

			public override string ToString() => Foo();

			public new object MemberwiseClone()
			{
				Logging.Log("Just making sure this is never called");
				return null;
			}
		}
	}
}
