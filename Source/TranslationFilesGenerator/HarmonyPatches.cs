using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Xml.Linq;
using Harmony;
using Harmony.ILCopying;
using RimWorld;
using TranslationFilesGenerator.Tools;
using UnityEngine;
using Verse;

namespace TranslationFilesGenerator
{
	[StaticConstructorOnStartup]
	static class HarmonyPatches
	{
		static HarmonyPatches()
		{
			HarmonyInstance harmony = HarmonyInstance.Create("rimworld.translation_files_generator");
			harmony.PatchAll();
		}
	}

	static class TranspilerSnippets
	{
		internal static List<CodeInstruction> TranslationFilesModeCheckInstructions(TranslationFilesMode mode, Label targetLabel)
		{
			return new List<CodeInstruction>()
			{
				new CodeInstruction(OpCodes.Call, typeof(TranslationFilesGenerator).GetProperty(nameof(TranslationFilesGenerator.Mode)).GetGetMethod()),
				new CodeInstruction(mode == TranslationFilesMode.Clean ? OpCodes.Ldc_I4_0 : OpCodes.Ldc_I4_1),
				new CodeInstruction(OpCodes.Beq, targetLabel),
			};
		}

		internal static List<CodeInstruction> LanguageCheckInstructions(bool sameLanguage, Label targetLabel)
		{
			return new List<CodeInstruction>()
			{
				new CodeInstruction(OpCodes.Ldsfld, typeof(LanguageDatabase).GetField(nameof(LanguageDatabase.activeLanguage))),
				new CodeInstruction(OpCodes.Ldsfld, typeof(LanguageDatabase).GetField(nameof(LanguageDatabase.defaultLanguage))),
				new CodeInstruction(sameLanguage ? OpCodes.Beq : OpCodes.Bne_Un, targetLabel),
			};
		}

		internal static void ReplaceFolderNotExistsErrorWithFolderCreate(List<CodeInstruction> instructions)
		{
			// Assumes the following code structure:
			//   if (!folder.Exists)
			//   {
			//     ...
			//     return;
			//   }
			// Effectively replaces it with:
			//   if (!folder.Exists)
			//   {
			//     folder.Create();
			//   }
			var firstReturnIndex = instructions.FindIndex(OpCodes.Ret.AsInstructionPredicate());
			var fileExistsCallIndex = instructions.FindLastIndex(firstReturnIndex - 1,
				OpCodes.Callvirt.AsInstructionPredicate(typeof(FileSystemInfo).GetProperty(nameof(FileSystemInfo.Exists)).GetGetMethod()));
			while (fileExistsCallIndex == -1)
			{
				firstReturnIndex = instructions.FindIndex(firstReturnIndex + 1, OpCodes.Ret.AsInstructionPredicate());
				fileExistsCallIndex = instructions.FindLastIndex(firstReturnIndex - 1,
					OpCodes.Callvirt.AsInstructionPredicate(typeof(FileSystemInfo).GetProperty(nameof(FileSystemInfo.Exists)).GetGetMethod()));
			}
			//Log.Message($"ReplaceFolderNotExistsErrorWithFolderCreate: {instructions.ItemToDebugString(firstReturnIndex, "firstReturnIndex=")}");
			//Log.Message($"ReplaceFolderNotExistsErrorWithFolderCreate: {instructions.ItemToDebugString(fileExistsCallIndex, "fileExistsCallIndex=")}");
			var dirInfoLoadIndex = fileExistsCallIndex - 1;
			var dirExistsCheckClauseStartIndex = fileExistsCallIndex + 2;
			//Log.Message($"ReplaceFolderNotExistsErrorWithFolderCreate: {instructions.RangeToDebugString(dirExistsCheckClauseStartIndex, firstReturnIndex - dirExistsCheckClauseStartIndex + 1)}");
			instructions.RemoveRange(dirExistsCheckClauseStartIndex, firstReturnIndex - dirExistsCheckClauseStartIndex + 1);
			instructions.InsertRange(dirExistsCheckClauseStartIndex, new[]
			{
				instructions[dirInfoLoadIndex].Clone(),
				new CodeInstruction(OpCodes.Call, typeof(DirectoryInfo).GetMethod(nameof(DirectoryInfo.Create), new Type[0])),
			});
		}
	}

	[HarmonyPatch(typeof(MainMenuDrawer), "DoTranslationInfoRect")]
	static class MainMenuDrawer_DoTranslationInfoRect_Patch
	{
		//static bool Prepare() { HarmonyInstance.DEBUG = true; return true; } // note: [HarmonyPrepare] is currently broken, so using specific named method approach
		//static void Cleanup() { HarmonyInstance.DEBUG = false; }

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructionEnumerable, ILGenerator ilGenerator)
		{
			var instructions = instructionEnumerable.AsList();

			// Skip the "activeLanguage != defaultLanguage" check.
			// Fortunately, this happens at the very start and doesn't involve any variables, so just remove the front few instructions.
			var firstReturnIndex = instructions.FindIndex(OpCodes.Ret.AsInstructionPredicate());
			instructions.RemoveRange(0, firstReturnIndex + 1);

			// Insert a new Rect variable for a new button and a copy of the way they're initialized (and the label Rect is modified) before the other button Rect's are initialized.
			var rectVar = ilGenerator.DeclareLocal(typeof(Rect));
			var firstRectInitLoadIndex = instructions.FindIndex(OpCodes.Ldloca_S.AsInstructionPredicate().LocalBuilder(localVar =>
				localVar.LocalIndex != 0 &&
				localVar.LocalType == typeof(Rect)));
			var firstRectSetHeightCallIndex = instructions.FindIndex(firstRectInitLoadIndex + 1,
				OpCodes.Call.AsInstructionPredicate(typeof(Rect).GetProperty(nameof(Rect.height)).GetSetMethod()));
			var newRectInstructions = instructions.CloneRange(firstRectInitLoadIndex, firstRectSetHeightCallIndex - firstRectInitLoadIndex + 1);
			newRectInstructions[0].operand = rectVar;
			instructions.InsertRange(firstRectInitLoadIndex, newRectInstructions);

			// Insert a new ButtonText for the new Rect after all the other ButtonText's (before the GUI.EndGroup call).
			var guiEndGroupCallIndex = instructions.FindLastIndex(OpCodes.Call.AsInstructionPredicate(typeof(GUI).GetMethod(nameof(GUI.EndGroup))));
			instructions.InsertRange(guiEndGroupCallIndex, new[]
			{
				new CodeInstruction(OpCodes.Ldloc_S, rectVar) { labels = instructions[guiEndGroupCallIndex].labels.PopAll() },
				new CodeInstruction(OpCodes.Call,
					typeof(MainMenuDrawer_DoTranslationInfoRect_Patch).GetMethod(nameof(MainMenuDrawer_DoTranslationInfoRect_Patch.AddGenerateTranslationFilesButton), AccessTools.all)),
			});

			return instructions;
		}

		static void AddGenerateTranslationFilesButton(Rect rect)
		{
			if (Widgets.ButtonText(rect, "GenerateTranslationFilesForMod".Translate()))
				DialogModLister();
		}

