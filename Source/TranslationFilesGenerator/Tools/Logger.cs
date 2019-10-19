using System;

namespace TranslationFilesGenerator.Tools
{
	public static class Logging
	{
		public static readonly Action<string> ConsoleLogger = str => Console.WriteLine(str);

		public static readonly Action<string> NullLogger = str => { };

		public static readonly Func<object, string> ObjectToStringer = obj => obj?.ToString() ?? "null";

		public static Action<string> DefaultLogger = ConsoleLogger;

		public static Func<object, string> DefaultToStringer = ObjectToStringer;

		public static string SingleLineLabelDelimiter = ": ";

		public static string MultiLineLabelDelimiter = ":\n\t";

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
	}
}
