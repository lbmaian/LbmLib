using NUnit.Framework;

namespace LbmLib.Language.Experimental.Tests
{
	// Note: Method and structure fixtures are public so that methods dynamically created via DebugDynamicMethodBuilder have access to them.

	[TestFixture]
	public class MethodClosureExtensionsTestsGeneric : MethodClosureExtensionsBase
	{
		// TODO: Test ClosureMethod.MakeGenericMethod on non-GenericMethodDefinition method => throws exception.

		// TODO: Test ClosureMethod.MakeGenericMethod on GenericMethodDefinition method.
	}
}
