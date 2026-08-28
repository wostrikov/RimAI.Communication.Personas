using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Ustas.RimAI.Core.Handshake;
using Verse;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Communication.Personas
{
    public class PersonasMod : Mod
    {
        public const string HandshakeModuleVersion = "1.0.0";
        public static PersonasSettings Settings;

        private Vector2 mainScrollPosition = Vector2.zero;

        private static bool _isRimPsycheLoaded = false;

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
            float contentHeight = 1200f;
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

            DrawContextFilterSettings(list);

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

        private void DrawContextFilterSettings(Listing_Standard listingStandard)
        {
            var ctx = Settings.Context;

            listingStandard.Label("RPD_Setting_FilterLabel".Translate());
            listingStandard.Gap(5f);

            const float colGap = 10f;
            int colCount = 3;
            float colWidth = (listingStandard.ColumnWidth - (colGap * (colCount - 1))) / colCount;

            Rect positionRect = listingStandard.GetRect(0f);
            float startY = positionRect.y;

            Rect col1Rect = new Rect(positionRect.x, startY, colWidth, 9999f);
            Listing_Standard list1 = new Listing_Standard { ColumnWidth = colWidth };
            list1.Begin(col1Rect);

            DrawHeader(list1, "RPD_Group_Bio".Translate());
            DrawFilterRow(list1, "RPD_Filter_Basic".Translate(), ref ctx.Inc_Basic);
            DrawFilterRow(list1, "RPD_Filter_Race".Translate(), ref ctx.Inc_Race, ref ctx.Inc_Race_Desc);
            DrawFilterRow(list1, "RPD_Filter_Genes".Translate(), ref ctx.Inc_Genes, ref ctx.Inc_Genes_Desc, "RPD_Tip_GenesDesc".Translate());
            DrawFilterRow(list1, "RPD_Filter_Backstory".Translate(), ref ctx.Inc_Backstory, ref ctx.Inc_Backstory_Desc);
            DrawFilterRow(list1, "RPD_Filter_Relations".Translate(), ref ctx.Inc_Relations);
            DrawFilterRow(list1, "RPD_Filter_DirectorNotes".Translate(), ref ctx.Inc_DirectorNotes, "RPD_Tip_NotesDesc".Translate());

            list1.End();

            Rect col2Rect = new Rect(col1Rect.xMax + colGap, startY, colWidth, 9999f);
            Listing_Standard list2 = new Listing_Standard { ColumnWidth = colWidth };
            list2.Begin(col2Rect);

            DrawHeader(list2, "RPD_Group_Traits".Translate());
            DrawFilterRow(list2, "RPD_Filter_Traits".Translate(), ref ctx.Inc_Traits, ref ctx.Inc_Traits_Desc);
            DrawFilterRow(list2, "RPD_Filter_Ideology".Translate(), ref ctx.Inc_Ideology, ref ctx.Inc_Ideology_Desc);
            DrawFilterRow(list2, "RPD_Filter_Skills".Translate(), ref ctx.Inc_Skills, ref ctx.Inc_Skills_Desc);
            DrawFilterRow(list2, "RPD_Filter_Health".Translate(), ref ctx.Inc_Health, ref ctx.Inc_Health_Desc);
            DrawFilterRow(list2, "RPD_Filter_Equipment".Translate(), ref ctx.Inc_Equipment);
            DrawFilterRow(list2, "RPD_Filter_Inventory".Translate(), ref ctx.Inc_Inventory);

            list2.End();

            Rect col3Rect = new Rect(col2Rect.xMax + colGap, startY, colWidth, 9999f);
            Listing_Standard list3 = new Listing_Standard { ColumnWidth = colWidth };
            list3.Begin(col3Rect);

            DrawHeader(list3, "RPD_Group_ExternalData".Translate());

            DrawFilterRow(list3, "RPD_Filter_DataComparison".Translate(), ref ctx.Inc_DataComparison);

            // 1. RimPsyche
            if (_isRimPsycheLoaded)
            {
                    DrawFilterRow(list3, "RPD_Filter_RimPsyche".Translate(), ref ctx.Inc_RimPsyche, ref ctx.Inc_RimPsyche_All, "RPD_Tip_RimPsyche".Translate());
            }

            // 2. Memory Mod
            if (ModsConfig.IsActive("ustas.rimai.communication.memory"))
            {
                    DrawFilterRow(list3, "RPD_Filter_Memories".Translate(), ref ctx.Inc_Memories);
                    DrawFilterRow(list3, "RPD_Filter_CommonKnowledge".Translate(), ref ctx.Inc_CommonKnowledge);
            }


                list3.End();

            float maxHeight = Mathf.Max(list1.CurHeight, list2.CurHeight);
            maxHeight = Mathf.Max(maxHeight, list3.CurHeight);

            listingStandard.Gap(maxHeight);
        }

        private void DrawFilterRow(Listing_Standard list, string label, ref bool nameSwitch, ref bool descSwitch, string descTooltip = null)
        {
            Rect rowRect = list.GetRect(24f);

            float descCheckboxWidth = 24f;
            float descCheckboxPadding = 5f;

            Rect mainLabelRect = new Rect(rowRect.x, rowRect.y, rowRect.width - descCheckboxWidth - descCheckboxPadding, rowRect.height);
            Widgets.CheckboxLabeled(mainLabelRect, label, ref nameSwitch);

            if (!nameSwitch) GUI.enabled = false;

            Rect descRect = new Rect(rowRect.xMax - descCheckboxWidth, rowRect.y, descCheckboxWidth, rowRect.height);
            Widgets.Checkbox(descRect.position, ref descSwitch, descCheckboxWidth, !nameSwitch);

            GUI.enabled = true;

            if (descTooltip != null)
            {
                TooltipHandler.TipRegion(rowRect, descTooltip);
            }
        }

        private void DrawFilterRow(Listing_Standard list, string label, ref bool nameSwitch, string tooltip = null)
        {
            Rect rowRect = list.GetRect(24f);
            Widgets.CheckboxLabeled(rowRect, label, ref nameSwitch);

            if (tooltip != null)
            {
                TooltipHandler.TipRegion(rowRect, tooltip);
            }
        }

        private void DrawHeader(Listing_Standard list, string text)
        {
            GUI.color = Color.yellow;
            list.Label($"━━ {text} ━━");
            GUI.color = Color.white;
            list.Gap(2f);
        }
    }
}