		static void DialogModLister()
		{
			// Only lists local (non-Steam) and non-Core running mods, since such mods are presumably mutable and possibly in development.
			var localNonCoreRunningMods = LoadedModManager.RunningMods.Where(mod => !ModLister.GetModWithIdentifier(mod.Identifier).OnSteamWorkshop && !mod.IsCoreMod);
			Dialog_DebugOptionListLister.ShowSimpleDebugMenu(localNonCoreRunningMods, mod => mod.Name, DialogLanguageLister);

		}

		static void DialogLanguageLister(ModContentPack mod)
		{
			var languages = LanguageDatabase.AllLoadedLanguages.ToList();
			languages.Remove(LanguageDatabase.defaultLanguage);
			languages.Insert(0, LanguageDatabase.defaultLanguage);
			if (LanguageDatabase.defaultLanguage != LanguageDatabase.activeLanguage)
			{
				languages.Remove(LanguageDatabase.activeLanguage);
				languages.Insert(1, LanguageDatabase.activeLanguage);
			}
			Dialog_DebugOptionListLister.ShowSimpleDebugMenu(languages, language => language.LanguageLabel(), language => TranslationFilesGenerator.Begin(mod, language));
		}
	}

	[HarmonyPatch(typeof(MainMenuDrawer), "MainMenuOnGUI")]
	static class MainMenuDrawer_MainMenuOnGUI_Patch
	{
		//static bool Prepare() { HarmonyInstance.DEBUG = true; return true; } // note: [HarmonyPrepare] is currently broken, so using specific named method approach
		//static void Cleanup() { HarmonyInstance.DEBUG = false; }

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructionEnumerable)
		{
			var instructions = instructionEnumerable.AsList();

			// Increase translation info rect width by 40 to fit the new "Generate translation files for mod" button text.
			var translationInfoRectConstructorIndex = instructions.FindLastIndex(
				OpCodes.Call.AsInstructionPredicate(typeof(Rect).GetConstructor(new[] { typeof(float), typeof(float), typeof(float), typeof(float) })));
			var rectWidthValueIndex = translationInfoRectConstructorIndex - 2;
			instructions[rectWidthValueIndex].operand = (float)instructions[rectWidthValueIndex].operand + 40;

			return instructions;
		}
	}

	[HarmonyPatch(typeof(TranslationFilesCleaner), "CleanupTranslationFiles")]
	static class TranslationFilesCleaner_CleanupTranslationFiles_Patch1
	{
		//static bool Prepare() { HarmonyInstance.DEBUG = true; return true; } // note: [HarmonyPrepare] is currently broken, so using specific named method approach
		//static void Cleanup() { HarmonyInstance.DEBUG = false; }

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructionEnumerable, ILGenerator ilGenerator)
		{
			var instructions = instructionEnumerable.AsList();

			// Now that "activeLanguage == defaultLanguage" is possible, in TranslationFilesMode.Clean mode (when the check is made, see below),
			// show a message indicating the error ala LanguageReportGenerator.
			var firstReturnIndex = instructions.FindIndex(OpCodes.Ret.AsInstructionPredicate());
			instructions.Insert(firstReturnIndex,
				new CodeInstruction(OpCodes.Call, typeof(TranslationFilesCleaner_CleanupTranslationFiles_Patch1).GetMethod(nameof(SameLanguageMessage), AccessTools.all)));
			firstReturnIndex++;

			// If in TranslationFilesMode.GenerateForMod mode, skip the "activeLanguage != defaultLanguage" check and the "activeModsInLoadOrder" check that happens right afterwards.
			// activeLanguage and defaultLanguage are stored in variables which are used elsewhere, so don't skip the initialization of those.
			var secondReturnIndex = instructions.FindIndex(firstReturnIndex + 1, OpCodes.Ret.AsInstructionPredicate());
			var afterLanguageVarInitIndex = instructions.FindLastIndex(firstReturnIndex - 1, OpCodes.Stfld.AsInstructionPredicate()) + 1;
			var afterChecksLabel = instructions[secondReturnIndex + 1].FirstOrNewAddedLabel(ilGenerator);
			instructions.InsertRange(afterLanguageVarInitIndex, TranspilerSnippets.TranslationFilesModeCheckInstructions(TranslationFilesMode.GenerateForMod, afterChecksLabel));

			// TODO: Call TranslationFilesGenerator.End() if LongEventHandler.QueueLongEvent doesn't happen (i.e. in the other if clauses), ideally in a finally block.

			return instructions;
		}

		static void SameLanguageMessage()
		{
			// LanguageReportGeneratod doesn't use a keyed translation for this, so not doing so here either.
			Messages.Message("Please activate a non-English language before cleaning.", MessageTypeDefOf.RejectInput, historical: false);
		}
	}

	[HarmonyPatch(typeof(TranslationFilesCleaner), "CleanupTranslationFiles")]
	static class TranslationFilesCleaner_CleanupTranslationFiles_Patch2
	{
		//static bool Prepare() { HarmonyInstance.DEBUG = true; return true; } // note: [HarmonyPrepare] is currently broken, so using specific named method approach
		//static void Cleanup() { HarmonyInstance.DEBUG = false; }

		[HarmonyTargetMethod]
		static MethodInfo CalculateMethod(HarmonyInstance harmony)
		{
			// TranslationFilesCleaner.'<CleanupTranslationFiles>c__AnonStorey0'.'<>m__0'
			// (delegate internal method of first LongEventHandler.QueueLongEvent in TranslationFilesCleaner.CleanupTranslationFiles)
			return typeof(TranslationFilesCleaner).GetNestedType("<CleanupTranslationFiles>c__AnonStorey0", AccessTools.all).GetMethod("<>m__0", AccessTools.all);
		}

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructionEnumerable)
		{
			var instructions = instructionEnumerable.AsList();

			var findGetWindowStackCallIndex = instructions.FindLastIndex(OpCodes.Call.AsInstructionPredicate(typeof(Find).GetProperty(nameof(Find.WindowStack)).GetGetMethod()));
			instructions.InsertRange(findGetWindowStackCallIndex, new[]
			{
				new CodeInstruction(OpCodes.Ldloc_1), // confirmationDialog
				new CodeInstruction(OpCodes.Call, typeof(TranslationFilesCleaner_CleanupTranslationFiles_Patch2).GetMethod(nameof(ModifyConfirmationDialog), AccessTools.all)),
			});

			// TODO: Call TranslationFilesGenerator.End() if Dialog_MessageBox.CreateConfirmation doesn't happen (i.e. in the other if clause), ideally in a finally block.

			return instructions;
		}

		static void ModifyConfirmationDialog(Dialog_MessageBox confirmationDialog)
		{
			if (TranslationFilesGenerator.Mode == TranslationFilesMode.GenerateForMod)
			{
				confirmationDialog.text = "ConfirmGenerateTranslationFiles".Translate(TranslationFilesGenerator.ModContentPack.Name,
					TranslationFilesGenerator.TargetLanguage.LanguageLabel(),
					LanguageDatabase.defaultLanguage.FriendlyNameNative);
				confirmationDialog.buttonAText = "ConfirmGenerateTranslationFiles_Confirm".Translate();
				confirmationDialog.buttonBAction = TranslationFilesGenerator.End;
				confirmationDialog.cancelAction = TranslationFilesGenerator.End;
			}
		}
	}

	[HarmonyPatch(typeof(TranslationFilesCleaner), "CleanupTranslationFiles")]
	static class TranslationFilesCleaner_CleanupTranslationFiles_Patch3
	{
		//static bool Prepare() { HarmonyInstance.DEBUG = true; return true; } // note: [HarmonyPrepare] is currently broken, so using specific named method approach
		//static void Cleanup() { HarmonyInstance.DEBUG = false; }

		[HarmonyTargetMethod]
		static MethodInfo CalculateMethod(HarmonyInstance harmony)
		{
			// TranslationFilesCleaner.'<CleanupTranslationFiles>c__AnonStorey0'.'<>m__1'
			// (delegate internal method of Dialog_MessageBox.CreateConfirmation in TranslationFilesCleaner.CleanupTranslationFiles)
			return typeof(TranslationFilesCleaner).GetNestedType("<CleanupTranslationFiles>c__AnonStorey0", AccessTools.all).GetMethod("<>m__1", AccessTools.all);
		}

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructionEnumerable, ILGenerator ilGenerator)
		{
			var instructions = instructionEnumerable.AsList();

			var progressStrLoadIndex = instructions.FindIndex(OpCodes.Ldstr.AsInstructionPredicate("CleaningTranslationFiles"));
			var afterProgressStrLoadIndex = progressStrLoadIndex + 1;

			// Fix a TranslationFilesCleaner bug where Translated is called on the progress string, when it's already called within LongEventHandler,
			// leading to weird pseudo-translation. Fix is to just remove the Translate call.
			if (OpCodes.Call.AsInstructionPredicate(typeof(Translator).GetMethod(nameof(Translator.Translate), new[] { typeof(string) }))(instructions[afterProgressStrLoadIndex]))
				instructions.RemoveAt(afterProgressStrLoadIndex);

			// Use GeneratingTranslationFilesForMod progress string key if TranslationFilesMode.GenerateInMod;
			// else continue using existing CleaningTranslationFiles progress string key.
			var progressStrLoadLabel = instructions[progressStrLoadIndex].FirstOrNewAddedLabel(ilGenerator);
			var afterProgressStrLoadLabel = instructions[afterProgressStrLoadIndex].FirstOrNewAddedLabel(ilGenerator);
			var newProgressStrInstructions = TranspilerSnippets.TranslationFilesModeCheckInstructions(TranslationFilesMode.Clean, progressStrLoadLabel);
			newProgressStrInstructions.AddRange(new[]
			{
				new CodeInstruction(OpCodes.Ldstr, "GeneratingTranslationFilesForMod"),
				new CodeInstruction(OpCodes.Br, afterProgressStrLoadLabel),
			});
			instructions.InsertRange(progressStrLoadIndex, newProgressStrInstructions);

			//Log.Message($"TranslationFilesCleaner_CleanupTranslationFiles_Patch3:\n\t{instructions.ToDebugString()}");
			return instructions;
		}
	}

	[HarmonyPatch(typeof(TranslationFilesCleaner), "DoCleanupTranslationFiles")]
	static class TranslationFilesCleaner_DoCleanupTranslationFiles_Patch
	{
		//static bool Prepare() { HarmonyInstance.DEBUG = true; return true; } // note: [HarmonyPrepare] is currently broken, so using specific named method approach
		//static void Cleanup() { HarmonyInstance.DEBUG = false; }

		[HarmonyPrefix]
		static void Prefix()
		{
			// See comments in TranslationFilesGenerator.Begin.
			ChangeToTargetLanguage();
		}

		[HarmonyPostfix]
		static void Postfix()
		{
			// TODO: Currently this does NOT act as a finally block - if the method throws an exception, postfixes don't run!
			// Note that this acts as a finally block for the whole method, so it's guaranteed to run.
			// See comments in TranslationFilesGenerator.End.
			ResetToOriginalActiveLanguage();
			TranslationFilesGenerator.End();
		}

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructionEnumerable)
		{
			var instructions = instructionEnumerable.AsList();

			// Skip the "activeLanguage != defaultLanguage" check. Even in TranslationFilesMode.Clean mode, the check is redundant.
			// Fortunately, this happens at the very start and doesn't involve any variables, so just remove the front few instructions.
			var firstReturnIndex = instructions.FindIndex(OpCodes.Ret.AsInstructionPredicate());
			instructions.RemoveRange(0, firstReturnIndex + 1);

			// Reset back to the original active language before the message done string is translated, yet after the active language folder path is fetched.
			var getActiveLanguageFolderPathCallIndex = instructions.FindIndex(
				OpCodes.Call.AsInstructionPredicate(typeof(TranslationFilesCleaner).GetMethod("GetActiveLanguageCoreModFolderPath", AccessTools.all)));
			var resetToOriginalActiveLanguageMethod = typeof(TranslationFilesCleaner_DoCleanupTranslationFiles_Patch).GetMethod(nameof(ResetToOriginalActiveLanguage), AccessTools.all);
			instructions.Insert(getActiveLanguageFolderPathCallIndex + 1,
				new CodeInstruction(OpCodes.Call, resetToOriginalActiveLanguageMethod) { labels = instructions[getActiveLanguageFolderPathCallIndex].labels.PopAll() });

			//Log.Message($"TranslationFilesCleaner_DoCleanupTranslationFiles_Patch: {instructions.ToDebugString()}");
			return instructions;
		}

		static void ChangeToTargetLanguage()
		{
			var targetLanguage = TranslationFilesGenerator.TargetLanguage;
			if (TranslationFilesGenerator.OriginalActiveLanguage != LanguageDatabase.activeLanguage)
				throw new InvalidOperationException($"activeLanguage {LanguageDatabase.activeLanguage} " +
					$"unexpectedly not OriginalActiveLanguage {TranslationFilesGenerator.OriginalActiveLanguage}");
			if (targetLanguage != LanguageDatabase.activeLanguage)
			{
				LanguageDatabase.activeLanguage = targetLanguage;
				targetLanguage.InjectIntoData_BeforeImpliedDefs();
				// TODO: Should DefInjectionPackage.DefInjection.fileSource and TranslationFilesCleaner.GetSourceFile be patched to include relative path,
				// so that it would be possible to filter DefInjection by mod? While nice, it's not necessary and probably not worth the effort.
				//Log.Message($"Change activeLanguage from {TranslationFilesGenerator.OriginalActiveLanguage} to {targetLanguage}");
			}
		}

		static void ResetToOriginalActiveLanguage()
		{
			var targetLanguage = LanguageDatabase.activeLanguage;
			var originalActiveLanguage = TranslationFilesGenerator.OriginalActiveLanguage;
			if (targetLanguage != originalActiveLanguage)
			{
				LanguageDatabase.activeLanguage = originalActiveLanguage;
				//Log.Message($"Reset activeLanguage back to {originalActiveLanguage}");
				// Note: Reloading the original activeLanguage will be done in TranslationFilesGenerator.End after the arguments are reset,
				// so that the changes in the LoadedLanguage HarmonyTranspiler patch are effectively reverted.
			}
		}
	}

	[HarmonyPatch(typeof(TranslationFilesCleaner), "CleanupKeyedTranslations")]
	static class TranslationFilesCleaner_CleanupKeyedTranslations_Patch
	{
		//static bool Prepare() { HarmonyInstance.DEBUG = true; return true; } // note: [HarmonyPrepare] is currently broken, so using specific named method approach
		//static void Cleanup() { HarmonyInstance.DEBUG = false; }

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructionEnumerable, ILGenerator ilGenerator)
		{
			var instructions = instructionEnumerable.AsList();

			// Not including a TranslationFilesMode check, since we do want to allow GenerateForMod mode for keyed translations for other languages,
			// and only want to exclude the case where activeLanguage and defaultLanguage are the same (as below).

			// Add a language check to skip keyed translations if "activeLanguage == defaultLanguage", since TranslationFilesMode.GenerateForMod now allows for this case.
			// This is necessary because the active language's keyed translations are always deleted before being recreated from the default language's keyed translations.
			// When the languages are the same, the net result is that the keyed translations are just deleted.
			// In any case, the proper way of finding the source keyed translations keys would be to scan the assembly for Translator.Translate method calls and get the string literals,
			// but even that's not guaranteed to get full coverage since it's possible that some calls to Translator.Translate use string variables rather than string literals.
			// So not going down that rabbit hole yet.
			// TODO: Go down that rabbit hole.
			// In the CleanupKeyedTranslations method, activeLanguage is a variable, but defaultLanguage is a field on an internal class (probably due to the usage of LINQ),
			// so rather than deal with that weirdness, just add a no-dependency check at the beginning of the method
			// where if the languages are same, do nothing and return early. In other words, if the languages are different, skip the early return.
			var firstInstructionLabel = instructions[0].FirstOrNewAddedLabel(ilGenerator);
			var languageCheckInstructions = TranspilerSnippets.LanguageCheckInstructions(sameLanguage: false, firstInstructionLabel);
			languageCheckInstructions.Add(new CodeInstruction(OpCodes.Ret));
			instructions.InsertRange(0, languageCheckInstructions);

			// If the keyed translations folder doesn't exist, rather than erroring out like it originally does, create the folder instead,
			// regardless of whether in Clean or GenerateForMod mode.
			TranspilerSnippets.ReplaceFolderNotExistsErrorWithFolderCreate(instructions);
			//Log.Message($"TranslationFilesCleaner_CleanupKeyedTranslations_Patch(afterfolder):\n\t{instructions.ToDebugString()}");

			// Don't add english XComment if non-placeholder translation already exists.
			// This is done by transforming the following code:
			//   foreach (XNode valueNode in keyElement.DescendantNodes())
			//   {
			//     // code for adding english XComment beginning with try block
			//     valueNode.Remove();
			//   }
			//   try
			//   {
			//     if (activeLanguage.TryGetTextFromKey(keyElement.Name.ToString(), out string translated))
			//     ...
			// to:
			//   try
			//   {
			//     bool isTranslated = activeLanguage.TryGetTextFromKey(keyElement.Name.ToString(), out string translated);
			//     foreach (XNode valueNode in keyElement.DescendantNodes())
			//     {
			//       if (!isTranslated || TranslationFilesGenerator.Mode != TranslationFilesMode.GenerateForMod)
			//       {
			//         // code for adding english XComment
			//       }
			//       valueNode.Remove();
			//     }
			//     if (isTranslated)
			//     ...

			// First find relevant instruction indices.
			var tryGetTextFromKeyCallIndex = instructions.FindIndex(
				OpCodes.Callvirt.AsInstructionPredicate(typeof(LoadedLanguage).GetMethod(nameof(LoadedLanguage.TryGetTextFromKey))));
			//Log.Message(instructions.ItemToDebugString(tryGetTextFromKeyCallIndex, "tryGetTextFromKeyCallIndex="));
			var tryStartIndex = instructions.FindLastIndex(tryGetTextFromKeyCallIndex - 1, ExceptionBlockType.BeginExceptionBlock.AsInstructionPredicate());
			//Log.Message(instructions.ItemToDebugString(tryStartIndex, "tryStartIndex="));
			var englishValueNodeLoopStartIndex = instructions.FindLastIndex(tryStartIndex - 1,
				OpCodes.Callvirt.AsInstructionPredicate(typeof(XContainer).GetMethod(nameof(XContainer.DescendantNodes)))) - 1;
			//Log.Message(instructions.ItemToDebugString(englishValueNodeLoopStartIndex, "englishValueNodeLoopStartIndex="));
			var xCommentConstructIndex = instructions.FindIndex(englishValueNodeLoopStartIndex + 1,
				OpCodes.Newobj.AsInstructionPredicate(typeof(XComment).GetConstructor(new[] { typeof(string) })));
			//Log.Message(instructions.ItemToDebugString(xCommentConstructIndex, "xCommentConstructIndex="));
			var englishCommentStartIndex = instructions.FindLastIndex(xCommentConstructIndex - 1, ExceptionBlockType.BeginExceptionBlock.AsInstructionPredicate());
			//Log.Message(instructions.ItemToDebugString(englishCommentStartIndex, "englishCommentStartIndex="));
			var afterEnglishCommentLabel = (Label)instructions[instructions.FindIndex(englishCommentStartIndex + 1, OpCodes.Leave.AsInstructionPredicate())].operand;

			// Get and remove the instructions from the try start to the TryGetTextFromKey call, adding the new isTranslated var store instruction at the end.
			var tryStartToIsTranslatedStoreInstructions = instructions.PopRange(tryStartIndex, tryGetTextFromKeyCallIndex + 1 - tryStartIndex);
			var isTranslatedVar = ilGenerator.DeclareLocal(typeof(bool));
			tryStartToIsTranslatedStoreInstructions.Add(new CodeInstruction(OpCodes.Stloc_S, isTranslatedVar));

			// The above removed instructions are replaced with a single isTranslated var load instruction.
			// This is also conveniently the first instructions after the english value node loop.
			var afterEnglishValueNodeLoopLabels = tryStartToIsTranslatedStoreInstructions[0].labels.PopAll().AddDefaultIfEmpty(() => ilGenerator.DefineLabel());
			instructions.Insert(tryStartIndex, new CodeInstruction(OpCodes.Ldloc_S, isTranslatedVar) { labels = afterEnglishValueNodeLoopLabels });

			// Insert TranslationFilesMode.GenerateForMod and isTranslated checks that if both true, skip to after the english XComment code.
			// Or in equivalent optimized CIL logic for logical AND:
			//   if (!isTranslated) goto englishCommentLabel;
			//   if (TranslationFilesGenerator.Mode == TranslationFiles.GenerateForMod) goto afterEnglishCommentLabel;
			var englishCommentLabel = instructions[englishCommentStartIndex].FirstOrNewAddedLabel(ilGenerator);
			var newCheckInstructions = new List<CodeInstruction>()
			{
				new CodeInstruction(OpCodes.Ldloc_S, isTranslatedVar),
				new CodeInstruction(OpCodes.Brfalse, englishCommentLabel),
			};
			newCheckInstructions.AddRange(TranspilerSnippets.TranslationFilesModeCheckInstructions(TranslationFilesMode.GenerateForMod, afterEnglishCommentLabel));
			instructions.InsertRange(englishCommentStartIndex, newCheckInstructions);

			// The above removed instructions (plus isTranslated var store instruction) are inserted to above the english value node loop code
			// (and the added checks and english XComment code within the loop).
			tryStartToIsTranslatedStoreInstructions[0].labels.AddRange(instructions[englishValueNodeLoopStartIndex].labels.PopAll());
			instructions.InsertRange(englishValueNodeLoopStartIndex, tryStartToIsTranslatedStoreInstructions);

			//Log.Message($"TranslationFilesCleaner_CleanupKeyedTranslations_Patch(after):\n\t{instructions.ToDebugString()}");
			return instructions;
		}
	}

	[HarmonyPatch(typeof(TranslationFilesCleaner), "CleanupBackstories")]
	static class TranslationFilesCleaner_CleanupBackstories_Patch
	{
		//static bool Prepare() { HarmonyInstance.DEBUG = true; return true; } // note: [HarmonyPrepare] is currently broken, so using specific named method approach
		//static void Cleanup() { HarmonyInstance.DEBUG = false; }

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructionEnumerable, ILGenerator ilGenerator)
		{
			var instructions = instructionEnumerable.AsList();

			// Add a TranslationFilesMode check to skip backstory translations in GenerateForMod mode.
			// This is necessary because mods (except the Core mod) are not allowed to define backstories the vanilla way.
			// (Instead, there are various other mod implementations that typically involve defining backstories via a custom Def type.)
			var firstInstructionLabel = instructions[0].FirstOrNewAddedLabel(ilGenerator);
			var translationFilesModeCheckInstructions = TranspilerSnippets.TranslationFilesModeCheckInstructions(TranslationFilesMode.Clean, firstInstructionLabel);
			translationFilesModeCheckInstructions.Add(new CodeInstruction(OpCodes.Ret));
			instructions.InsertRange(0, translationFilesModeCheckInstructions);

			return instructions;
		}
	}

	// TranslationFilesCleaner.'<GetLanguageCoreModFolderPath>m__C' (internal lambda method within TranslationFilesCleaner.GetLanguageCoreModFolderPath)
	[HarmonyPatch(typeof(TranslationFilesCleaner), "<GetLanguageCoreModFolderPath>m__C")]
	static class TranslationFilesCleaner_GetLanguageCoreModFolderPath_Patch
	{
		//static bool Prepare() { HarmonyInstance.DEBUG = true; return true; } // note: [HarmonyPrepare] is currently broken, so using specific named method approach
		//static void Cleanup() { HarmonyInstance.DEBUG = false; }

		[HarmonyPrefix]
		static bool Prefix(ModContentPack x, ref bool __result)
		{
			if (TranslationFilesGenerator.Mode == TranslationFilesMode.GenerateForMod)
			{
				__result = x == TranslationFilesGenerator.ModContentPack;
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(TranslationFilesCleaner), "CleanupDefInjections")]
	static class TranslationFilesCleaner_CleanupDefInjections_Patch
	{
		//static bool Prepare() { HarmonyInstance.DEBUG = true; return true; } // note: [HarmonyPrepare] is currently broken, so using specific named method approach
		//static void Cleanup() { HarmonyInstance.DEBUG = false; }

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructionEnumerable)
		{
			var instructions = instructionEnumerable.AsList();

			// If the def-injected folder doesn't exist, rather than erroring out like it originally does, create the folder instead,
			// regardless of whether in Clean or GenerateForMod mode.
			TranspilerSnippets.ReplaceFolderNotExistsErrorWithFolderCreate(instructions);

			// Delete any non-empty subfolders before creating the new def injection files (and after deleting the old files).
			var allDefTypesWithDatabaseCallIndex = instructions.FindIndex(
				OpCodes.Call.AsInstructionPredicate(typeof(GenDefDatabase).GetMethod(nameof(GenDefDatabase.AllDefTypesWithDatabases))));
			instructions.InsertRange(allDefTypesWithDatabaseCallIndex, new[]
			{
				new CodeInstruction(OpCodes.Ldloc_3), // defInjectionsFolder
				new CodeInstruction(OpCodes.Call, typeof(TranslationFilesCleaner_CleanupDefInjections_Patch).GetMethod(nameof(DeleteEmptySubfolders), AccessTools.all)),
			});

			return instructions;
		}

		static void DeleteEmptySubfolders(DirectoryInfo dirInfo)
		{
			foreach (DirectoryInfo subfolder in dirInfo.GetDirectories("*", SearchOption.AllDirectories))
			{
				try
				{
					if (subfolder.GetFileSystemInfos().Length == 0)
						subfolder.Delete();
				}
				catch (Exception ex)
				{
					Log.Error("Could not delete " + subfolder.Name + ": " + ex.ToString());
				}
			}
		}
	}

	[HarmonyPatch(typeof(DefInjectionUtility), "ForEachPossibleDefInjection")]
	static class DefInjectionUtility_ForEachPossibleDefInjection_Patch
	{
		//static bool Prepare() { HarmonyInstance.DEBUG = true; return true; } // note: [HarmonyPrepare] is currently broken, so using specific named method approach
		//static void Cleanup() { HarmonyInstance.DEBUG = false; }

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructionEnumerable)
		{
			var instructions = instructionEnumerable.AsList();

			// Redirect call to GenDefDatabase.GetAllDefsInDatabaseForDef to our own AllDefsInModOfType.
			var getAllDefsInDatabaseForDefCallInstruction = instructions.Find(
				OpCodes.Call.AsInstructionPredicate(typeof(GenDefDatabase).GetMethod(nameof(GenDefDatabase.GetAllDefsInDatabaseForDef))));
			getAllDefsInDatabaseForDefCallInstruction.operand = typeof(DefInjectionUtility_ForEachPossibleDefInjection_Patch).GetMethod(nameof(AllDefsOfType), AccessTools.all);

			return instructions;
		}

		static IEnumerable<Def> AllDefsOfType(Type defType)
		{
			var mod = TranslationFilesGenerator.ModContentPack;
			if (mod != null)
				return mod.AllDefs.Where(def => def.GetType() == defType);
			return GenDefDatabase.GetAllDefsInDatabaseForDef(defType);
		}
	}

	[HarmonyPatch]
	static class LoadedLanguage_FolderPaths_Patch
	{
		//static bool Prepare() { HarmonyInstance.DEBUG = true; return true; } // note: [HarmonyPrepare] is currently broken, so using specific named method approach
		//static void Cleanup() { HarmonyInstance.DEBUG = false; }

		[HarmonyTargetMethod]
		static MethodInfo CalculateMethod(HarmonyInstance harmony)
		{
			// LoadedLanguage.'<>c__Iterator0'.MoveNext
			// (MoveNext method of internal iterator class used within LoadedLanguage.FolderName's getter)
			return typeof(LoadedLanguage).GetNestedType("<>c__Iterator0", AccessTools.all).GetMethod("MoveNext");
		}

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructionEnumerable)
		{
			var instructions = instructionEnumerable.AsList();

			var getRunningModsCallInstruction = instructions.Find(
				OpCodes.Call.AsInstructionPredicate(typeof(LoadedModManager).GetProperty(nameof(LoadedModManager.RunningMods)).GetGetMethod()));
			getRunningModsCallInstruction.operand = typeof(LoadedLanguage_FolderPaths_Patch).GetMethod(nameof(GetMods), AccessTools.all);

			return instructions;
		}

		static IEnumerable<ModContentPack> GetMods()
		{
			var mods = LoadedModManager.RunningMods;
			if (TranslationFilesGenerator.Mode == TranslationFilesMode.GenerateForMod)
			{
				// Filter for only this TranslationFileGenerator mod and the target mod, while retaining order.
				var translationFilesGeneratorMod = LoadedModManager.GetMod<TranslationFilesGeneratorMod>().Content;
				mods = mods.Where(mod => mod == translationFilesGeneratorMod || mod == TranslationFilesGenerator.ModContentPack);
			}
			//Log.Message($"LoadedLanguage_FolderPaths_Patch.GetMods:\n\t{mods.Join("\n\t")}");
			return mods;
		}
	}

	[HarmonyPatch(typeof(TranslationFilesCleaner), "CleanupDefInjectionsForDefType")]
	static class TranslationFilesCleaner_CleanupDefInjectionsForDefType_Patch
	{
		//static bool Prepare() { HarmonyInstance.DEBUG = true; return true; } // note: [HarmonyPrepare] is currently broken, so using specific named method approach
		//static void Cleanup() { HarmonyInstance.DEBUG = false; }

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructionEnumerable, ILGenerator ilGenerator)
		{
			var instructions = instructionEnumerable.DeoptimizeLocalVarInstructions(ilGenerator).AsList();
			//Log.Message($"TranslationFilesCleaner_CleanupDefInjectionsForDefType_Patch(before):\n{instructions.ToDebugString()}");

			// Need a DefInjectionPackage var for ModifyInjection.
			// Initialize it to GetDefInjectionPackage(activeLanguage, defType).
			var defInjectionPackageVar = ilGenerator.DeclareLocal(typeof(DefInjectionPackage));
			var activeLanguageStoreIndex = instructions.FindIndex(OpCodes.Stloc_S.AsInstructionPredicate().LocalBuilder(typeof(LoadedLanguage)));
			var defInjectionVarInitInstructions = new[]
			{
				instructions[activeLanguageStoreIndex].Clone(OpCodes.Ldloc_S),
				new CodeInstruction(OpCodes.Ldarg_0), // defType
				new CodeInstruction(OpCodes.Call, typeof(TranslationFilesCleaner_CleanupDefInjectionsForDefType_Patch).GetMethod(nameof(GetDefInjectionPackage), AccessTools.all)),
				new CodeInstruction(OpCodes.Stloc_S, defInjectionPackageVar),
			};
			instructions.InsertRange(activeLanguageStoreIndex + 1, defInjectionVarInitInstructions);

			// Also need a FileInfo translationFile var for ModifyInjection.
			var translationFileVar = ilGenerator.DeclareLocal(typeof(FileInfo));
			// Annoyingly, fileName is a field stored in an object of an internal type, rather than a local var.
			var xDocumentVarStoreIndex = instructions.FindIndex(defInjectionVarInitInstructions.Length + 1, OpCodes.Stloc_S.AsInstructionPredicate().LocalBuilder(typeof(XDocument)));
			//Log.Message(instructions.ItemToDebugString(xDocumentVarStoreIndex, "xDocumentVarStoreIndex="));
			var fileNameFieldStoreIndex = instructions.FindLastIndex(xDocumentVarStoreIndex - 1,
				OpCodes.Stfld.AsInstructionPredicate().Operand<FieldInfo>(field => field.FieldType == typeof(string) && field.Name == "fileName"));
			//Log.Message(instructions.ItemToDebugString(fileNameFieldStoreIndex, "fileNameFieldStoreIndex="));
			var fileNameHolderLoadIndex = instructions.FindLastIndex(fileNameFieldStoreIndex - 1, OpCodes.Ldloc_S.AsInstructionPredicate().LocalBuilder(localVar =>
				localVar.LocalType.GetField("fileName", AccessTools.all) is FieldInfo field && field.FieldType == typeof(string)));
			//Log.Message(instructions.ItemToDebugString(fileNameHolderLoadIndex, "fileNameHolderLoadIndex="));

			// Initialize translationFile var to new FileInfo(Path.Combine(Path.Combine(defInjectionsFolderPath, GenTypes.GetTypeNameWithoutIgnoredNamespaces(defType)), fileName)).
			var translationFileVarInitInstructions = new[]
			{
				new CodeInstruction(OpCodes.Ldarg_1), // defInjectionsFolderPath
				new CodeInstruction(OpCodes.Ldarg_0), // defType
				new CodeInstruction(OpCodes.Call, typeof(GenTypes).GetMethod(nameof(GenTypes.GetTypeNameWithoutIgnoredNamespaces))),
				new CodeInstruction(OpCodes.Call, typeof(Path).GetMethod(nameof(Path.Combine))),
				instructions[fileNameHolderLoadIndex].Clone(),
				instructions[fileNameFieldStoreIndex].Clone(OpCodes.Ldfld),
				new CodeInstruction(OpCodes.Call, typeof(Path).GetMethod(nameof(Path.Combine))),
				new CodeInstruction(OpCodes.Newobj, typeof(FileInfo).GetConstructor(new[] { typeof(string) })),
				new CodeInstruction(OpCodes.Stloc_S, translationFileVar),
			};
			instructions.InsertRange(xDocumentVarStoreIndex + 1, translationFileVarInitInstructions);
			//Log.Message($"TranslationFilesCleaner_CleanupDefInjectionsForDefType_Patch(aftervarinit):\n{instructions.ToDebugString()}");

			// Skip first XComment since it's just the dummy <!--NEWLINE-->.
			var searchStartIndex = xDocumentVarStoreIndex + 1 + translationFileVarInitInstructions.Length + 1;
			searchStartIndex = instructions.FindIndex(searchStartIndex, OpCodes.Newobj.AsInstructionPredicate(typeof(XComment).GetConstructor(new[] { typeof(string) }))) + 1;
			//Log.Message(instructions.ItemToDebugString(searchStartIndex, "searchStartIndex.1="));

			// Case where the injection site is a list, and either does NOT allow a full list injection
			// (i.e. does NOT have a field attributed with TranslationCanChangeCount) or has at least one existing injection in the list.
			// If activeLanguage == defaultLanguage, we're going to throw away existing injections, so unless the injection site doesn't allow a full list injection,
			// this case will be skipped then. Specifically, if the languages are the same, skip the loop where the hasExistingInjection flag can be set.
			var getEnglishListCallIndex = instructions.FindIndex(searchStartIndex,
				OpCodes.Call.AsInstructionPredicate(typeof(TranslationFilesCleaner).GetMethod("GetEnglishList", AccessTools.all)));
			//Log.Message(instructions.ItemToDebugString(getEnglishListCallIndex, "getEnglishListCallIndex="));
			var hasExistingInjectionVarStoreIndex = instructions.FindIndex(getEnglishListCallIndex + 1,
				OpCodes.Stloc_S.AsInstructionPredicate().LocalBuilder(typeof(bool)));
			//Log.Message(instructions.ItemToDebugString(hasExistingInjectionVarStoreIndex, "hasExistingInjectionVarStoreIndex="));
			var hasExistingInjectionVarLoadIndex = instructions.FindIndex(hasExistingInjectionVarStoreIndex + 1,
				OpCodes.Ldloc_S.AsInstructionPredicate(instructions[hasExistingInjectionVarStoreIndex].operand));
			//Log.Message(instructions.ItemToDebugString(hasExistingInjectionVarLoadIndex, "hasExistingInjectionVarLoadIndex="));
			var partialListInjectionLabel = instructions[hasExistingInjectionVarLoadIndex].FirstOrNewAddedLabel(ilGenerator);
			var languageCheckInstructions = TranspilerSnippets.LanguageCheckInstructions(sameLanguage: true, partialListInjectionLabel);
			instructions.InsertRange(hasExistingInjectionVarStoreIndex + 1, languageCheckInstructions);

			searchStartIndex = ModifyInjection(instructions, ilGenerator, defInjectionPackageVar, translationFileVar, searchStartIndex,
				skipSameLanguageInjection: true, fullListInsertion: false);
			//Log.Message(instructions.ItemToDebugString(searchStartIndex, "searchStartIndex.2="));

			// Case where the injection site is a list, and both allows a full list injection (i.e. does have a field attributed with TranslationCanChangeCount,
			// currently only used for RulePack/Rule_File fields) and does NOT have any existing injections in the list.
			searchStartIndex = ModifyInjection(instructions, ilGenerator, defInjectionPackageVar, translationFileVar, searchStartIndex,
				skipSameLanguageInjection: false, fullListInsertion: true);
			//Log.Message(instructions.ItemToDebugString(searchStartIndex, "searchStartIndex.3="));

			// Case where the injection site is NOT a list.
			searchStartIndex = ModifyInjection(instructions, ilGenerator, defInjectionPackageVar, translationFileVar, searchStartIndex,
				skipSameLanguageInjection: false, fullListInsertion: false);
			//Log.Message(instructions.ItemToDebugString(searchStartIndex, "searchStartIndex.4="));

			// Ensure Def types use GenTypes.GetTypeNameWithoutIgnoredNamespaces to get their folder name.
			var saveXMLDocumentCallIndex = instructions.FindLastIndex(
				OpCodes.Call.AsInstructionPredicate(typeof(TranslationFilesCleaner).GetMethod("SaveXMLDocumentWithProcessedNewlineTags", AccessTools.all)));
			var typeGetNameCallIndex = instructions.FindLastIndex(saveXMLDocumentCallIndex - 1,
				OpCodes.Callvirt.AsInstructionPredicate(typeof(MemberInfo).GetProperty(nameof(MemberInfo.Name)).GetGetMethod()));
			instructions[typeGetNameCallIndex].SetTo(OpCodes.Call, typeof(GenTypes).GetMethod(nameof(GenTypes.GetTypeNameWithoutIgnoredNamespaces)));

			// TODO: Add mod setting to strip trailing whitespace on empty lines.

			//Log.Message($"TranslationFilesCleaner_CleanupDefInjectionsForDefType_Patch(after):\n{instructions.ToDebugString()}");
			return instructions.ReoptimizeLocalVarInstructions();
		}

		static DefInjectionPackage GetDefInjectionPackage(LoadedLanguage language, Type defType)
		{
			var defInjectionPackage = language.defInjections.Where(defInjection => defInjection.defType == defType).FirstOrDefault();
			if (defInjectionPackage == null)
			{
				defInjectionPackage = new DefInjectionPackage(defType);
				language.defInjections.Add(defInjectionPackage);
			}
			return defInjectionPackage;
		}

		static int ModifyInjection(List<CodeInstruction> instructions, ILGenerator ilGenerator, LocalBuilder defInjectionPackageVar, LocalBuilder translationFileVar,
			int searchStartIndex, bool skipSameLanguageInjection, bool fullListInsertion)
		{
			var xCommentConstructIndex = instructions.FindIndex(searchStartIndex, OpCodes.Newobj.AsInstructionPredicate(typeof(XComment).GetConstructor(new[] { typeof(string) })));
			//Log.Message(instructions.ItemToDebugString(xCommentConstructIndex, "xCommentConstructIndex="));
			var tryStartIndex = instructions.FindLastIndex(xCommentConstructIndex - 1, ExceptionBlockType.BeginExceptionBlock.AsInstructionPredicate());
			//Log.Message(instructions.ItemToDebugString(tryStartIndex, "tryStartIndex="));
			var afterTryEndLabel = (Label)instructions[instructions.FindIndex(xCommentConstructIndex + 1, OpCodes.Leave.AsInstructionPredicate())].operand;
			//Log.Message(instructions.ItemToDebugString(instructions.FindIndex(xCommentConstructIndex + 1, afterTryEndLabel.AsInstructionPredicate()), "afterTryEndIndex="));
			var defInjectionVarStoreIndex = instructions.FindLastIndex(tryStartIndex - 1,
				OpCodes.Stloc_S.AsInstructionPredicate().LocalBuilder(typeof(DefInjectionPackage.DefInjection)));
			//Log.Message(instructions.ItemToDebugString(defInjectionVarStoreIndex, "defInjectionVarStoreIndex="));

			// All the following new logic which is going to be injected right before the try block containing the english XComment.
			var newInstructions = new List<CodeInstruction>();
			var languageNotSameLabel = ilGenerator.DefineLabel();

			if (!skipSameLanguageInjection)
			{
				// If activeLanguage == defaultLanguage, we're going to effectively skip the english XComment,
				// and add a new def-injection from the possibleDefInjection-derived englishStr/englishList,
				// so that the later GetDefInjectableFieldNode call will see and use that new injection.
				// If activeLanguage != defaultLanguage, skip to that case (see below).
				newInstructions.AddRange(TranspilerSnippets.LanguageCheckInstructions(sameLanguage: false, languageNotSameLabel));

				// If an injection for the normalizedPath exists, remove it.
				var defInjectionPackageInjectionsInstructions = DefInjectionPackageInjectionsInstructions(defInjectionPackageVar);
				var normalizedPathLoadInstructions = NormalizedPathLoadInstructions(instructions, xCommentConstructIndex - 1);
				newInstructions.AddRange(defInjectionPackageInjectionsInstructions);
				newInstructions.AddRange(normalizedPathLoadInstructions);
				newInstructions.AddRange(new[]
				{
					new CodeInstruction(OpCodes.Call, typeof(Dictionary<string, DefInjectionPackage.DefInjection>).GetMethod(nameof(Dictionary<int, int>.Remove))),
					new CodeInstruction(OpCodes.Pop),
				});
				// Also remove any existing normalizedPath.<x> injections if full list injection.
				if (fullListInsertion)
				{
					newInstructions.AddRange(defInjectionPackageInjectionsInstructions);
					newInstructions.AddRange(normalizedPathLoadInstructions);
					newInstructions.AddRange(new[]
					{
						new CodeInstruction(OpCodes.Call,
							typeof(TranslationFilesCleaner_CleanupDefInjectionsForDefType_Patch).GetMethod(nameof(RemoveAllInjectionsWithIndexedPath), AccessTools.all)),
						new CodeInstruction(OpCodes.Pop),
					});
				}

				// Get defInjectionPackage, translationFile, and normalizedPath.
				newInstructions.AddRange(new[]
				{
					new CodeInstruction(OpCodes.Ldloc_S, defInjectionPackageVar),
					new CodeInstruction(OpCodes.Ldloc_S, translationFileVar),
				});
				newInstructions.AddRange(normalizedPathLoadInstructions);

				if (fullListInsertion)
				{
					// Get fullListInjection list, then call defInjectionPackage.TryAddFullListInjection(translationFile, normalizedPath, englishList.AsList(), null).
					var englishListVarLoadIndex = instructions.FindLastIndex(xCommentConstructIndex - 1,
						OpCodes.Ldloc_S.AsInstructionPredicate().LocalBuilder(typeof(IEnumerable<string>), useIsAssignableFrom: true));
					//Log.Message(instructions.ItemToDebugString(englishListVarLoadIndex, "englishListVarLoadIndex="));
					newInstructions.AddRange(new[]
					{
						instructions[englishListVarLoadIndex].Clone(),
						new CodeInstruction(OpCodes.Call, typeof(LanguageExtensions).GetMethod(nameof(LanguageExtensions.AsList), AccessTools.all).MakeGenericMethod(typeof(string))),
						new CodeInstruction(OpCodes.Ldnull), // comments
						new CodeInstruction(OpCodes.Call, typeof(DefInjectionPackage).GetMethod("TryAddFullListInjection", AccessTools.all)),
					});
				}
				else
				{
					// Get injection, then call defInjectionPackage.TryAddInjection(translationFile, normalizedPath, englishStr).
					var englishStrVarLoadIndex = instructions.FindLastIndex(xCommentConstructIndex - 1, OpCodes.Ldloc_S.AsInstructionPredicate().LocalBuilder(typeof(string)));
					//Log.Message(instructions.ItemToDebugString(englishStrVarLoadIndex, "englishStrVarLoadIndex="));
					newInstructions.AddRange(new[]
					{
						instructions[englishStrVarLoadIndex].Clone(),
						new CodeInstruction(OpCodes.Call, typeof(DefInjectionPackage).GetMethod("TryAddInjection", AccessTools.all)),
					});
				}

				// Get the newly added DefInjection.
				newInstructions.AddRange(new[]
				{
					new CodeInstruction(OpCodes.Ldloc_S, defInjectionPackageVar),
					new CodeInstruction(OpCodes.Ldfld, typeof(DefInjectionPackage).GetField(nameof(DefInjectionPackage.injections))),
				});
				newInstructions.AddRange(normalizedPathLoadInstructions);
				newInstructions.AddRange(new[]
				{
					new CodeInstruction(OpCodes.Call, typeof(Dictionary<string, DefInjectionPackage.DefInjection>).GetProperty("Item").GetGetMethod()),
				});

				// Then assign it to the defInjection var.
				newInstructions.Add(instructions[defInjectionVarStoreIndex].Clone());

				// Finally branch to after the try-catch block.
				newInstructions.Add(new CodeInstruction(OpCodes.Br, afterTryEndLabel));
			}

			// In the activeLanguage != defaultLanguage case, don't add the english XComment if a non-placeholder injection already exists.
			var tryStartLabel = instructions[tryStartIndex].FirstOrNewAddedLabel(ilGenerator);
			var defInjectionVar = instructions[defInjectionVarStoreIndex].operand;
			newInstructions.AddRange(new[]
			{
				// If defInjection == null, still need the english XComment.
				new CodeInstruction(OpCodes.Ldloc_S, defInjectionVar) { labels = { languageNotSameLabel } },
				new CodeInstruction(OpCodes.Brfalse, tryStartLabel),

				// If defInjection.isPlaceholder, still need the english XComment.
				new CodeInstruction(OpCodes.Ldloc_S, defInjectionVar),
				new CodeInstruction(OpCodes.Ldfld, typeof(DefInjectionPackage.DefInjection).GetField(nameof(DefInjectionPackage.DefInjection.isPlaceholder))),
				new CodeInstruction(OpCodes.Brtrue, tryStartLabel),

				new CodeInstruction(OpCodes.Br, afterTryEndLabel),
			});

			// Finally insert all these new instructions before the try block containing the XComment.
			instructions.InsertRange(tryStartIndex, newInstructions);

			var afterTryEndIndex = instructions.FindIndex(xCommentConstructIndex + 1, afterTryEndLabel.AsInstructionPredicate());
			//Log.Message($"TranslationFilesCleaner_CleanupDefInjectionsForDefType_Patch:\n\t{instructions.RangeToDebugString(tryStartIndex, afterTryEndIndex - tryStartIndex + 1)}");
			return afterTryEndIndex;
		}

		static CodeInstruction[] DefInjectionPackageInjectionsInstructions(LocalBuilder defInjectionPackageVar)
		{
			return new[]
			{
				new CodeInstruction(OpCodes.Ldloc_S, defInjectionPackageVar),
				new CodeInstruction(OpCodes.Ldfld, typeof(DefInjectionPackage).GetField(nameof(DefInjectionPackage.injections))),
			};
		}

		static CodeInstruction[] NormalizedPathLoadInstructions(List<CodeInstruction> instructions, int searchEndIndex)
		{
			var possibleDefInjectionType = typeof(TranslationFilesCleaner).GetNestedType("PossibleDefInjection", AccessTools.all);
			var possibleDefInjectionLoadIndex = instructions.FindLastIndex(searchEndIndex, OpCodes.Ldloc_S.AsInstructionPredicate().LocalBuilder(possibleDefInjectionType));
			//Log.Message(instructions.ItemToDebugString(possibleDefInjectionLoadIndex, "possibleDefInjectionLoadIndex="));
			return new[]
			{
				instructions[possibleDefInjectionLoadIndex].Clone(),
				new CodeInstruction(OpCodes.Ldfld, possibleDefInjectionType.GetField("normalizedPath")),
			};
		}

		static int RemoveAllInjectionsWithIndexedPath(Dictionary<string, DefInjectionPackage.DefInjection> injections, string key)
		{
			// Same lambda logic as in DefInjectionPackage.CheckErrors, to prevent that method from returning false (and recording an error).
			return injections.RemoveAll(pairPredicate: pair => !pair.Value.IsFullListInjection && pair.Key.StartsWith(key + "."));
		}
	}

	// TODO: Patch SaveXMLDocumentWithProcessedNewlineTags with options to add EOF newline and prevent UTF BOM.

	// Uncomment following line to log all PossibleDefInjection's to output_log.txt.
	//[HarmonyPatch]
	static class TranslationFilesCleaner_CleanupDefInjectionsForDefType_Debug_Patch
	{
		//static bool Prepare() { HarmonyInstance.DEBUG = true; return true; } // note: [HarmonyPrepare] is currently broken, so using specific named method approach
		//static void Cleanup() { HarmonyInstance.DEBUG = false; }

		[HarmonyTargetMethod]
		static MethodInfo CalculateMethod(HarmonyInstance harmony)
		{
			// TranslationFilesCleaner.'<CleanupDefInjectionsForDefType>c__AnonStorey2'.'<>m__1'
			// (internal delegate method in DefInjectionUtility.ForEachPossibleDefInjection call within TranslationFilesCleaner.CleanupDefInjectionsForDefType)
			return typeof(TranslationFilesCleaner).GetNestedType("<CleanupDefInjectionsForDefType>c__AnonStorey2", AccessTools.all).GetMethod("<>m__1", AccessTools.all);
		}

		[HarmonyPostfix]
		static void Postfix(bool translationAllowed, System.Collections.IList ___possibleDefInjections)
		{
			if (translationAllowed)
			{
				StringBuilder sb = new StringBuilder("PossibleDefInjection: translationAllowed=true");
				// Have to use non-generics because DefInjectionUtility.PossibleDefInjection is private.
				object possibleDefInjection = ___possibleDefInjections[___possibleDefInjections.Count - 1];
				foreach (FieldInfo field in possibleDefInjection.GetType().GetFields())
				{
					sb.Append($", {field.Name}={field.GetValue(possibleDefInjection) ?? "<null>"}");
				}
				Debug.Log(sb.ToString());
			} else
			{
				//Debug.Log("PossibleDefInjection: translationAllowed=false");
			}
		}
	}
}
