using System;

namespace LbmLib
{
	public static class Logging
	{
		public static readonly Action<string> ConsoleLogger = str => Console.WriteLine(str);

		public static readonly Action<string> NullLogger = str => { };

		public static readonly Func<object, string> ObjectToStringer = obj => obj?.ToString() ?? "null";

		public static Action<string> DefaultLogger = ConsoleLogger;

		// TODO: Use a new interface that allows both object => string and T => string?
		public static Func<object, string> DefaultToStringer = ObjectToStringer;

		public static string SingleLineLabelDelimiter = ": ";

		public static string MultiLineLabelDelimiter = ":\n\t";

		sealed class WithObject : IDisposable
		{
			readonly Action<string> origLogger;
			readonly Func<object, string> origToStringer;

			public WithObject(Action<string> logger, Func<object, string> toStringer)
			{
				if (!(logger is null))
				{
					origLogger = DefaultLogger;
					DefaultLogger = logger ?? DefaultLogger;
				}
				if (!(toStringer is null))
				{
					origToStringer = DefaultToStringer;
					DefaultToStringer = toStringer ?? DefaultToStringer;
				}
			}

			public void Dispose()
			{
				if (!(origLogger is null))
					DefaultLogger = origLogger;
				if (!(origToStringer is null))
					DefaultToStringer = origToStringer;
			}
		}

		public static IDisposable With(Action<string> logger = null, Func<object, string> toStringer = null)
		{
			return new WithObject(logger, toStringer);
		}

		// Note: The <T> is redundant here in C#, but when generating CIL that calls this method,
		// it does away with the callee potentially needing to check whether input obj is a value type and thus needs boxing.
		public static void Log<T>(this T obj, string label = "", string labelDelimiter = null, Action<string> logger = null, Func<object, string> toStringer = null)
		{
			if (logger is null)
				logger = DefaultLogger;
			if (toStringer is null)
				toStringer = DefaultToStringer;
			if (string.IsNullOrEmpty(label))
			{
				logger(toStringer(obj));
			}
			else
			{
				var str = toStringer(obj);
				if (labelDelimiter is null)
					labelDelimiter = str.Contains("\n") ? MultiLineLabelDelimiter : SingleLineLabelDelimiter;
				logger(label + labelDelimiter + str);
			}
		}

		public static T Logged<T>(this T obj, string label = "", string labelDelimiter = null, Action<string> logger = null, Func<object, string> toStringer = null)
		{
			Log(obj, label, labelDelimiter, logger, toStringer);
			return obj;
		}

		public static void StringLog(this string str)
		{
			DefaultLogger(str ?? "null");
		}
	}
}
