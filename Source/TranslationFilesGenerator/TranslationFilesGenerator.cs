using System.Collections.Generic;
using System.Linq;
using Verse;

namespace TranslationFilesGenerator
{
	public enum TranslationFilesMode
	{
		Clean,
		GenerateForMod,
	}

	// This class provides the mechanism for adding additional "arguments" to TranslationFilesCleaner.
	// TranslationFilesCleaner is a static class and thus can't be subclassed, so instead we have an object containing such additional arguments,
	// and various HarmonyPrefix and HarmonyPostfix patches that read and use these arguments.
	public static class TranslationFilesGenerator
	{
		// RimWorld is effectively single-threaded*, and this class is practically used one at a time, so no need for ThreadStatic or related.
		// (Actually, RimWorld does have an event system that runs in a different thread via LongEventHandler, which is used in TranslationFilesCleaner,
		// so ThreadStatic couldn't actually be used here anyway.)

		public static TranslationFilesMode Mode { get; private set; } // defaults to TranslationFilesMode.Clean
		public static ModContentPack ModContentPack { get; private set; }
		public static LoadedLanguage TargetLanguage { get; private set; }
		public static LoadedLanguage OriginalActiveLanguage { get; private set; }

		public static void Begin(ModContentPack modContentPack, LoadedLanguage targetLanguage)
		{
			Mode = TranslationFilesMode.GenerateForMod;
			ModContentPack = modContentPack;
			TargetLanguage = targetLanguage;
			OriginalActiveLanguage = LanguageDatabase.activeLanguage;
			//Log.Message($"TranslationFilesGenerator.Begin: {ToString()}");
			// Note: LanguageDatabase.activeLanguage is only changed within DoCleanupTranslationFiles,
			// so that confirmation dialogs and such are still translated in the current active language.
			TranslationFilesCleaner.CleanupTranslationFiles();
		}

		public static void End()
		{
			//Log.Message($"TranslationFilesGenerator.End: {ToString()}");

			var targetLanguage = TargetLanguage;
			if (targetLanguage != OriginalActiveLanguage)
			{
				// Revert the injections from the target language.
				// This is done by setting each injection's translation string/list to the replaced string/list, then calling InjectIntoData_BeforeImpliedDefs
				// (which unlike InjectIntoData_AfterImpliedDefs doesn't log load errors nor unnecessarily inject backstory data).
				var defInjections = targetLanguage.defInjections.SelectMany(defInjectionPackage => defInjectionPackage.injections.Values);
				foreach (DefInjectionPackage.DefInjection defInjection in defInjections)
				{
					if (defInjection.IsFullListInjection)
						defInjection.fullListInjection = defInjection.replacedList.AsList();
					else
						defInjection.injection = defInjection.replacedString;
					defInjection.injected = false;
				}
				targetLanguage.InjectIntoData_BeforeImpliedDefs();
				// Resetting the target language technically isn't necessary, but it does save a bit of memory.
				targetLanguage.ResetDataAndErrors();
			}

			// Resetting Instance has to be done separately in a DoCleanupTranslationFiles HarmonyPostfix patch (which calls this method),
			// since TranslationFilesCleaner uses LongEventHandler for deferring execution.
			Mode = TranslationFilesMode.Clean;
			ModContentPack = null;
			TargetLanguage = null;
			OriginalActiveLanguage = null;

			// We need to reload the language data now that it's either switched back to or updated.
			// This needs to be done AFTER the above arguments are reset, so that the changes in the LoadedLanguage HarmonyTranspiler patch are effectively reverted.
			var activeLanguage = LanguageDatabase.activeLanguage;
			// Not using LanguageDatabase.SelectLanguage, since it's really slow.
			activeLanguage.ResetDataAndErrors();
			// InjectIntoData_AfterImpliedDefs does everything InjectIntoData_BeforeImpliedDefs does along with load error logging and backstory load/injections.
			activeLanguage.InjectIntoData_AfterImpliedDefs();
			//Log.Message($"Reload language {activeLanguage}");
		}

		public static new string ToString()
		{
			return $"TranslationFilesGenerator[Mode={Mode}, ModMetaData={ModContentPack}, TargetLanguage={TargetLanguage}, OriginalActiveLanguage={OriginalActiveLanguage}]";
		}
	}
}
