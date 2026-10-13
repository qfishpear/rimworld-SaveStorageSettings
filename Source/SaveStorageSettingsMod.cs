using UnityEngine;
using Verse;

namespace SaveStorageSettings
{
    public class SaveStorageSettingsSettings : ModSettings
    {
        public bool skipRecipeResearchCheck;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref skipRecipeResearchCheck, "skipRecipeResearchCheck", false);
            base.ExposeData();
        }
    }

    public class SaveStorageSettingsMod : Mod
    {
        public static SaveStorageSettingsSettings Settings;

        public SaveStorageSettingsMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<SaveStorageSettingsSettings>();
        }

        public override string SettingsCategory()
        {
            return "SaveStorageSettings.SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled(
                "SaveStorageSettings.SkipRecipeResearchCheck".Translate(),
                ref Settings.skipRecipeResearchCheck,
                "SaveStorageSettings.SkipRecipeResearchCheckDesc".Translate(),
                0f,
                1f);
            listing.End();
        }
    }
}
