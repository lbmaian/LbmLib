﻿using System;

namespace LbmLib.Language
{
	public static class ArrayExtensions
	{
		// More Array-specific version of Enumerable.Concat.
		public static T[] Append<T>(this T[] array, params T[] toAppend)
		{
			var arrayLength = array.Length;
			var toAppendLength = toAppend.Length;
			var combinedArray = new T[arrayLength + toAppendLength];
			Array.Copy(array, 0, combinedArray, 0, arrayLength);
			Array.Copy(toAppend, 0, combinedArray, arrayLength, toAppendLength);
			return combinedArray;
		}

#if !NET35
		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
#endif
		public static T[] Prepend<T>(this T[] array, params T[] toPrepend)
		{
			return toPrepend.Append(array);
		}

		// Faster and more convenient that (T[])array.Clone().
		public static T[] Copy<T>(this T[] array)
		{
			var arrayLength = array.Length;
			var copiedArray = new T[arrayLength];
			Array.Copy(array, 0, copiedArray, 0, arrayLength);
			return copiedArray;
		}

		// Array version of IList.GetRange(index, count).
		public static T[] Copy<T>(this T[] array, int index, int count)
		{
			if (index < 0)
				throw new ArgumentOutOfRangeException($"index ({index}) cannot be < 0");
			if (count < 0)
				throw new ArgumentOutOfRangeException($"count ({count}) cannot be < 0");
			var arrayLength = array.Length;
			if (index > arrayLength - count)
				throw new ArgumentOutOfRangeException($"index ({index}) + count ({count}) cannot be > array.Length ({arrayLength})");
			var range = new T[count];
			Array.Copy(array, index, range, 0, count);
			return range;
		}

		// Array version of IList.GetRangeFromStart(count).
		public static T[] CopyFromStart<T>(this T[] array, int count)
		{
			if (count < 0)
				throw new ArgumentOutOfRangeException($"count ({count}) cannot be < 0");
			var arrayLength = array.Length;
			if (count > arrayLength)
				throw new ArgumentOutOfRangeException($"count ({count}) cannot be > array.Length ({arrayLength})");
			var range = new T[count];
			Array.Copy(array, 0, range, 0, count);
			return range;
		}

		// Array version of IList.GetRangeToEnd(index).
		public static T[] CopyToEnd<T>(this T[] array, int index)
		{
			if (index < 0)
				throw new ArgumentOutOfRangeException($"index ({index}) cannot be < 0");
			var arrayLength = array.Length;
			if (index > arrayLength)
				throw new ArgumentOutOfRangeException($"index ({index}) cannot be > array.Length ({arrayLength})");
			var count = arrayLength - index;
			var range = new T[count];
			Array.Copy(array, index, range, 0, count);
			return range;
		}
	}
}