using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace TranslationFilesGenerator.Tools
{
	public static class RimWorldLogging
	{
		public static readonly Action<string> RWLogger = str => Log.Message(str);

		public static readonly Action<string> UnityLogger = str => UnityEngine.Debug.Log(str);

		public static readonly Func<object, string> RWToStringer = obj =>
		{
			if (obj is string str)
				return str;
			if (obj is System.Collections.IEnumerable enumerable)
				return enumerable.ToStringSafeEnumerable();
			return obj.ToStringSafe();
		};
	}

	public static class LoadedLanguageExtensions
	{
		public static string LanguageLabel(this LoadedLanguage language)
		{
			if (language.FriendlyNameNative == language.FriendlyNameEnglish)
				return language.FriendlyNameNative;
			return $"{language.FriendlyNameNative} [{language.FriendlyNameEnglish}]";
		}

		// Tries to reset the given language to before its loaded state, including clearing any recorded errors.
		// This doesn't reset loaded metadata (as referenced in LoadedLanguage.TryLoadMetadataFrom) or the Worker for the language.
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
			var wordInfo = (LanguageWordInfo)typeof(LoadedLanguage).GetField("wordInfo", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(language);
			var genders = (Dictionary<string, Gender>)typeof(LanguageWordInfo).GetField("genders", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(wordInfo);
			genders.Clear();
		}
	}
}
