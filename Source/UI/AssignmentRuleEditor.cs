using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Ustas.RimAI.Communication.Personas.Policy;
using Verse;

namespace Ustas.RimAI.Communication.Personas.UI
{
    /// <summary>
    /// What the library manager needs to show and edit one assignment rule:
    /// its kind and target in words, the def picker, the age range and the
    /// heading of the preset pool. Lifted out of the window when rules learned
    /// to go by age, because the window is held to the size it had.
    /// </summary>
    internal static class AssignmentRuleEditor
    {
        /// <summary>What a rule is about, for the list and its search: the def, or the years of an Age rule.</summary>
        public static string TargetLabel(AssignmentRule rule)
        {
            if (rule.type == RuleType.Age) return rule.minAge + "-" + rule.maxAge;
            return rule.targetDefName ?? "None";
        }

        public static void DrawAgeRange(Rect row, AssignmentRule rule)
        {
            const float fieldWidth = 60f;
            Text.Anchor = TextAnchor.MiddleLeft;
            string label = "RPD_Library_AgeRange".Translate();
            float labelWidth = Mathf.Min(Text.CalcSize(label).x + 10f, row.width - 2f * fieldWidth - 30f);
            Widgets.Label(new Rect(row.x, row.y, labelWidth, row.height), label);
            Text.Anchor = TextAnchor.UpperLeft;

            float x = row.x + labelWidth;
            string minBuffer = rule.minAge.ToString();
            Widgets.TextFieldNumeric(new Rect(x, row.y, fieldWidth, row.height), ref rule.minAge, ref minBuffer,
                PersonaAssignmentRulePolicy.MinAllowedAge, PersonaAssignmentRulePolicy.MaxAllowedAge);
            x += fieldWidth;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(x, row.y, 30f, row.height), "-");
            Text.Anchor = TextAnchor.UpperLeft;
            x += 30f;
            string maxBuffer = rule.maxAge.ToString();
            Widgets.TextFieldNumeric(new Rect(x, row.y, fieldWidth, row.height), ref rule.maxAge, ref maxBuffer,
                PersonaAssignmentRulePolicy.MinAllowedAge, PersonaAssignmentRulePolicy.MaxAllowedAge);
            TooltipHandler.TipRegion(row, "RPD_Library_AgeRangeTip".Translate());
        }

        /// <summary>The pool's heading, with the two buttons that tick or clear every preset at once.</summary>
        public static void DrawPoolHeader(Rect row, AssignmentRule rule)
        {
            const float buttonWidth = 130f;
            // One line whatever the language: the heading shares its row with
            // two buttons, and a wrapped one would run into the pool below.
            Rect heading = new Rect(row.x, row.y, row.width - 2f * buttonWidth - 12f, row.height);
            string title = "RPD_Library_PresetsPool".Translate();
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.WordWrap = false;
            Widgets.Label(heading, title);
            Text.WordWrap = true;
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(heading, title);

            var presets = PersonasMod.Settings.userPresets;
            if (Widgets.ButtonText(new Rect(row.xMax - 2f * buttonWidth - 6f, row.y, buttonWidth, row.height), "RPD_Library_SelectAll".Translate()))
            {
                foreach (CustomPreset preset in presets)
                {
                    if (preset != null && !rule.allowedPresetIds.Contains(preset.id)) rule.allowedPresetIds.Add(preset.id);
                }
            }
            if (Widgets.ButtonText(new Rect(row.xMax - buttonWidth, row.y, buttonWidth, row.height), "RPD_Library_UnselectAll".Translate()))
                rule.allowedPresetIds.Clear();
        }

        public static bool TargetExists(RuleType type, string defName)
        {
            if (string.IsNullOrEmpty(defName)) return true;

            switch (type)
            {
                case RuleType.FactionDef:
                    return DefDatabase<FactionDef>.GetNamedSilentFail(defName) != null;
                case RuleType.RaceDef:
                    return DefDatabase<ThingDef>.GetNamedSilentFail(defName) != null;
                case RuleType.XenotypeDef:
                    return ModsConfig.BiotechActive && DefDatabase<XenotypeDef>.GetNamedSilentFail(defName) != null;
                default:
                    return false;
            }
        }

        public static void OpenDefSelector(AssignmentRule rule)
        {
            List<FloatMenuOption> opts = new List<FloatMenuOption>();

            if (rule.type == RuleType.FactionDef)
            {
                foreach (var def in DefDatabase<FactionDef>.AllDefs.OrderBy(d => d.label))
                {
                    var currentDef = def;
                    opts.Add(new FloatMenuOption($"{currentDef.LabelCap} ({currentDef.defName})",
                        () => rule.targetDefName = currentDef.defName));
                }
            }
            else if (rule.type == RuleType.RaceDef)
            {
                foreach (var def in DefDatabase<ThingDef>.AllDefs.Where(d => d.race != null && d.race.Humanlike).OrderBy(d => d.label))
                {
                    var currentDef = def;
                    opts.Add(new FloatMenuOption(currentDef.LabelCap,
                        () => rule.targetDefName = currentDef.defName));
                }
            }
            else if (rule.type == RuleType.XenotypeDef && ModsConfig.BiotechActive)
            {
                foreach (var def in DefDatabase<XenotypeDef>.AllDefs.OrderBy(d => d.label))
                {
                    var currentDef = def;
                    opts.Add(new FloatMenuOption(currentDef.LabelCap,
                        () => rule.targetDefName = currentDef.defName));
                }
            }

            if (opts.Count == 0) opts.Add(new FloatMenuOption("RPD_Library_NoDefsFound".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        public static string TypeLabel(RuleType type)
        {
            switch (type)
            {
                case RuleType.FactionDef:
                    return "RPD_RuleType_FactionDef".Translate();
                case RuleType.RaceDef:
                    return "RPD_RuleType_RaceDef".Translate();
                case RuleType.XenotypeDef:
                    return "RPD_RuleType_XenotypeDef".Translate();
                case RuleType.Age:
                    return "RPD_RuleType_Age".Translate();
                default:
                    return type.ToString();
            }
        }
    }
}
