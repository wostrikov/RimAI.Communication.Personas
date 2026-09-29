using UnityEngine;
using Ustas.RimAI.Communication.Personas.Config;
using Ustas.RimAI.Communication.Personas.Policy;
using Verse;

namespace Ustas.RimAI.Communication.Personas.UI
{
    /// <summary>Auto-Evolve choices: whom it covers, how often, on which events, and append or overwrite.</summary>
    public sealed class Window_AutoEvolveConfig : Window
    {
        readonly PersonaAutomationSettings _settings;
        string _intervalBuffer;

        public Window_AutoEvolveConfig(PersonaAutomationSettings settings)
        {
            _settings = settings;
            _intervalBuffer = settings.autoEvolveIntervalDays.ToString();
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(560f, 560f);

        public override void PreClose()
        {
            base.PreClose();
            PersonasMod.Settings?.Write();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "RPD_Tab_AutoEvolution".Translate());
            Text.Font = GameFont.Small;

            var list = new Listing_Standard { maxOneColumn = true };
            list.Begin(new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 90f));

            list.CheckboxLabeled("RPD_AutoEvolve_Enable".Translate(), ref _settings.autoEvolveEnabled, "RPD_AutoEvolve_EnableTooltip".Translate());
            list.CheckboxLabeled("RPD_AutoEvolve_IncludeCaptives".Translate(), ref _settings.autoEvolveIncludeCaptives, "RPD_AutoEvolve_IncludeCaptivesTip".Translate());
            list.Gap(6f);

            Rect intervalRow = list.GetRect(24f);
            Widgets.Label(intervalRow.LeftPart(0.7f), "RPD_AutoEvolve_Interval".Translate());
            int days = _settings.autoEvolveIntervalDays;
            Widgets.TextFieldNumeric(intervalRow.RightPart(0.25f), ref days, ref _intervalBuffer, 0f, PersonaAutomationPolicy.MaxIntervalDays);
            _settings.autoEvolveIntervalDays = PersonaAutomationPolicy.ClampIntervalDays(days);
            TooltipHandler.TipRegion(intervalRow, "RPD_AutoEvolve_IntervalTip".Translate());
            list.Gap(6f);

            DrawMode(list);
            list.GapLine();

            string triggers = "RPD_AutoEvolve_Triggers".Translate();
            Rect triggersRect = list.Label("<b>" + triggers + "</b>");
            TooltipHandler.TipRegion(triggersRect, "RPD_AutoEvolve_TriggersTip".Translate());
            list.CheckboxLabeled("RPD_AutoEvolve_RoleTriggers".Translate(), ref _settings.evolveOnRoleChange, "RPD_AutoEvolve_RoleChangeTip".Translate());
            list.CheckboxLabeled("RPD_AutoEvolve_EventMarriage".Translate(), ref _settings.evolveOnMarriage, "RPD_AutoEvolve_EventMarriageTip".Translate());
            list.CheckboxLabeled("RPD_AutoEvolve_EventBreakup".Translate(), ref _settings.evolveOnBreakup, "RPD_AutoEvolve_EventBreakupTip".Translate());
            list.CheckboxLabeled("RPD_AutoEvolve_EventBirth".Translate(), ref _settings.evolveOnBirth, "RPD_AutoEvolve_EventBirthTip".Translate());
            list.CheckboxLabeled("RPD_AutoEvolve_EventDirectFamilyDeath".Translate(), ref _settings.evolveOnFamilyDeath, "RPD_AutoEvolve_EventDirectFamilyDeathTip".Translate());
            list.CheckboxLabeled("RPD_AutoEvolve_EventTraitAdded".Translate(), ref _settings.evolveOnTraitGained, "RPD_AutoEvolve_EventTraitAddedTip".Translate());

            list.End();
        }

        void DrawMode(Listing_Standard list)
        {
            Rect row = list.GetRect(24f);
            Widgets.Label(row.LeftPart(0.4f), "RPD_AutoEvolve_Mode".Translate());
            TooltipHandler.TipRegion(row, "RPD_AutoEvolve_ModeTooltip".Translate());

            Rect right = row.RightPart(0.6f);
            Rect appendRect = right.LeftHalf();
            Rect overwriteRect = right.RightHalf();
            if (Widgets.RadioButtonLabeled(appendRect, "RPD_AutoEvolve_ModeAppend".Translate(), _settings.autoEvolveMode == AutoEvolveMode.Append))
                _settings.autoEvolveMode = AutoEvolveMode.Append;
            if (Widgets.RadioButtonLabeled(overwriteRect, "RPD_AutoEvolve_ModeOverwrite".Translate(), _settings.autoEvolveMode == AutoEvolveMode.Overwrite))
                _settings.autoEvolveMode = AutoEvolveMode.Overwrite;
        }
    }
}
