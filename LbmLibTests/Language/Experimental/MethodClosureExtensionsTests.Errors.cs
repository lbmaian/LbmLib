using System;
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
			MethodClosureExtensionsFixture.Do(fixture =>
			{
				var method = typeof(MethodClosureExtensionsTestsSimple).GetMethod(nameof(MethodClosureExtensionsTestsSimple.SimpleStaticNonVoidMethod));
				AssertStaticMethodErrors(fixture, method,
					validDelegateType: typeof(Func<string, int, long, int, string>),
					invalidDelegateType: typeof(Func<string, int, string, int, string>),
					validSampleArgs: new object[] { "hello world", 1, 2L, 3 },
					invalidSampleArgs: new object[] { "hello world", 1, 2L, "string" },
					invokeIsAlwaysTargetException: false,
					delegateDynamicInvokeIsAlwaysException: false);
			});
		}

		[Test]
		public void PartialApply_StaticMethod_Error()
		{
			MethodClosureExtensionsFixture.Do(fixture =>
			{
				var method = typeof(MethodClosureExtensionsTestsSimple).GetMethod(nameof(MethodClosureExtensionsTestsSimple.SimpleStaticNonVoidMethod));
				var partialAppliedMethod = method.PartialApply("hello world", 1, 2L);
				AssertStaticMethodErrors(fixture, partialAppliedMethod,
					validDelegateType: typeof(Func<int, string>),
					invalidDelegateType: typeof(Func<string, int>),
					validSampleArgs: new object[] { 3 },
					invalidSampleArgs: new object[] { "string" },
					invokeIsAlwaysTargetException: false,
					delegateDynamicInvokeIsAlwaysException: false);
			});
		}

		[Test]
		public void Multiple_PartialApply_StaticMethod_Error()
		{
			MethodClosureExtensionsFixture.Do(fixture =>
			{
				var method = typeof(MethodClosureExtensionsTestsSimple).GetMethod(nameof(MethodClosureExtensionsTestsSimple.SimpleStaticNonVoidMethod));
				var partialAppliedMethod = method.PartialApply("hello world");
				partialAppliedMethod = partialAppliedMethod.PartialApply(1);
				partialAppliedMethod = partialAppliedMethod.PartialApply(2L);
				partialAppliedMethod = partialAppliedMethod.PartialApply(3);
				AssertStaticMethodErrors(fixture, partialAppliedMethod,
					validDelegateType: typeof(Func<string>),
					invalidDelegateType: typeof(Action),
					validSampleArgs: new object[0],
					invalidSampleArgs: null,
					invokeIsAlwaysTargetException: false,
					delegateDynamicInvokeIsAlwaysException: false);
			});
		}

		[TestCase(nameof(TestStruct.SimpleInstanceVoidMethod),
			typeof(Action<int, string[]>), typeof(Action<int, string>), true)]
		[TestCase(nameof(TestStruct.SimpleInstanceNoThisUsageMethod),
			typeof(Func<int, string[], int>), typeof(Func<int, string, int>), false)]
		public void Control_StructInstanceMethod_Error(string methodName,
			Type validDelegateType, Type invalidDelegateType, bool nullTargetDelegateIsException)
		{
			MethodClosureExtensionsFixture.Do(fixture =>
			{
				var v = new TestStruct(15);
				var method = typeof(TestStruct).GetMethod(methodName);
				AssertInstanceMethodErrors(fixture, v, method,
					validDelegateType,
					invalidDelegateType,
					validSampleArgs: new object[] { 5, new[] { "hello", "world" } },
					invalidSampleArgs: new object[] { 5, "string" },
					nullTargetDelegateIsException);
			});
		}

		[TestCase(nameof(TestStruct.SimpleInstanceVoidMethod),
			typeof(Action<string[]>), typeof(Action<string>), true)]
		[TestCase(nameof(TestStruct.SimpleInstanceNoThisUsageMethod),
			typeof(Func<string[], int>), typeof(Func<string, int>), false)]
		public void PartialApply_StructInstanceMethod_Error(string methodName,
			Type validDelegateType, Type invalidDelegateType, bool nullTargetDelegateIsException)
		{
			MethodClosureExtensionsFixture.Do(fixture =>
			{
				var v = new TestStruct(15);
				var method = typeof(TestStruct).GetMethod(methodName);
				var partialAppliedMethod = method.PartialApply(5);
				AssertInstanceMethodErrors(fixture, v, partialAppliedMethod,
					validDelegateType,
					invalidDelegateType,
					validSampleArgs: new object[] { new[] { "hello", "world" } },
					invalidSampleArgs: new object[] { "string" },
					nullTargetDelegateIsException);
			});
		}

		[TestCase(nameof(TestStruct.SimpleInstanceVoidMethod),
			typeof(Action), typeof(Func<string>), true)]
		[TestCase(nameof(TestStruct.SimpleInstanceNoThisUsageMethod),
			typeof(Func<int>), typeof(Func<string>), false)]
		public void Multiple_PartialApply_StructInstanceMethod_Error(string methodName,
			Type validDelegateType, Type invalidDelegateType, bool nullTargetDelegateIsException)
		{
			MethodClosureExtensionsFixture.Do(fixture =>
			{
				var v = new TestStruct(15);
				var method = typeof(TestStruct).GetMethod(methodName);
				var partialAppliedMethod = method.PartialApply(5);
				partialAppliedMethod = partialAppliedMethod.PartialApply(new object[] { new[] { "hello", "world" } });
				AssertInstanceMethodErrors(fixture, v, partialAppliedMethod,
					validDelegateType,
					invalidDelegateType,
					validSampleArgs: new object[0],
					invalidSampleArgs: null,
					nullTargetDelegateIsException);
			});
		}

		[TestCase(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod),
			typeof(Action<int, string[]>), typeof(Action<int, string>), true)]
		[TestCase(nameof(TestClassSimple.SimpleInstanceNoThisUsageMethod),
			typeof(Func<int, string[], int>), typeof(Action<int, string[]>), false)]
		[TestCase(nameof(TestClassSimple.SimpleInstanceSafeThisUsageMethod),
			typeof(Func<int, string[], TestClassSimple>), typeof(Func<int, string[], int>), false)]
		public void Control_ClassInstanceMethod_Error(string methodName,
			Type validDelegateType, Type invalidDelegateType, bool nullTargetDelegateIsException)
		{
			MethodClosureExtensionsFixture.Do(fixture =>
			{
				var c = new TestClassSimple(15);
				var method = typeof(TestClassSimple).GetMethod(methodName);
				AssertInstanceMethodErrors(fixture, c, method,
					validDelegateType,
					invalidDelegateType,
					validSampleArgs: new object[] { 5, new[] { "hello", "world" } },
					invalidSampleArgs: new object[] { 5, "string" },
					nullTargetDelegateIsException);
			});
		}

		[TestCase(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod),
			typeof(Action<string[]>), typeof(Action<string>), true)]
		[TestCase(nameof(TestClassSimple.SimpleInstanceNoThisUsageMethod),
			typeof(Func<string[], int>), typeof(Action<string[], int>), false)]
		[TestCase(nameof(TestClassSimple.SimpleInstanceSafeThisUsageMethod),
			typeof(Func<string[], TestClassSimple>), typeof(Func<string[], int>), false)]
		public void PartialApply_ClassInstanceMethod_Error(string methodName,
			Type validDelegateType, Type invalidDelegateType, bool nullTargetDelegateIsException)
		{
			MethodClosureExtensionsFixture.Do(fixture =>
			{
				var c = new TestClassSimple(15);
				var method = typeof(TestClassSimple).GetMethod(methodName);
				var partialAppliedMethod = method.PartialApply(5);
				AssertInstanceMethodErrors(fixture, c, partialAppliedMethod,
					validDelegateType,
					invalidDelegateType,
					validSampleArgs: new object[] { new[] { "hello", "world" } },
					invalidSampleArgs: new object[] { "string" },
					nullTargetDelegateIsException);
			});
		}

		[TestCase(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod),
			typeof(Action), typeof(Func<string>), true)]
		[TestCase(nameof(TestClassSimple.SimpleInstanceNoThisUsageMethod),
			typeof(Func<int>), typeof(Func<string>), false)]
		[TestCase(nameof(TestClassSimple.SimpleInstanceSafeThisUsageMethod),
			typeof(Func<TestClassSimple>), typeof(Func<string>), false)]
		public void Multiple_PartialApply_ClassInstanceMethod_Error(string methodName,
			Type validDelegateType, Type invalidDelegateType, bool nullTargetDelegateIsException)
		{
			MethodClosureExtensionsFixture.Do(fixture =>
			{
				var c = new TestClassSimple(15);
				var method = typeof(TestClassSimple).GetMethod(methodName);
				var partialAppliedMethod = method.PartialApply(5);
				partialAppliedMethod = partialAppliedMethod.PartialApply(new object[] { new[] { "hello", "world" } });
				AssertInstanceMethodErrors(fixture, c, partialAppliedMethod,
					validDelegateType,
					invalidDelegateType,
					validSampleArgs: new object[0],
					invalidSampleArgs: null,
					nullTargetDelegateIsException);
			});
		}

		[Test]
		public void Bind_StaticMethod_Error()
		{
			MethodClosureExtensionsFixture.Do(fixture =>
			{
				var method = typeof(MethodClosureExtensionsTestsSimple).GetMethod(nameof(MethodClosureExtensionsTestsSimple.SimpleStaticNonVoidMethod));
				// Static method cannot be bound.
				Assert.Throws(typeof(ArgumentException), () => method.Bind(this));
			});
		}

		[Test]
		public void Bind_PartialApply_StaticMethod_Error()
		{
			MethodClosureExtensionsFixture.Do(fixture =>
			{
				var method = typeof(MethodClosureExtensionsTestsSimple).GetMethod(nameof(MethodClosureExtensionsTestsSimple.SimpleStaticNonVoidMethod));
				var partialAppliedMethod = method.PartialApply("hello world", 1, 2L);
				// Static method cannot be bound.
				Assert.Throws(typeof(ArgumentException), () => partialAppliedMethod.Bind(this));
			});
		}

		[TestCase(nameof(TestStruct.SimpleInstanceVoidMethod), false,
			typeof(Action<int, string[]>), typeof(Action<int, string>), false, false)]
		[TestCase(nameof(TestStruct.SimpleInstanceNoThisUsageMethod), false,
			typeof(Func<int, string[], int>), typeof(Func<int, string, int>), false, false)]
		[TestCase(nameof(TestStruct.SimpleInstanceVoidMethod), true,
			typeof(Action<int, string[]>), typeof(Action<int, string>), true, true)]
		[TestCase(nameof(TestStruct.SimpleInstanceNoThisUsageMethod), true,
			typeof(Func<int, string[], int>), typeof(Func<int, string, int>), true, false)]
		public void Bind_StructInstanceMethod_Error(string methodName, bool bindNull,
			Type validDelegateType, Type invalidDelegateType, bool invokeIsAlwaysTargetException, bool delegateDynamicInvokeIsAlwaysException)
		{
			MethodClosureExtensionsFixture.Do(fixture =>
			{
				var v = new TestStruct(15);
				var method = typeof(TestStruct).GetMethod(methodName);
				// Instance method cannot be bound to invalid target.
				Assert.Throws(typeof(ArgumentException), () => method.Bind(this));
				var boundMethod = bindNull ? method.Bind(null) : method.Bind(v);
				AssertStaticMethodErrors(fixture, boundMethod,
					validDelegateType,
					invalidDelegateType,
					validSampleArgs: new object[] { 5, new[] { "hello", "world" } },
					invalidSampleArgs: new object[] { 5, "string" },
					invokeIsAlwaysTargetException,
					delegateDynamicInvokeIsAlwaysException);
				// Bound method cannot be rebound.
				Assert.Throws(typeof(ArgumentException), () => boundMethod.Bind(v));
			});
		}

		[TestCase(nameof(TestStruct.SimpleInstanceVoidMethod), false,
			typeof(Action<string[]>), typeof(Action<string>), false, false)]
		[TestCase(nameof(TestStruct.SimpleInstanceNoThisUsageMethod), false,
			typeof(Func<string[], int>), typeof(Func<string, int>), false, false)]
		[TestCase(nameof(TestStruct.SimpleInstanceVoidMethod), true,
			typeof(Action<string[]>), typeof(Action<string>), true, true)]
		[TestCase(nameof(TestStruct.SimpleInstanceNoThisUsageMethod), true,
			typeof(Func<string[], int>), typeof(Func<string, int>), true, false)]
		public void Bind_PartialApply_StructInstanceMethod_Error(string methodName, bool bindNull,
			Type validDelegateType, Type invalidDelegateType, bool invokeIsAlwaysTargetException, bool delegateDynamicInvokeIsAlwaysException)
		{
			MethodClosureExtensionsFixture.Do(fixture =>
			{
				var v = new TestStruct(15);
				var method = typeof(TestStruct).GetMethod(methodName);
				var partialAppliedMethod = method.PartialApply(5);
				var boundMethod = bindNull ? partialAppliedMethod.Bind(null) : partialAppliedMethod.Bind(v);
				AssertStaticMethodErrors(fixture, boundMethod,
					validDelegateType,
					invalidDelegateType,
					validSampleArgs: new object[] { new[] { "hello", "world" } },
					invalidSampleArgs: new object[] { "string" },
					invokeIsAlwaysTargetException,
					delegateDynamicInvokeIsAlwaysException);
				// Bound partially applied method cannot be rebound.
				Assert.Throws(typeof(ArgumentException), () => boundMethod.Bind(v));
			});
		}

		[TestCase(nameof(TestStruct.SimpleInstanceVoidMethod), false,
			typeof(Action<string[]>), typeof(Action<string>), false, false)]
		[TestCase(nameof(TestStruct.SimpleInstanceNoThisUsageMethod), false,
			typeof(Func<string[], int>), typeof(Func<string, int>), false, false)]
		[TestCase(nameof(TestStruct.SimpleInstanceVoidMethod), true,
			typeof(Action<string[]>), typeof(Action<string>), true, true)]
		[TestCase(nameof(TestStruct.SimpleInstanceNoThisUsageMethod), true,
			typeof(Func<string[], int>), typeof(Func<string, int>), true, false)]
		public void PartialApply_Bind_StructInstanceMethod_Error(string methodName, bool bindNull,
			Type validDelegateType, Type invalidDelegateType, bool invokeIsAlwaysTargetException, bool delegateDynamicInvokeIsAlwaysException)
		{
			MethodClosureExtensionsFixture.Do(fixture =>
			{
				var v = new TestStruct(15);
				var method = typeof(TestStruct).GetMethod(methodName);
				var boundMethod = bindNull ? method.Bind(null) : method.Bind(v);
				var partialAppliedMethod = boundMethod.PartialApply(5);
				AssertStaticMethodErrors(fixture, partialAppliedMethod,
					validDelegateType,
					invalidDelegateType,
					validSampleArgs: new object[] { new[] { "hello", "world" } },
					invalidSampleArgs: new object[] { "string" },
					invokeIsAlwaysTargetException,
					delegateDynamicInvokeIsAlwaysException);
				// Partially applied bound method cannot be rebound.
				Assert.Throws(typeof(ArgumentException), () => partialAppliedMethod.Bind(v));
			});
		}

		[TestCase(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod), false,
			typeof(Action<int, string[]>), typeof(Action<int, string>), false, false)]
		[TestCase(nameof(TestClassSimple.SimpleInstanceNoThisUsageMethod), false,
			typeof(Func<int, string[], int>), typeof(Action<int, string[]>), false, false)]
		[TestCase(nameof(TestClassSimple.SimpleInstanceSafeThisUsageMethod), false,
			typeof(Func<int, string[], TestClassSimple>), typeof(Func<int, string[], int>), false, false)]
		[TestCase(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod), true,
			typeof(Action<int, string[]>), typeof(Action<int, string>), true, true)]
		[TestCase(nameof(TestClassSimple.SimpleInstanceNoThisUsageMethod), true,
			typeof(Func<int, string[], int>), typeof(Action<int, string[]>), true, false)]
		[TestCase(nameof(TestClassSimple.SimpleInstanceSafeThisUsageMethod), true,
			typeof(Func<int, string[], TestClassSimple>), typeof(Func<int, string[], int>), true, false)]
		public void Bind_ClassInstanceMethod_Error(string methodName, bool bindNull,
			Type validDelegateType, Type invalidDelegateType, bool invokeIsAlwaysTargetException, bool delegateDynamicInvokeIsAlwaysException)
		{
			MethodClosureExtensionsFixture.Do(fixture =>
			{
				var c = new TestClassSimple(15);
				var method = typeof(TestClassSimple).GetMethod(methodName);
				// Instance method cannot be bound to invalid target.
				Assert.Throws(typeof(ArgumentException), () => method.Bind(this));
				var boundMethod = bindNull ? method.Bind(null) : method.Bind(c);
				AssertStaticMethodErrors(fixture, boundMethod,
					validDelegateType,
					invalidDelegateType,
					validSampleArgs: new object[] { 5, new[] { "hello", "world" } },
					invalidSampleArgs: new object[] { 5, "string" },
					invokeIsAlwaysTargetException,
					delegateDynamicInvokeIsAlwaysException);
			});
		}

		[TestCase(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod), false,
			typeof(Action<string[]>), typeof(Action<string>), false, false)]
		[TestCase(nameof(TestClassSimple.SimpleInstanceNoThisUsageMethod), false,
			typeof(Func<string[], int>), typeof(Action<string[], int>), false, false)]
		[TestCase(nameof(TestClassSimple.SimpleInstanceSafeThisUsageMethod), false,
			typeof(Func<string[], TestClassSimple>), typeof(Func<string[], int>), false, false)]
		[TestCase(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod), true,
			typeof(Action<string[]>), typeof(Action<string>), true, true)]
		[TestCase(nameof(TestClassSimple.SimpleInstanceNoThisUsageMethod), true,
			typeof(Func<string[], int>), typeof(Action<string[], int>), true, false)]
		[TestCase(nameof(TestClassSimple.SimpleInstanceSafeThisUsageMethod), true,
			typeof(Func<string[], TestClassSimple>), typeof(Func<string[], int>), true, false)]
		public void Bind_PartialApply_ClassInstanceMethod_Error(string methodName, bool bindNull,
			Type validDelegateType, Type invalidDelegateType, bool invokeIsAlwaysTargetException, bool delegateDynamicInvokeIsAlwaysException)
		{
			MethodClosureExtensionsFixture.Do(fixture =>
			{
				var c = new TestClassSimple(15);
				var method = typeof(TestClassSimple).GetMethod(methodName);
				var partialAppliedMethod = method.PartialApply(5);
				var boundMethod = bindNull ? partialAppliedMethod.Bind(null) : partialAppliedMethod.Bind(c);
				AssertStaticMethodErrors(fixture, boundMethod,
					validDelegateType,
					invalidDelegateType,
					validSampleArgs: new object[] { new[] { "hello", "world" } },
					invalidSampleArgs: new object[] { "string" },
					invokeIsAlwaysTargetException,
					delegateDynamicInvokeIsAlwaysException);
				// Bound partially applied method cannot be rebound.
				Assert.Throws(typeof(ArgumentException), () => boundMethod.Bind(c));
			});
		}

		[TestCase(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod), false,
			typeof(Action<string[]>), typeof(Action<string>), false, false)]
		[TestCase(nameof(TestClassSimple.SimpleInstanceNoThisUsageMethod), false,
			typeof(Func<string[], int>), typeof(Action<string[], int>), false, false)]
		[TestCase(nameof(TestClassSimple.SimpleInstanceSafeThisUsageMethod), false,
			typeof(Func<string[], TestClassSimple>), typeof(Func<string[], int>), false, false)]
		[TestCase(nameof(TestClassSimple.SimpleVirtualInstanceVoidMethod), true,
			typeof(Action<string[]>), typeof(Action<string>), true, true)]
		[TestCase(nameof(TestClassSimple.SimpleInstanceNoThisUsageMethod), true,
			typeof(Func<string[], int>), typeof(Action<string[], int>), true, false)]
		[TestCase(nameof(TestClassSimple.SimpleInstanceSafeThisUsageMethod), true,
			typeof(Func<string[], TestClassSimple>), typeof(Func<string[], int>), true, false)]
		public void PartialApply_Bind_ClassInstanceMethod_Error(string methodName, bool bindNull,
			Type validDelegateType, Type invalidDelegateType, bool invokeIsAlwaysTargetException, bool delegateDynamicInvokeIsAlwaysException)
		{
			MethodClosureExtensionsFixture.Do(fixture =>
			{
				var c = new TestClassSimple(15);
				var method = typeof(TestClassSimple).GetMethod(methodName);
				var boundMethod = bindNull ? method.Bind(null) : method.Bind(c);
				var partialAppliedMethod = boundMethod.PartialApply(5);
				AssertStaticMethodErrors(fixture, partialAppliedMethod,
					validDelegateType,
					invalidDelegateType,
					validSampleArgs: new object[] { new[] { "hello", "world" } },
					invalidSampleArgs: new object[] { "string" },
					invokeIsAlwaysTargetException,
					delegateDynamicInvokeIsAlwaysException);
				// Partially applied bound method cannot be rebound.
				Assert.Throws(typeof(ArgumentException), () => partialAppliedMethod.Bind(c));
			});
		}

		// TODO: IsStatic tests.

		void AssertStaticMethodErrors(MethodClosureExtensionsFixture fixture, MethodInfo method,
			Type validDelegateType, Type invalidDelegateType,
			object[] validSampleArgs, object[] invalidSampleArgs,
			bool invokeIsAlwaysTargetException, bool delegateDynamicInvokeIsAlwaysException)
		{
			if (invokeIsAlwaysTargetException)
			{
				Assert.Throws(typeof(TargetException), () => method.Invoke(null, validSampleArgs));
				Assert.Throws(typeof(TargetException), () => method.Invoke(this, validSampleArgs));
			}
			else
			{
				using (fixture.DebugOnlyFilter())
				{
					// Verify that validSampleArgs is, well, valid.
					method.Invoke(null, validSampleArgs);
					// Invocation target is ignored for static methods.
					method.Invoke(this, validSampleArgs);
				}
			}
			if (delegateDynamicInvokeIsAlwaysException)
			{
				var @delegate = method.CreateDelegate(validDelegateType);
				Assert.Throws(typeof(TargetInvocationException), () => @delegate.DynamicInvoke(validSampleArgs));
				@delegate = method.CreateDelegate(validDelegateType, null);
				Assert.Throws(typeof(TargetInvocationException), () => @delegate.DynamicInvoke(validSampleArgs));
			}
			else
			{
				using (fixture.DebugOnlyFilter())
				{
					// Verify that validDelegateType is indeed valid.
					method.CreateDelegate(validDelegateType).DynamicInvoke(validSampleArgs);
					// null is valid target for static method CreateDelegate.
					method.CreateDelegate(validDelegateType, null).DynamicInvoke(validSampleArgs);
				}
			}
			if (validSampleArgs.Length > 0)
			{
				// Method cannot be invoked with too few parameters.
				Assert.Throws(invokeIsAlwaysTargetException ? typeof(TargetException) : typeof(TargetParameterCountException),
					() => method.Invoke(null, new object[0]));
			}
			// Method cannot be invoked with too many parameters.
			Assert.Throws(invokeIsAlwaysTargetException ? typeof(TargetException) : typeof(TargetParameterCountException),
				() => method.Invoke(null, validSampleArgs.Append("extra")));
			if (!(invalidSampleArgs is null))
			{
				// Method cannot be invoked with invalid parameter type.
				Assert.Throws(invokeIsAlwaysTargetException ? typeof(TargetException) : typeof(ArgumentException),
					() => method.Invoke(null, invalidSampleArgs));
			}
			// Static method CreateDelegate cannot be invoked with a non-null target.
			// Note: On Mono runtime, TargetParameterCountException will be thrown by MethodInfo.CreateDelegate.
			// In all other cases (MethodInfo.CreateDelegate on MS .NET runtime, ClosureMethod.CreateDelegate), ArgumentException will be thrown.
			AssertThrowsOneOfTwoExceptions<ArgumentException, TargetParameterCountException>(() => method.CreateDelegate(validDelegateType, this));
			// CreateDelegate cannot be invoked with null delegate type.
			Assert.Throws(typeof(ArgumentNullException), () => method.CreateDelegate(null));
			// CreateDelegate cannot be invoked with an invalid delegate type.
			// Note: On Mono runtime, if Action/Func and has wrong # of type parameters, TargetParameterCountException is thrown instead
			// so just ensure either the Action is passed for Func (or vice versa), or the same # of type parameters are passed,
			// just with wrong type parameter(s).
			Assert.Throws(typeof(ArgumentException), () => method.CreateDelegate(invalidDelegateType));
		}

		void AssertInstanceMethodErrors(MethodClosureExtensionsFixture fixture, object instance, MethodInfo method,
			Type validDelegateType, Type invalidDelegateType,
			object[] validSampleArgs, object[] invalidSampleArgs,
			bool nullTargetDelegateIsException)
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
			// CreateDelegate cannot be invoked with null delegate type.
			Assert.Throws(typeof(ArgumentNullException), () => method.CreateDelegate(null));
			Assert.Throws(typeof(ArgumentNullException), () => method.CreateDelegate(null, null));
			Assert.Throws(typeof(ArgumentNullException), () => method.CreateDelegate(null, instance));
			// CreateDelegate(delegateType, null) doesn't throw error, but invocation of the resulting delegate results is invalid.
			// Note: On Mono runtime, TargetException will be thrown by MethodInfo.CreateDelegate.
			// In all other cases (MethodInfo.CreateDelegate on MS .NET runtime, ClosureMethod.CreateDelegate), TargetInvocationException will be thrown.
			var nullBoundDelegate = method.CreateDelegate(validDelegateType, null);
			if (nullTargetDelegateIsException)
				AssertThrowsOneOfTwoExceptions<TargetInvocationException, TargetException>(() => nullBoundDelegate.DynamicInvoke(validSampleArgs));
			else
				nullBoundDelegate.DynamicInvoke(validSampleArgs);
		}

		static void AssertThrowsOneOfTwoExceptions<E1, E2>(TestDelegate testDelegate) where E1 : Exception where E2 : Exception
		{
			try
			{
				testDelegate();
				return;
			}
			catch (Exception exception)
			{
				if (exception is E1 || exception is E2)
					return;
				Assert.Throws(typeof(ArgumentException), testDelegate);
			}
			Assert.Throws(typeof(ArgumentException), testDelegate);
		}
	}
}
