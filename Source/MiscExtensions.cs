using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace TranslationFilesGenerator
{
	public static class MiscExtensions
	{
		public static List<T> AsList<T>(this IEnumerable<T> enumerable)
		{
			return enumerable as List<T> ?? new List<T>(enumerable);
		}

		// XXX: Needs a better name.
		public static List<T> PopAll<T>(this ICollection<T> collection)
		{
			var collectionCopy = new List<T>(collection);
			collection.Clear();
			return collectionCopy;
		}

		// XXX: Needs a better name.
		public static List<T> PopRange<T>(this List<T> list, int index, int count)
		{
			var range = list.GetRange(index, count);
			list.RemoveRange(index, count);
			return range;
		}

		public static List<T> AddDefaultIfEmpty<T>(this List<T> list, Func<T> defaultSupplier)
		{
			if (list.Count == 0)
				list.Add(defaultSupplier());
			return list;
		}

		public static int RemoveAll<K, V>(this Dictionary<K, V> dictionary, Predicate<K> keyPredicate = null, Predicate<V> valuePredicate = null, Predicate<KeyValuePair<K, V>> pairPredicate = null)
		{
			int removeCount = 0;
			if (keyPredicate != null)
			{
				foreach (var key in dictionary.Keys.Where(new Func<K, bool>(keyPredicate)).ToList())
				{
					if (dictionary.Remove(key))
						removeCount++;
				}
			}
			if (valuePredicate != null)
			{
				foreach (var key in dictionary.Where(pair => valuePredicate(pair.Value)).Select(pair => pair.Key).ToList())
				{
					if (dictionary.Remove(key))
						removeCount++;
				}
			}
			if (pairPredicate != null)
			{
				foreach (var key in dictionary.Where(new Func<KeyValuePair<K, V>, bool>(pairPredicate)).Select(pair => pair.Key).ToList())
				{
					if (dictionary.Remove(key))
						removeCount++;
				}
			}
			return removeCount;
		}

		public static string Join<T>(this IEnumerable<T> enumerable, string delimiter = ", ")
		{
			return enumerable.Aggregate("", (string prev, T curr) => prev + ((prev != "") ? delimiter : "") + curr?.ToString() ?? "null");
		}

		public static string LanguageLabel(this LoadedLanguage language)
		{
			if (language.FriendlyNameNative == language.FriendlyNameEnglish)
				return language.FriendlyNameNative;
			return $"{language.FriendlyNameNative} [{language.FriendlyNameEnglish}]";
		}

		// Tries to reset the target language to before its loaded state, including clearing any recorded errors.
		public static void ResetDataAndErrors(this LoadedLanguage language)
		{
			typeof(LoadedLanguage).GetField("dataIsLoaded", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(language, false);
			language.loadErrors.Clear();
			language.backstoriesLoadErrors.Clear();
			language.anyKeyedReplacementsXmlParseError = false;
			language.lastKeyedReplacementsXmlParseErrorInFile = null;
			language.anyDefInjectionsXmlParseError = false;
			language.lastDefInjectionsXmlParseErrorInFile = null;
			language.anyError = false;
			language.icon = BaseContent.BadTex;
			language.keyedReplacements.Clear();
			language.defInjections.Clear();
			language.stringFiles.Clear();
		}
	}
}
