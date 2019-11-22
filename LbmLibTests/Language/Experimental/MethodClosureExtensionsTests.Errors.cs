using System;
using System.Reflection;
using NUnit.Framework;

namespace LbmLib.Language.Experimental.Tests
{
	[TestFixture]
	public class MethodClosureExtensionsTestsErrors
	{
		[OneTimeSetUp]
		public static void SetUpOnce()
		{
			Logging.DefaultLogger = Logging.ConsoleLogger;
		}

		[Test]
		public void Control_StaticMethod_Error()
		{
			var method = typeof(MethodClosureExtensionsTestsSimple).GetMethod(nameof(MethodClosureExtensionsTestsSimple.SimpleStaticNonVoidMethod));
			AssertStaticMethodErrors(method,
				typeof(Func<string, int, long, int, string>),
				typeof(Func<string, int>),
				new object[] { "hello world", 1, 2L, 3 },
				new object[] { "hello world", 1, 2L, "string" });
		}

		[Test]
		public void PartialApply_StaticMethod_Error()
		{
			var method = typeof(MethodClosureExtensionsTestsSimple).GetMethod(nameof(MethodClosureExtensionsTestsSimple.SimpleStaticNonVoidMethod));
			var partialAppliedMethod = method.PartialApply("hello world", 1, 2L);
			AssertStaticMethodErrors(partialAppliedMethod,
				typeof(Func<int, string>),
				typeof(Func<string, int>),
				new object[] { 3 },
				new object[] { "string" });
		}

		[Test]
		public void Multiple_PartialApply_StaticMethod_Error()
		{
			var method = typeof(MethodClosureExtensionsTestsSimple).GetMethod(nameof(MethodClosureExtensionsTestsSimple.SimpleStaticNonVoidMethod));
			var partialAppliedMethod = method.PartialApply("hello world");
			partialAppliedMethod = partialAppliedMethod.PartialApply(1);
			partialAppliedMethod = partialAppliedMethod.PartialApply(2L);
			partialAppliedMethod = partialAppliedMethod.PartialApply(3);
			AssertStaticMethodErrors(partialAppliedMethod,
				typeof(Func<string>),
				typeof(Action),
				new object[0],
				null);
		}

		[Test]
		public void Control_StructInstanceMethod_Error()
		{
			var v = new TestStruct(15);
			var method = typeof(TestStruct).GetMethod(nameof(TestStruct.SimpleInstanceVoidMethod));
			AssertInstanceMethodErrors(v, method,
				typeof(Action<int, string[]>),
				typeof(Action<int, string, string>),
				new object[] { 5, new[] { "hello", "world" } },
				new object[] { 5, "string" });
		}

		[Test]
		public void PartialApply_StructInstanceMethod_Error()
		{
			var v = new TestStruct(15);
			var method = typeof(TestStruct).GetMethod(nameof(TestStruct.SimpleInstanceVoidMethod));
			var partialAppliedMethod = method.PartialApply(5);
			AssertInstanceMethodErrors(v, partialAppliedMethod,
				typeof(Action<string[]>),
				typeof(Action<string, string>),
				new object[] { new[] { "hello", "world" } },
				new object[] { "string" });
		}

		[Test]
		public void Multiple_PartialApply_StructInstanceMethod_Error()
		{
			var v = new TestStruct(15);
			var method = typeof(TestStruct).GetMethod(nameof(TestStruct.SimpleInstanceVoidMethod));
			var partialAppliedMethod = method.PartialApply(5);
			partialAppliedMethod = partialAppliedMethod.PartialApply(new object[] { new[] { "hello", "world" } });
			AssertInstanceMethodErrors(v, partialAppliedMethod,
				typeof(Action),
				typeof(Func<string>),
				new object[0],
				null);
		}

		[Test]
		public void Control_ClassInstanceMethod_Error()
		{
			var c = new TestClassSimple(15);
			var method = typeof(TestClassSimple).GetMethod(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod));
			AssertInstanceMethodErrors(c, method,
				typeof(Action<int, string[]>),
				typeof(Action<int, string, string>),
				new object[] { 5, new[] { "hello", "world" } },
				new object[] { 5, "string" });
		}

