using System;

namespace LbmLib.Language
{
	public static class DebugLogging
	{
		public static readonly Func<object, string> ToDebugStringer = obj => obj.ToDebugString();
	}
}
