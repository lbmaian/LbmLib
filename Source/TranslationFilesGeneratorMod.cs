using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace TranslationFilesGenerator
{
	public class TranslationFilesGeneratorSettings : ModSettings
	{
		public override void ExposeData()
		{
			// TODO
		}
	}

	public class TranslationFilesGeneratorMod : Mod
	{
		TranslationFilesGeneratorSettings settings;

		public TranslationFilesGeneratorMod(ModContentPack content) : base(content)
		{
			settings = GetSettings<TranslationFilesGeneratorSettings>();
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			// TODO
		}

		public override string SettingsCategory()
		{
			return "TranslationFilesGeneratorModName".Translate();
		}
	}
}
