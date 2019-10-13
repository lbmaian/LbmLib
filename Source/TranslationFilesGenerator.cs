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
			// Note: LanguageDatabase.activeLanguage is only changed within DoCleanupTranslationFiles,
			// so that confirmation dialogs and such are still translated in the current active language.
			TranslationFilesCleaner.CleanupTranslationFiles();
		}

		public static void End()
		{
			var targetLanguage = TargetLanguage;

			// Resetting Instance has to be done separately in a DoCleanupTranslationFiles HarmonyPostfix patch (which calls this method),
			// since TranslationFilesCleaner uses LongEventHandler for deferring execution.
			Mode = TranslationFilesMode.Clean;
			ModContentPack = null;
			TargetLanguage = null;
			OriginalActiveLanguage = null;

			// If the target language and original active language were the same, we need to reload the language data now that it's updated.
			// This needs to be done AFTER the above arguments are reset, which effectively reverts the functional changes in the LoadedLanguage HarmonyTranspiler patch.
			var activeLanguage = LanguageDatabase.activeLanguage;
			if (targetLanguage == activeLanguage)
			{
				// Not using LanguageDatabase.SelectLanguage, since it's really slow.
				activeLanguage.ResetDataAndErrors();
				activeLanguage.InjectIntoData_AfterImpliedDefs();
			}
		}

		public static new string ToString()
		{
			return $"TranslationFilesGenerator[Mode={Mode}, ModMetaData={ModContentPack}, TargetLanguage={TargetLanguage}, OriginalActiveLanguage={OriginalActiveLanguage}]";
		}
	}
}
