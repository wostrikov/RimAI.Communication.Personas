using System.Collections.Generic;
using UnityEngine;
using Ustas.RimAI.Communication.Personas.Automation;
using Ustas.RimAI.Communication.Personas.Config;
using Verse;

namespace Ustas.RimAI.Communication.Personas.UI
{
    /// <summary>Per-category Auto-Gen choices: when to generate, with which prompt and which pawn data.</summary>
    public sealed class Window_AutoGenConfig : Window
    {
        const float RowHeight = 30f;
        const float CategoryWidth = 150f;
        const float CheckWidth = 110f;
        const float PresetWidth = 170f;
        const float AdvancedWidth = 170f;
        const float ContextWidth = 120f;
        const float NotesHeight = 64f;

        readonly PersonaAutomationSettings _settings;

        public Window_AutoGenConfig(PersonaAutomationSettings settings)
        {
            _settings = settings;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            forcePause = false;
        }

        public override Vector2 InitialSize => new Vector2(990f, 520f);

        public override void PreClose()
        {
            base.PreClose();
            PersonasMod.Settings?.Write();
        }

        public override void DoWindowContents(Rect inRect)
        {
            _settings.EnsureCategories();

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "RPD_AutoGen_ConfigTitle".Translate());
            Text.Font = GameFont.Small;

            Rect switchRect = new Rect(inRect.x, inRect.y + 40f, 320f, 24f);
            Widgets.CheckboxLabeled(switchRect, "RPD_Settings_EnableAutoGen".Translate(), ref _settings.autoGenEnabled);
            TooltipHandler.TipRegion(switchRect, "RPD_Settings_EnableAutoGenTip".Translate());

            Rect notesLabel = new Rect(inRect.x, inRect.y + 70f, inRect.width, 24f);
            Widgets.Label(notesLabel, "RPD_AutoGen_Notes".Translate());
            TooltipHandler.TipRegion(notesLabel, "RPD_AutoGen_NotesTip".Translate());
            _settings.autoGenNotes = Widgets.TextArea(new Rect(inRect.x, inRect.y + 94f, inRect.width, NotesHeight), _settings.autoGenNotes ?? string.Empty);

            float y = inRect.y + 94f + NotesHeight + 10f;
            DrawHeaderRow(new Rect(inRect.x, y, inRect.width, 24f));
            y += 28f;
            Widgets.DrawLineHorizontal(inRect.x, y - 2f, inRect.width);

            foreach (PawnRoleCategory role in PawnRoleClassifier.Ordered)
            {
                AutoGenCategorySettings category = _settings.GetCategory(role);
                if (category == null) continue;
                DrawCategoryRow(new Rect(inRect.x, y, inRect.width, RowHeight), role, category);
                y += RowHeight;
            }
        }

        static void DrawHeaderRow(Rect row)
        {
            GUI.color = Color.gray;
            float x = row.x + CategoryWidth;
            Label(ref x, row, CheckWidth, "RPD_AutoGen_Enable".Translate(), "RPD_AutoGen_EnableTip".Translate());
            Label(ref x, row, CheckWidth, "RPD_AutoGen_OnlyOnRoleChangeShort".Translate(), "RPD_AutoGen_OnlyOnRoleChangeTip".Translate());
            Label(ref x, row, CheckWidth, "RPD_AutoGen_SyncModSettingsShort".Translate(), "RPD_AutoGen_SyncModSettingsTip".Translate());
            Label(ref x, row, PresetWidth, "RPD_AutoGen_Preset".Translate(), "RPD_AutoGen_PresetTip".Translate());
            Label(ref x, row, AdvancedWidth, "RPD_AutoGen_AdvancedPreset".Translate(), "RPD_AutoGen_AdvancedPresetTip".Translate());
            GUI.color = Color.white;
        }

        static void Label(ref float x, Rect row, float width, string text, string tip)
        {
            Rect rect = new Rect(x, row.y, width, row.height);
            Widgets.Label(rect, text);
            TooltipHandler.TipRegion(rect, tip);
            x += width;
        }

        void DrawCategoryRow(Rect row, PawnRoleCategory role, AutoGenCategorySettings category)
        {
            Widgets.DrawHighlightIfMouseover(row);
            float x = row.x;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(x, row.y, CategoryWidth, row.height), PawnRoleClassifier.Label(role));
            Text.Anchor = TextAnchor.UpperLeft;
            x += CategoryWidth;

