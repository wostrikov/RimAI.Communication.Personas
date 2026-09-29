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
        const float ContextWidth = 120f;

        readonly PersonaAutomationSettings _settings;

        public Window_AutoGenConfig(PersonaAutomationSettings settings)
        {
            _settings = settings;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            forcePause = false;
        }

        public override Vector2 InitialSize => new Vector2(820f, 420f);

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

            float y = inRect.y + 78f;
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

            Rect presetRect = new Rect(x, row.y + 3f, PresetWidth - 10f, 24f);
            if (Widgets.ButtonText(presetRect, PresetLabel(category.presetSlot)))
                Find.WindowStack.Add(new FloatMenu(PresetOptions(category)));
            x += PresetWidth;

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