		[Test]
		public void PartialApply_ClassInstanceMethod_Error()
		{
			var c = new TestClassSimple(15);
			var method = typeof(TestClassSimple).GetMethod(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod));
			var partialAppliedMethod = method.PartialApply(5);
			AssertInstanceMethodErrors(c, partialAppliedMethod,
				typeof(Action<string[]>),
				typeof(Action<string, string>),
				new object[] { new[] { "hello", "world" } },
				new object[] { "string" });
		}

		[Test]
		public void Multiple_PartialApply_ClassInstanceMethod_Error()
		{
			var c = new TestClassSimple(15);
			var method = typeof(TestClassSimple).GetMethod(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod));
			var partialAppliedMethod = method.PartialApply(5);
			partialAppliedMethod = partialAppliedMethod.PartialApply(new object[] { new[] { "hello", "world" } });
			AssertInstanceMethodErrors(c, partialAppliedMethod,
				typeof(Action),
				typeof(Func<string>),
				new object[0],
				null);
		}

		[Test]
		public void Bind_StaticMethod_Error()
		{
			var method = typeof(MethodClosureExtensionsTestsSimple).GetMethod(nameof(MethodClosureExtensionsTestsSimple.SimpleStaticNonVoidMethod));
			// Static method cannot be bound.
			Assert.Throws(typeof(ArgumentException), () => method.Bind(this));
			// Bind(null) is never valid.
			Assert.Throws(typeof(ArgumentNullException), () => method.Bind(null));
		}

		[Test]
		public void Bind_PartialApply_StaticMethod_Error()
		{
			var method = typeof(MethodClosureExtensionsTestsSimple).GetMethod(nameof(MethodClosureExtensionsTestsSimple.SimpleStaticNonVoidMethod));
			var partialAppliedMethod = method.PartialApply("hello world", 1, 2L);
			// Static method cannot be bound.
			Assert.Throws(typeof(ArgumentException), () => partialAppliedMethod.Bind(this));
			// Bind(null) is never valid.
			Assert.Throws(typeof(ArgumentNullException), () => partialAppliedMethod.Bind(null));
		}

		[Test]
		public void Bind_StructInstanceMethod_Error()
		{
			var v = new TestStruct(15);
			var method = typeof(TestStruct).GetMethod(nameof(TestStruct.SimpleInstanceVoidMethod));
			// Instance method cannot be bound to invalid target.
			Assert.Throws(typeof(ArgumentException), () => method.Bind(this));
			// Bind(null) is never valid.
			Assert.Throws(typeof(ArgumentNullException), () => method.Bind(null));
			var boundMethod = method.Bind(v);
			AssertStaticMethodErrors(boundMethod,
				typeof(Action<int, string[]>),
				typeof(Action<int, string, string>),
				new object[] { 5, new[] { "hello", "world" } },
				new object[] { 5, "string" });
			// Bound method cannot be rebound.
			Assert.Throws(typeof(ArgumentException), () => boundMethod.Bind(v));
		}

		[Test]
		public void Bind_PartialApply_StructInstanceMethod_Error()
		{
			var v = new TestStruct(15);
			var method = typeof(TestStruct).GetMethod(nameof(TestStruct.SimpleInstanceVoidMethod));
			var partialAppliedMethod = method.PartialApply(5);
			var boundMethod = partialAppliedMethod.Bind(v);
			AssertStaticMethodErrors(boundMethod,
				typeof(Action<string[]>),
				typeof(Action<string, string>),
				new object[] { new[] { "hello", "world" } },
				new object[] { "string" });
			// Bound partially applied method cannot be rebound.
			Assert.Throws(typeof(ArgumentException), () => boundMethod.Bind(v));
		}

		[Test]
		public void PartialApply_Bind_StructInstanceMethod_Error()
		{
			var v = new TestStruct(15);
			var method = typeof(TestStruct).GetMethod(nameof(TestStruct.SimpleInstanceVoidMethod));
			var boundMethod = method.Bind(v);
			var partialAppliedMethod = boundMethod.PartialApply(5);
			AssertStaticMethodErrors(partialAppliedMethod,
				typeof(Action<string[]>),
				typeof(Action<string, string>),
				new object[] { new[] { "hello", "world" } },
				new object[] { "string" });
			// Partially applied bound method cannot be rebound.
			Assert.Throws(typeof(ArgumentException), () => partialAppliedMethod.Bind(v));
		}

		[Test]
		public void Bind_ClassInstanceMethod_Error()
		{
			var c = new TestClassSimple(15);
			var method = typeof(TestClassSimple).GetMethod(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod));
			// Instance method cannot be bound to invalid target.
			Assert.Throws(typeof(ArgumentException), () => method.Bind(this));
			// Bind(null) is never valid.
			Assert.Throws(typeof(ArgumentNullException), () => method.Bind(null));
			var boundMethod = method.Bind(c);
			AssertStaticMethodErrors(boundMethod,
				typeof(Action<int, string[]>),
				typeof(Action<int, string, string>),
				new object[] { 5, new[] { "hello", "world" } },
				new object[] { 5, "string" });
		}

		[Test]
		public void Bind_PartialApply_ClassInstanceMethod_Error()
		{
			var c = new TestClassSimple(15);
			var method = typeof(TestClassSimple).GetMethod(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod));
			var partialAppliedMethod = method.PartialApply(5);
			var boundMethod = partialAppliedMethod.Bind(c);
			AssertStaticMethodErrors(boundMethod,
				typeof(Action<string[]>),
				typeof(Action<string, string>),
				new object[] { new[] { "hello", "world" } },
				new object[] { "string" });
			// Bound partially applied method cannot be rebound.
			Assert.Throws(typeof(ArgumentException), () => boundMethod.Bind(c));
		}

		[Test]
		public void PartialApply_Bind_ClassInstanceMethod_Error()
		{
			var c = new TestClassSimple(15);
			var method = typeof(TestClassSimple).GetMethod(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod));
			var boundMethod = method.Bind(c);
			var partialAppliedMethod = boundMethod.PartialApply(5);
			AssertStaticMethodErrors(partialAppliedMethod,
				typeof(Action<string[]>),
				typeof(Action<string, string>),
				new object[] { new[] { "hello", "world" } },
				new object[] { "string" });
			// Partially applied bound method cannot be rebound.
			Assert.Throws(typeof(ArgumentException), () => partialAppliedMethod.Bind(c));
		}

		void AssertStaticMethodErrors(MethodInfo method, Type validDelegateType, Type invalidDelegateType, object[] validSampleArgs, object[] invalidSampleArgs)
		{
			using (Logging.With(log => { }))
			{
				// Verify that validDelegateType and validSampleArgs are, well, valid.
				method.Invoke(null, validSampleArgs);
				method.CreateDelegate(validDelegateType).DynamicInvoke(validSampleArgs);
				// Invocation target is ignored for static methods.
				method.Invoke(this, validSampleArgs);
				// null is valid target for static method CreateDelegate.
				method.CreateDelegate(validDelegateType, null).DynamicInvoke(validSampleArgs);
			}
			if (validSampleArgs.Length > 0)
			{
				// Method cannot be invoked with too few parameters.
				Assert.Throws(typeof(TargetParameterCountException), () => method.Invoke(null, new object[0]));
			}
			// Method cannot be invoked with too many parameters.
			Assert.Throws(typeof(TargetParameterCountException), () => method.Invoke(null, validSampleArgs.Append("extra")));
			if (!(invalidSampleArgs is null))
			{
				// Method cannot be invoked with invalid parameter type.
				Assert.Throws(typeof(ArgumentException), () => method.Invoke(null, invalidSampleArgs));
			}
			// Static method CreateDelegate cannot be invoked with a non-null target.
			Assert.Throws(typeof(ArgumentException), () => method.CreateDelegate(validDelegateType, this));
			// CreateDelegate cannot be invoked with an invalid delegate type.
			Assert.Throws(typeof(ArgumentException), () => method.CreateDelegate(invalidDelegateType));
		}

		void AssertInstanceMethodErrors(object instance, MethodInfo method, Type validDelegateType, Type invalidDelegateType, object[] validSampleArgs, object[] invalidSampleArgs)
		{
			using (Logging.With(log => { }))
			{
				// Verify that validDelegateType and validSampleArgs are, well, valid.
				method.Invoke(instance, validSampleArgs);
				method.CreateDelegate(validDelegateType, instance).DynamicInvoke(validSampleArgs);
			}
			// Instance method cannot be invoked without a target.
			Assert.Throws(typeof(TargetException), () => method.Invoke(null, validSampleArgs));
			// Instance method cannot be invoked with an invalid target.
			Assert.Throws(typeof(TargetException), () => method.Invoke(this, validSampleArgs));
			if (validSampleArgs.Length > 0)
			{
				// Method cannot be invoked with too few parameters.
				Assert.Throws(typeof(TargetParameterCountException), () => method.Invoke(instance, new object[0]));
			}
			// Method cannot be invoked with too many parameters.
			Assert.Throws(typeof(TargetParameterCountException), () => method.Invoke(instance, validSampleArgs.Append("extra")));
			if (!(invalidSampleArgs is null))
			{
				// Method cannot be invoked with invalid parameter type.
				Assert.Throws(typeof(ArgumentException), () => method.Invoke(instance, invalidSampleArgs));
			}
			// Instance method CreateDelegate cannot be invoked without a target.
			Assert.Throws(typeof(ArgumentException), () => method.CreateDelegate(validDelegateType));
			// Instance method CreateDelegate cannot be invoked with an invalid target.
			Assert.Throws(typeof(ArgumentException), () => method.CreateDelegate(validDelegateType, this));
			// CreateDelegate cannot be invoked with an invalid delegate type.
			Assert.Throws(typeof(ArgumentException), () => method.CreateDelegate(invalidDelegateType, instance));
			// CreateDelegate(delegateType, null) doesn't throw error, but invocation of the resulting delegate results is invalid.
			var nullBoundDelegate = method.CreateDelegate(validDelegateType, null);
			Assert.Throws(typeof(TargetInvocationException), () => nullBoundDelegate.DynamicInvoke(validSampleArgs));
		}
	}
}
