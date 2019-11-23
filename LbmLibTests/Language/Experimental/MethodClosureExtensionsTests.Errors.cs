﻿using System;
using System.Reflection;
using NUnit.Framework;

namespace LbmLib.Language.Experimental.Tests
{
	[TestFixture]
	public class MethodClosureExtensionsTestsErrors : MethodClosureExtensionsBase
	{
		[Test]
		public void Control_StaticMethod_Error()
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				var method = typeof(MethodClosureExtensionsTestsSimple).GetMethod(nameof(MethodClosureExtensionsTestsSimple.SimpleStaticNonVoidMethod));
				AssertStaticMethodErrors(fixture, method,
					typeof(Func<string, int, long, int, string>),
					typeof(Func<string, int, string, int, string>),
					new object[] { "hello world", 1, 2L, 3 },
					new object[] { "hello world", 1, 2L, "string" });
			}
		}

		[Test]
		public void PartialApply_StaticMethod_Error()
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				var method = typeof(MethodClosureExtensionsTestsSimple).GetMethod(nameof(MethodClosureExtensionsTestsSimple.SimpleStaticNonVoidMethod));
				var partialAppliedMethod = method.PartialApply("hello world", 1, 2L);
				AssertStaticMethodErrors(fixture, partialAppliedMethod,
					typeof(Func<int, string>),
					typeof(Func<string, int>),
					new object[] { 3 },
					new object[] { "string" });
			}
		}

		[Test]
		public void Multiple_PartialApply_StaticMethod_Error()
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				var method = typeof(MethodClosureExtensionsTestsSimple).GetMethod(nameof(MethodClosureExtensionsTestsSimple.SimpleStaticNonVoidMethod));
				var partialAppliedMethod = method.PartialApply("hello world");
				partialAppliedMethod = partialAppliedMethod.PartialApply(1);
				partialAppliedMethod = partialAppliedMethod.PartialApply(2L);
				partialAppliedMethod = partialAppliedMethod.PartialApply(3);
				AssertStaticMethodErrors(fixture, partialAppliedMethod,
					typeof(Func<string>),
					typeof(Action),
					new object[0],
					null);
			}
		}

		[Test]
		public void Control_StructInstanceMethod_Error()
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				var v = new TestStruct(15);
				var method = typeof(TestStruct).GetMethod(nameof(TestStruct.SimpleInstanceVoidMethod));
				AssertInstanceMethodErrors(fixture, v, method,
					typeof(Action<int, string[]>),
					typeof(Action<int, string>),
					new object[] { 5, new[] { "hello", "world" } },
					new object[] { 5, "string" });
			}
		}

		[Test]
		public void PartialApply_StructInstanceMethod_Error()
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				var v = new TestStruct(15);
				var method = typeof(TestStruct).GetMethod(nameof(TestStruct.SimpleInstanceVoidMethod));
				var partialAppliedMethod = method.PartialApply(5);
				AssertInstanceMethodErrors(fixture, v, partialAppliedMethod,
					typeof(Action<string[]>),
					typeof(Action<string>),
					new object[] { new[] { "hello", "world" } },
					new object[] { "string" });
			}
		}

		[Test]
		public void Multiple_PartialApply_StructInstanceMethod_Error()
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				var v = new TestStruct(15);
				var method = typeof(TestStruct).GetMethod(nameof(TestStruct.SimpleInstanceVoidMethod));
				var partialAppliedMethod = method.PartialApply(5);
				partialAppliedMethod = partialAppliedMethod.PartialApply(new object[] { new[] { "hello", "world" } });
				AssertInstanceMethodErrors(fixture, v, partialAppliedMethod,
					typeof(Action),
					typeof(Func<string>),
					new object[0],
					null);
			}
		}

		[Test]
		public void Control_ClassInstanceMethod_Error()
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				var c = new TestClassSimple(15);
				var method = typeof(TestClassSimple).GetMethod(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod));
				AssertInstanceMethodErrors(fixture, c, method,
					typeof(Action<int, string[]>),
					typeof(Action<int, string>),
					new object[] { 5, new[] { "hello", "world" } },
					new object[] { 5, "string" });
			}
		}

		[Test]
		public void PartialApply_ClassInstanceMethod_Error()
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				var c = new TestClassSimple(15);
				var method = typeof(TestClassSimple).GetMethod(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod));
				var partialAppliedMethod = method.PartialApply(5);
				AssertInstanceMethodErrors(fixture, c, partialAppliedMethod,
					typeof(Action<string[]>),
					typeof(Action<string>),
					new object[] { new[] { "hello", "world" } },
					new object[] { "string" });
			}
		}

		[Test]
		public void Multiple_PartialApply_ClassInstanceMethod_Error()
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				var c = new TestClassSimple(15);
				var method = typeof(TestClassSimple).GetMethod(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod));
				var partialAppliedMethod = method.PartialApply(5);
				partialAppliedMethod = partialAppliedMethod.PartialApply(new object[] { new[] { "hello", "world" } });
				AssertInstanceMethodErrors(fixture, c, partialAppliedMethod,
					typeof(Action),
					typeof(Func<string>),
					new object[0],
					null);
			}
		}

		[Test]
		public void Bind_StaticMethod_Error()
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				var method = typeof(MethodClosureExtensionsTestsSimple).GetMethod(nameof(MethodClosureExtensionsTestsSimple.SimpleStaticNonVoidMethod));
				// Static method cannot be bound.
				Assert.Throws(typeof(ArgumentException), () => method.Bind(this));
				// Bind(null) is never valid.
				Assert.Throws(typeof(ArgumentNullException), () => method.Bind(null));
			}
		}

		[Test]
		public void Bind_PartialApply_StaticMethod_Error()
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				var method = typeof(MethodClosureExtensionsTestsSimple).GetMethod(nameof(MethodClosureExtensionsTestsSimple.SimpleStaticNonVoidMethod));
				var partialAppliedMethod = method.PartialApply("hello world", 1, 2L);
				// Static method cannot be bound.
				Assert.Throws(typeof(ArgumentException), () => partialAppliedMethod.Bind(this));
				// Bind(null) is never valid.
				Assert.Throws(typeof(ArgumentNullException), () => partialAppliedMethod.Bind(null));
			}
		}

		[Test]
		public void Bind_StructInstanceMethod_Error()
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				var v = new TestStruct(15);
				var method = typeof(TestStruct).GetMethod(nameof(TestStruct.SimpleInstanceVoidMethod));
				// Instance method cannot be bound to invalid target.
				Assert.Throws(typeof(ArgumentException), () => method.Bind(this));
				// Bind(null) is never valid.
				Assert.Throws(typeof(ArgumentNullException), () => method.Bind(null));
				var boundMethod = method.Bind(v);
				AssertStaticMethodErrors(fixture, boundMethod,
					typeof(Action<int, string[]>),
					typeof(Action<int, string>),
					new object[] { 5, new[] { "hello", "world" } },
					new object[] { 5, "string" });
				// Bound method cannot be rebound.
				Assert.Throws(typeof(ArgumentException), () => boundMethod.Bind(v));
			}
		}

		[Test]
		public void Bind_PartialApply_StructInstanceMethod_Error()
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				var v = new TestStruct(15);
				var method = typeof(TestStruct).GetMethod(nameof(TestStruct.SimpleInstanceVoidMethod));
				var partialAppliedMethod = method.PartialApply(5);
				var boundMethod = partialAppliedMethod.Bind(v);
				AssertStaticMethodErrors(fixture, boundMethod,
					typeof(Action<string[]>),
					typeof(Action<string>),
					new object[] { new[] { "hello", "world" } },
					new object[] { "string" });
				// Bound partially applied method cannot be rebound.
				Assert.Throws(typeof(ArgumentException), () => boundMethod.Bind(v));
			}
		}

		[Test]
		public void PartialApply_Bind_StructInstanceMethod_Error()
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				var v = new TestStruct(15);
				var method = typeof(TestStruct).GetMethod(nameof(TestStruct.SimpleInstanceVoidMethod));
				var boundMethod = method.Bind(v);
				var partialAppliedMethod = boundMethod.PartialApply(5);
				AssertStaticMethodErrors(fixture, partialAppliedMethod,
					typeof(Action<string[]>),
					typeof(Action<string>),
					new object[] { new[] { "hello", "world" } },
					new object[] { "string" });
				// Partially applied bound method cannot be rebound.
				Assert.Throws(typeof(ArgumentException), () => partialAppliedMethod.Bind(v));
			}
		}

		[Test]
		public void Bind_ClassInstanceMethod_Error()
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				var c = new TestClassSimple(15);
				var method = typeof(TestClassSimple).GetMethod(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod));
				// Instance method cannot be bound to invalid target.
				Assert.Throws(typeof(ArgumentException), () => method.Bind(this));
				// Bind(null) is never valid.
				Assert.Throws(typeof(ArgumentNullException), () => method.Bind(null));
				var boundMethod = method.Bind(c);
				AssertStaticMethodErrors(fixture, boundMethod,
					typeof(Action<int, string[]>),
					typeof(Action<int, string>),
					new object[] { 5, new[] { "hello", "world" } },
					new object[] { 5, "string" });
			}
		}

		[Test]
		public void Bind_PartialApply_ClassInstanceMethod_Error()
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				var c = new TestClassSimple(15);
				var method = typeof(TestClassSimple).GetMethod(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod));
				var partialAppliedMethod = method.PartialApply(5);
				var boundMethod = partialAppliedMethod.Bind(c);
				AssertStaticMethodErrors(fixture, boundMethod,
					typeof(Action<string[]>),
					typeof(Action<string>),
					new object[] { new[] { "hello", "world" } },
					new object[] { "string" });
				// Bound partially applied method cannot be rebound.
				Assert.Throws(typeof(ArgumentException), () => boundMethod.Bind(c));
			}
		}

		[Test]
		public void PartialApply_Bind_ClassInstanceMethod_Error()
		{
			using (var fixture = new MethodClosureExtensionsFixture())
			{
				var c = new TestClassSimple(15);
				var method = typeof(TestClassSimple).GetMethod(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod));
				var boundMethod = method.Bind(c);
				var partialAppliedMethod = boundMethod.PartialApply(5);
				AssertStaticMethodErrors(fixture, partialAppliedMethod,
					typeof(Action<string[]>),
					typeof(Action<string>),
					new object[] { new[] { "hello", "world" } },
					new object[] { "string" });
				// Partially applied bound method cannot be rebound.
				Assert.Throws(typeof(ArgumentException), () => partialAppliedMethod.Bind(c));
			}
		}

		void AssertStaticMethodErrors(MethodClosureExtensionsFixture fixture, MethodInfo method,
			Type validDelegateType, Type invalidDelegateType,
			object[] validSampleArgs, object[] invalidSampleArgs)
		{
			using (fixture.DebugOnlyFilter())
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
			// Note: On Mono runtime, TargetParameterCountException will be thrown by MethodInfo.CreateDelegate.
			// In all other cases (MethodInfo.CreateDelegate on MS .NET runtime, ClosureMethod.CreateDelegate), ArgumentException will be thrown.
			AssertThrowsOneOfTwoExceptions<ArgumentException, TargetParameterCountException>(() => method.CreateDelegate(validDelegateType, this));
			// CreateDelegate cannot be invoked with an invalid delegate type.
			// Note: On Mono runtime, if Action/Func and has wrong # of type parameters, TargetParameterCountException is thrown instead
			// so just ensure either the Action is passed for Func (or vice versa), or the same # of type parameters are passed,
			// just with wrong type parameter(s).
			Assert.Throws(typeof(ArgumentException), () => method.CreateDelegate(invalidDelegateType));
		}

		void AssertInstanceMethodErrors(MethodClosureExtensionsFixture fixture, object instance, MethodInfo method,
			Type validDelegateType, Type invalidDelegateType,
			object[] validSampleArgs, object[] invalidSampleArgs)
		{
			using (fixture.DebugOnlyFilter())
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
			// Note: On Mono runtime, if Action/Func and has wrong # of type parameters, TargetParameterCountException is thrown instead
			// so just ensure either the Action is passed for Func (or vice versa), or the same # of type parameters are passed,
			// just with wrong type parameter(s).
			Assert.Throws(typeof(ArgumentException), () => method.CreateDelegate(invalidDelegateType, instance));
			// CreateDelegate(delegateType, null) doesn't throw error, but invocation of the resulting delegate results is invalid.
			// Note: On Mono runtime, TargetException will be thrown by MethodInfo.CreateDelegate.
			// In all other cases (MethodInfo.CreateDelegate on MS .NET runtime, ClosureMethod.CreateDelegate), TargetInvocationException will be thrown.
			var nullBoundDelegate = method.CreateDelegate(validDelegateType, null);
			AssertThrowsOneOfTwoExceptions<TargetInvocationException, TargetException>(() => nullBoundDelegate.DynamicInvoke(validSampleArgs));
		}

		static void AssertThrowsOneOfTwoExceptions<E1, E2>(TestDelegate testDelegate) where E1 : Exception where E2 : Exception
		{
			try
			{
				testDelegate();
				return;
			}
			catch (Exception ex)
			{
				if (ex is E1 || ex is E2)
					return;
				Assert.Throws(typeof(ArgumentException), testDelegate);
			}
			Assert.Throws(typeof(ArgumentException), testDelegate);
		}
	}
}
