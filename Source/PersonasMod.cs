using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Ustas.RimAI.Core.Handshake;
using Verse;
using Ustas.RimAI.Core.Diagnostics;
using Ustas.RimAI.Communication.Personas.UI;

namespace Ustas.RimAI.Communication.Personas
{
    public class PersonasMod : Mod
    {
        public const string HandshakeModuleVersion = "1.0.0";
        public static PersonasSettings Settings;

        private Vector2 mainScrollPosition = Vector2.zero;

        private static bool _isRimPsycheLoaded = false;

        internal static bool IsRimPsycheLoaded => _isRimPsycheLoaded;

        public PersonasMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<PersonasSettings>();
            _isRimPsycheLoaded = AccessTools.TypeByName("Maux36.RimPsyche.CompPsyche") != null;
            RimAiHandshake.TryActivate(
                RimAiHandshakeDescriptor.Current(
                    RimAiModuleIds.Personas,
                    HandshakeModuleVersion,
                    isOptional: true),
                PersonasComposition.Current.Start);
            // DirectorStartup + Scriban surface are scheduled from PersonasComposition.Start.
        }

        public override string SettingsCategory() => Content?.Name ?? "RimAI.Communication.Personas";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Ustas.RimAI.Core.Modules.RimAISettingsNavigation.Open("communication", "personas");
            float contentHeight = 1400f;
            Rect viewRect = new Rect(0, 0, inRect.width - 16f, contentHeight);

            Widgets.BeginScrollView(inRect, ref mainScrollPosition, viewRect);

            Listing_Standard list = new Listing_Standard();
            // Verse wraps a Listing into a second column, off the visible view, as soon as
            // content passes the rect height, and CurHeight then reports that new column.
            // A scrolling settings page never wants that; see validate_scrollable_listings.
            list.maxOneColumn = true;
            list.Begin(viewRect);

