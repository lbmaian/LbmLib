using System;
using Harmony;

namespace LbmLib.Harmony
{
	[AttributeUsage(AttributeTargets.Class)]
	public class HarmonyDebugAttribute : Attribute
	{
	}

	public sealed class HarmonyWithDebug : IDisposable
	{
		readonly bool origDebug;

		public HarmonyWithDebug(bool debug = true)
		{
			origDebug = HarmonyInstance.DEBUG;
			HarmonyInstance.DEBUG = debug;
		}

		public void Dispose()
		{
			HarmonyInstance.DEBUG = origDebug;
		}
	}
}