            Checkbox(ref x, row, ref category.enabled, "RPD_AutoGen_EnableTip".Translate());
            Checkbox(ref x, row, ref category.onRoleChange, "RPD_AutoGen_OnlyOnRoleChangeTip".Translate());
            Checkbox(ref x, row, ref category.syncWithGlobalContext, "RPD_AutoGen_SyncModSettingsTip".Translate());

            bool advanced = DirectorPresetRenderer.IsSet(category.advancedPreset);
            Rect presetRect = new Rect(x, row.y + 3f, PresetWidth - 10f, 24f);
            if (Widgets.ButtonText(presetRect, PresetLabel(category.presetSlot), active: !advanced) && !advanced)
                Find.WindowStack.Add(new FloatMenu(PresetOptions(category)));
            x += PresetWidth;

            Rect advancedRect = new Rect(x, row.y + 3f, AdvancedWidth - 10f, 24f);
            string advancedLabel = advanced ? category.advancedPreset : "RPD_AutoGen_AdvancedNone".Translate().ToString();
            if (Widgets.ButtonText(advancedRect, advancedLabel.Truncate(advancedRect.width - 10f)))
                Find.WindowStack.Add(new FloatMenu(AdvancedOptions(category)));
            TooltipHandler.TipRegion(advancedRect, "RPD_AutoGen_AdvancedPresetTip".Translate());
            x += AdvancedWidth;

            Rect contextRect = new Rect(x, row.y + 3f, ContextWidth, 24f);
            TooltipHandler.TipRegion(contextRect, "RPD_AutoGen_EditContextTip".Translate());
            if (Widgets.ButtonText(contextRect, "RPD_AutoGen_EditContext".Translate(), active: !category.syncWithGlobalContext)
                && !category.syncWithGlobalContext)
            {
                Find.WindowStack.Add(new Window_AutoGenContext(PawnRoleClassifier.Label(role), category.customContext));
            }
        }

        static void Checkbox(ref float x, Rect row, ref bool value, string tip)
        {
            Rect rect = new Rect(x + 8f, row.y + 3f, 24f, 24f);
            Widgets.Checkbox(rect.position, ref value);
            TooltipHandler.TipRegion(rect, tip);
            x += CheckWidth;
        }

        static string PresetLabel(int slot)
        {
            List<PromptPreset> presets = PersonasMod.Settings?.presets;
            int index = AutoGenCategorySettings.ClampSlot(slot);
            string label = presets != null && presets.Count > index ? presets[index]?.label : null;
            return string.IsNullOrEmpty(label) ? "RPD_Setting_Slot".Translate(index + 1).ToString() : label;
        }

        /// <summary>Every Communication preset, and the slot's own prompt.</summary>
        static List<FloatMenuOption> AdvancedOptions(AutoGenCategorySettings category)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("RPD_AutoGen_AdvancedNone".Translate(), () => category.advancedPreset = string.Empty)
            };
            var presets = Ustas.RimAI.Communication.API.RimTalkPromptAPI.GetAllPresets();
            if (presets != null)
            {
                foreach (var preset in presets)
                {
                    string name = preset?.Name;
                    if (string.IsNullOrEmpty(name)) continue;
                    options.Add(new FloatMenuOption(name, () => category.advancedPreset = name));
                }
            }
            return options;
        }

        static List<FloatMenuOption> PresetOptions(AutoGenCategorySettings category)
        {
            var options = new List<FloatMenuOption>();
            for (int slot = AutoGenCategorySettings.FirstSinglePersonaSlot; slot <= AutoGenCategorySettings.LastSinglePersonaSlot; slot++)
            {
                int chosen = slot;
                options.Add(new FloatMenuOption(PresetLabel(chosen), () => category.presetSlot = chosen));
            }
            return options;
        }
    }

    /// <summary>One Auto-Gen category's own choice of pawn data.</summary>
    public sealed class Window_AutoGenContext : Window
    {
        readonly string _title;
        readonly ContextSettings _context;
        readonly bool _rimPsycheLoaded;

        public Window_AutoGenContext(string categoryLabel, ContextSettings context)
        {
            _title = "RPD_AutoGen_ContextTitle".Translate(categoryLabel);
            _context = context;
            _rimPsycheLoaded = PersonasMod.IsRimPsycheLoaded;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(760f, 420f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), _title);
            Text.Font = GameFont.Small;

            var listing = new Listing_Standard { maxOneColumn = true };
            listing.Begin(new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 90f));
            ContextFilterDrawer.Draw(listing, _context, _rimPsycheLoaded);
            listing.End();
        }
    }
}