            Rect titleRect = list.GetRect(30f);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect.LeftPart(0.7f), "RPD_Settings_Title".Translate());
            Text.Font = GameFont.Small;

            Rect libraryBtnRect = titleRect.RightPart(0.25f);
            if (Widgets.ButtonText(libraryBtnRect, "RPD_Settings_OpenLibrary".Translate()))
            {
                Find.WindowStack.Add(new Window_LibraryManager());
            }
            TooltipHandler.TipRegion(libraryBtnRect, "RPD_Tip_OpenLibrary".Translate());

            list.Gap(8f);

            list.CheckboxLabeled("RPD_Settings_ShowMainButton".Translate(), ref Settings.ShowMainButton, "RPD_Settings_ShowMainButtonTip".Translate());

            list.CheckboxLabeled("RPD_Filter_DirectorNotes".Translate(), ref Settings.Context.Inc_DirectorNotes, "RPD_Tip_NotesDesc".Translate());
            list.CheckboxLabeled("RPD_Settings_EnableEvolve".Translate(), ref Settings.enableEvolveFeature, "RPD_Settings_EnableEvolveTip".Translate());
            list.CheckboxLabeled("RPD_Setting_DebugLog".Translate(), ref Settings.EnableDebugLog, "RPD_Setting_DebugLogDesc".Translate());


            list.GapLine();

            ContextFilterDrawer.Draw(list, Settings.Context, _isRimPsycheLoaded);

            list.GapLine();

            PersonaAutomationSettingsSection.Draw(list, Settings.Automation);

            list.GapLine();

            list.Label("<b>" + "RPD_Setting_RimTalkIntegration".Translate() + "</b>");
            list.Label("RPD_Setting_RimTalkIntegrationDesc".Translate());

            string noneLabel = "RPD_Setting_NoneInternal".Translate();

            List<string> rtPresets = new List<string> { noneLabel };
            try
            {
                var presets = Ustas.RimAI.Communication.API.RimTalkPromptAPI.GetAllPresets();
                if (presets != null) rtPresets.AddRange(presets.Select(p => p.Name));
            }
            // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY - preset list could not be read, so the dropdown is empty
            catch (System.Exception ex)
            {
                RimAiLog.WarningOnce(RimAiLogCategory.Personas, "[RimAI.Personas] preset list could not be read, so the dropdown is empty: " + ex, 1511129093);
            }

            Rect row1 = list.GetRect(24f);
            Widgets.Label(row1.LeftPart(0.4f), "RPD_Setting_ForSingleGen".Translate());
            string currentSingle = string.IsNullOrEmpty(Settings.rimTalkPreset_Single) ? noneLabel : Settings.rimTalkPreset_Single;
            if (Widgets.ButtonText(row1.RightPart(0.6f), currentSingle))
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption>();
                foreach (var pName in rtPresets)
                {
                    string val = pName == noneLabel ? "" : pName;
                    opts.Add(new FloatMenuOption(pName, () => Settings.rimTalkPreset_Single = val));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            list.Gap(5f);

            Rect row2 = list.GetRect(24f);
            Widgets.Label(row2.LeftPart(0.4f), "RPD_Setting_ForEvolve".Translate());

            string currentEvolve = string.IsNullOrEmpty(Settings.rimTalkPreset_Evolve) ? noneLabel : Settings.rimTalkPreset_Evolve;

            if (Widgets.ButtonText(row2.RightPart(0.6f), currentEvolve))
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption>();
                foreach (var pName in rtPresets)
                {
                    string val = pName == noneLabel ? "" : pName;
                    opts.Add(new FloatMenuOption(pName, () => Settings.rimTalkPreset_Evolve = val));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }

            list.GapLine();

            DrawPromptSection(list, viewRect.width);

            list.End();
            Widgets.EndScrollView();

            Settings.Write();
        }

        private void DrawPromptSection(Listing_Standard list, float width)
        {
            var settings = Settings;
            if (settings.presets == null || settings.presets.Count < 4) settings.InitPresets();

            var currentPreset = settings.presets[settings.selectedPresetIndex];

            Rect headerRect = list.GetRect(24f);
            Widgets.Label(headerRect.LeftPart(0.7f), "RPD_Prompt_Label".Translate());
            if (Widgets.ButtonText(headerRect.RightPart(0.3f), "RPD_Button_Reset".Translate()))
            {
                if (settings.selectedPresetIndex == 0) { currentPreset.label = "Standard (3 Options)"; currentPreset.text = PersonasSettings.DefaultPrompt_Standard; }
                else if (settings.selectedPresetIndex == 1) { currentPreset.label = "Simple (One Shot)"; currentPreset.text = PersonasSettings.DefaultPrompt_Simple; }
                else if (settings.selectedPresetIndex == 2) { currentPreset.label = "Strict (Backstory)"; currentPreset.text = PersonasSettings.DefaultPrompt_Strict; }
                else if (settings.selectedPresetIndex == 3) { currentPreset.label = "Evolution (Update Only)"; currentPreset.text = PersonasSettings.DefaultPrompt_Evolve; }
            }

            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            list.Label("RPD_Label_JsonTip".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            list.Gap(2f);

            Rect ctrlRect = list.GetRect(26f);
            int userChar = currentPreset.text?.Length ?? 0;
            int hiddenChar = PersonasSettings.HiddenTechnicalPrompt_Single.Length;
            int estTokens = (int)((userChar + hiddenChar) / 2.5f);

            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(ctrlRect.LeftPart(0.4f), "RPD_Label_TokenEst".Translate(estTokens));

            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleLeft;

            Rect rightPart = ctrlRect.RightPart(0.6f);
            float dropdownWidth = 100f;
            float labelWidth = rightPart.width - dropdownWidth - 5f;

            Rect labelRect = new Rect(rightPart.x, rightPart.y, labelWidth, 24f);
            string newLabel = Widgets.TextField(labelRect, currentPreset.label);
            if (newLabel != currentPreset.label) currentPreset.label = newLabel;

            Rect dropdownRect = new Rect(labelRect.xMax + 5f, rightPart.y, dropdownWidth, 24f);
            string slotLabel = "RPD_Setting_Slot".Translate(settings.selectedPresetIndex + 1) + " ▼";

            if (Widgets.ButtonText(dropdownRect, slotLabel))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                for (int i = 0; i < settings.presets.Count; i++)
                {
                    int index = i;
                    string name = settings.presets[i].label;
                    if (string.IsNullOrEmpty(name)) name = "RPD_Setting_Slot".Translate(i + 1);
                    options.Add(new FloatMenuOption($"{i + 1}. {name}", () => settings.selectedPresetIndex = index));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            Text.Anchor = TextAnchor.UpperLeft;
            list.Gap(5f);

            Rect outRect = list.GetRect(600f);
            // Non-obvious edge case — read carefully before changing. (ScrollView ScrollView TextArea)
            currentPreset.text = Widgets.TextArea(outRect, currentPreset.text);
        }
    }
}