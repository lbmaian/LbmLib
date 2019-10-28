using System;

namespace LbmLib.Language
{
	public static class StringExtensions
	{
		// More convenient string methods for splitting strings.

		public static string[] SplitStringDelimiter(this string str, string delimiter, StringSplitOptions options = StringSplitOptions.None)
		{
			return str.Split(new[] { delimiter }, options);
		}

		public static string[] SplitStringDelimiter(this string str, string[] delimiters, StringSplitOptions options = StringSplitOptions.None)
		{
			return str.Split(delimiters, options);
		}

		public static string[] SplitKeepStringDelimiter(this string str, string delimiter, int keepDelimiterIndex)
		{
			var leftDelimiter = delimiter.Substring(0, keepDelimiterIndex);
			var rightDelimiter = delimiter.Substring(keepDelimiterIndex);
			var strs = str.SplitStringDelimiter(delimiter);
			var endIndex = strs.Length - 1;
			if (endIndex == 0)
				return strs;
			strs[0] += leftDelimiter;
			for (var index = 1; index < endIndex; index++)
			{
				strs[index] = rightDelimiter + strs[index] + leftDelimiter;
			}
			strs[endIndex] = rightDelimiter + strs[endIndex];
			return strs;
		}
	}
}
