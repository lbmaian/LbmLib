namespace LbmLib.Language.Experimental.Tests
{
	// Note: Method and structure fixtures are public so that methods dynamically created via DebugDynamicMethodBuilder have access to them.

	// structs have no inheritance, so using partial struct as a workaround.
	public partial struct TestStruct
	{
		public int X;

		public TestStruct(int x)
		{
			X = x;
		}

		public override bool Equals(object obj) => obj is TestStruct test && X == test.X;

		public override int GetHashCode() => -1830369473 + X.GetHashCode();

		public override string ToString() => $"TestStruct{{{X}}}";

		public static bool operator ==(TestStruct left, TestStruct right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(TestStruct left, TestStruct right)
		{
			return !(left == right);
		}
	}

	public class TestClass
	{
		public int X;

		public TestClass(int x)
		{
			X = x;
		}

		public override bool Equals(object obj) => obj is TestClass test && X == test.X;

		public override int GetHashCode() => -1830369473 + X.GetHashCode();

		public override string ToString() => $"TestClass{{{X}}}";

		public static bool operator ==(TestClass left, TestClass right)
		{
			return left is null ? right is null : left.Equals(right);
		}

		public static bool operator !=(TestClass left, TestClass right)
		{
			return !(left == right);
		}
	}
}
