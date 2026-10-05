using System.Collections.Generic;
using UnityEngine;
using Ustas.RimAI.Communication.Personas.Automation;
using Ustas.RimAI.Communication.Personas.Config;
using Ustas.RimAI.Communication.Personas.Policy;
using Verse;

namespace Ustas.RimAI.Communication.Personas.UI
{
    /// <summary>
    /// The automation block of the Mod-settings page: the two master switches,
    /// the buttons to their detail windows, and the history limit.
    /// </summary>
    internal static class PersonaAutomationSettingsSection
    {
        const float ButtonWidth = 140f;

        internal static void Draw(Listing_Standard list, PersonaAutomationSettings settings)
        {
            string header = "RPD_Settings_AutomationHeader".Translate();
            list.Label("<b>" + header + "</b>");
            DrawFuse(list, settings);

            if (DrawSwitchRow(list, "RPD_Settings_EnableAutoGen".Translate(), "RPD_Settings_EnableAutoGenTip".Translate(),
                    ref settings.autoGenEnabled, "RPD_Settings_ConfigAutoGenTip".Translate()))
            {
                Find.WindowStack.Add(new Window_AutoGenConfig(settings));
            }

            if (DrawSwitchRow(list, "RPD_AutoEvolve_Enable".Translate(), "RPD_AutoEvolve_EnableTooltip".Translate(),
                    ref settings.autoEvolveEnabled, "RPD_AutoEvolve_ConfigTip".Translate()))
            {
                Find.WindowStack.Add(new Window_AutoEvolveConfig(settings));
            }

            list.CheckboxLabeled("RPD_Settings_AutoNotify".Translate(), ref settings.notifyOnAutoChange, "RPD_Settings_AutoNotifyTip".Translate());
            DrawPauseAtSpeed(list, settings);

            int pending = PersonasComposition.Current.IsStarted ? PersonasComposition.Current.Automation.PendingCount : 0;
            if (pending > 0 && Current.ProgramState == ProgramState.Playing)
            {
                GUI.color = Color.gray;
                list.Label("RPD_AutoGen_QueueStatus".Translate(pending));
                GUI.color = Color.white;
            }

            float max = list.SliderLabeled(
                "RPD_History_MaxRecords".Translate() + " " + settings.historyMaxRecords,
                settings.historyMaxRecords,
                PersonaHistoryPolicy.MinMaxRecords,
                PersonaHistoryPolicy.MaxMaxRecords,
                0.5f,
                "RPD_History_MaxRecordsTip".Translate());
            settings.historyMaxRecords = PersonaHistoryPolicy.ClampMaxRecords(Mathf.RoundToInt(max));
        }

        /// <summary>Why the automation turned itself off, and the one way to turn it back on.</summary>
        static void DrawFuse(Listing_Standard list, PersonaAutomationSettings settings)
        {
            if (!PersonaAutomationFuse.IsTripped(settings)) return;
            Rect row = list.GetRect(28f);
            GUI.color = new Color(1f, 0.45f, 0.45f);
            Widgets.Label(new Rect(row.x, row.y, row.width - ButtonWidth - 10f, row.height),
                "RPD_Experimental_FuseReason".Translate(PersonaAutomationFuse.ReasonText(settings)));
            GUI.color = Color.white;
            if (Widgets.ButtonText(new Rect(row.xMax - ButtonWidth, row.y, ButtonWidth, 24f), "RPD_Experimental_ClearError".Translate()))
                PersonaAutomationFuse.Clear(settings);
            list.Gap(4f);
        }

        static void DrawPauseAtSpeed(Listing_Standard list, PersonaAutomationSettings settings)
        {
            Rect row = list.GetRect(28f);
            Widgets.Label(new Rect(row.x, row.y, row.width - ButtonWidth - 10f, 24f), "RPD_Automation_PauseAtSpeed".Translate());
            TooltipHandler.TipRegion(row, "RPD_Automation_PauseAtSpeedTip".Translate());
            Rect button = new Rect(row.xMax - ButtonWidth, row.y, ButtonWidth, 24f);
            if (!Widgets.ButtonText(button, SpeedLabel(settings.pauseAtSpeed))) return;

            var options = new List<FloatMenuOption>();
            foreach (int speed in new[] { 0, 2, 3, 4 })
            {
                int chosen = speed;
                options.Add(new FloatMenuOption(SpeedLabel(chosen), () => settings.pauseAtSpeed = chosen));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        static string SpeedLabel(int speed) =>
            speed <= 0
                ? "RPD_Automation_PauseNever".Translate().ToString()
                : "RPD_Automation_PauseAtSpeedValue".Translate(speed).ToString();

        /// <summary>A labelled switch with a Configure button beside it; returns true when the button was pressed.</summary>
        static bool DrawSwitchRow(Listing_Standard list, string label, string tip, ref bool value, string buttonTip)
        {
            Rect row = list.GetRect(28f);
            Rect checkRect = new Rect(row.x, row.y, row.width - ButtonWidth - 10f, 24f);
            Widgets.CheckboxLabeled(checkRect, label, ref value);
            TooltipHandler.TipRegion(checkRect, tip);

            Rect buttonRect = new Rect(row.xMax - ButtonWidth, row.y, ButtonWidth, 24f);
            TooltipHandler.TipRegion(buttonRect, buttonTip);
            return Widgets.ButtonText(buttonRect, "RPD_Settings_ConfigAutoGen".Translate());
        }
    }
}
