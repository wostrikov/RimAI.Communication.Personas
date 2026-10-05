using Ustas.RimAI.Communication.UI;
using Ustas.RimAI.Communication.Personas.Config;
using Ustas.RimAI.Communication.Personas.Policy;
using Ustas.RimAI.Core.Diagnostics;
using RimWorld;
using System;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using RimAI.Core.Runtime;

namespace Ustas.RimAI.Communication.Personas
{
    public static class Patch_PersonaEditorWindow_DirectorFeatures
    {
        private static Task<string> evolveTask = null;
        private static Pawn evolvingPawn = null;
        // The window the request came from: a different editor opened meanwhile
        // must not receive the answer.
        private static Window evolvingWindow = null;
        // Captured at the click, so changing the mode mid-request changes nothing.
        private static bool evolvingOverwrite = false;

        private static void ClearEvolveState()
        {
            evolveTask = null;
            evolvingPawn = null;
            evolvingWindow = null;
            evolvingOverwrite = false;
        }

        /// <summary>
        /// Takes a finished answer on the UI thread. A continuation used to write it
        /// from the worker thread, racing the next frame's read.
        /// </summary>
        private static string TakeFinishedResult(Window window)
        {
            if (evolveTask == null || !evolveTask.IsCompleted || evolvingWindow != window)
                return null;
            string result = null;
            if (evolveTask.IsFaulted)
                RimAiLog.Warning(RimAiLogCategory.Personas, "[Director] Manual Evolve request failed: " + evolveTask.Exception?.GetBaseException());
            else if (!evolveTask.IsCanceled)
                result = evolveTask.Result;
            evolveTask = null;
            return result;
        }

        public static void DrawFooter(PersonaEditorWindow window, Pawn pawn, Rect inRect)
        {
            if (window == null || pawn == null) return;
            History.PersonaEditorHistoryWatcher.Observe(window, pawn);

            string evolveResult = TakeFinishedResult(window);
            if (evolveResult != null && evolvingPawn == pawn)
            {
                // Overwrite replaces the text with the rewritten persona; Append adds
                // a development line - the Auto-Evolve page's mode, as Auto-Evolve uses it.
                string currentText = DirectorUtils.GetWindowText(window);
                DirectorUtils.SetWindowText(window, PersonaAutomationPolicy.ComposeEvolved(currentText, evolveResult, evolvingOverwrite));
            }
            if (evolveTask == null && evolvingWindow == window)
                ClearEvolveState();

            float footerY = inRect.y + 267f;
            float buttonWidth = 80f;
            float buttonHeight = 24f;
            float spacing = 5f;

            float startX = inRect.x;
            Rect noteRect = new Rect(startX, footerY, buttonWidth, buttonHeight);
            Rect evolveRect = new Rect(noteRect.xMax + spacing, footerY, buttonWidth, buttonHeight);
            Rect timeRect = new Rect(evolveRect.xMax + spacing, footerY, buttonWidth, buttonHeight);

            if (PersonasMod.Settings.Context.Inc_DirectorNotes)
            {
                string currentNotes = PersonasMod.Settings.directorNotes;
                bool hasNotes = !string.IsNullOrEmpty(currentNotes);
                Color oldColor = GUI.color;
                if (hasNotes) GUI.color = Color.cyan;

                if (Widgets.ButtonText(noteRect, "RPD_Button_EditNotes".Translate()))
                {
                    Find.WindowStack.Add(new Window_DirectorNotesEditor());
                }
                GUI.color = oldColor;

                if (Mouse.IsOver(noteRect))
                {
                    string tooltip = hasNotes
                        ? $"{"RPD_Tip_CurrentNotes".Translate()}:\n{currentNotes}"
                        : "RPD_Tip_NoNotes".Translate().ToString();
                    TooltipHandler.TipRegion(noteRect, tooltip);
                }
            }

            Rect historyRect = new Rect(timeRect.xMax + spacing, footerY, buttonWidth, buttonHeight);
            if (History.PersonaHistoryService.CurrentStore?.HasRecords(pawn) == true)
            {
                if (Widgets.ButtonText(historyRect, "RPD_AutoEvolve_ButtonHistory".Translate()))
                    Find.WindowStack.Add(new UI.Window_PersonaHistory(pawn, window));
                TooltipHandler.TipRegion(historyRect, "RPD_Tip_History".Translate());
            }

            if (!PersonasMod.Settings.enableEvolveFeature)
                return;

            bool isEvolving = evolveTask != null && !evolveTask.IsCompleted;

            if (isEvolving)
            {
                Widgets.ButtonText(evolveRect, "RPD_Batch_Status_Generating".Translate(), active: false);
            }
            else
            {
                if (Widgets.ButtonText(evolveRect, "RPD_Button_Evolve".Translate()))
                {
                    ClearEvolveState();
                    evolvingPawn = pawn;
                    evolvingWindow = window;
                    evolvingOverwrite = PersonasMod.Settings.Automation?.autoEvolveMode == AutoEvolveMode.Overwrite;
                    var (request, currentPersona) = DirectorPersonaEvolve.PrepareEvolveRequest(pawn, window, null, evolvingOverwrite);

                    if (request != null)
                    {
                        evolveTask = RimAiBackground.Run(() =>
                        {
                            var result = DirectorUtils.ExecuteEvolveTask(request);
                            if (result != null && !string.IsNullOrEmpty(result.Persona))
                                return result.Persona.Trim();
                            return null;
                        });
                    }
                    else
                    {
                        Messages.Message("RPD_Msg_PrepareFailed".Translate(), MessageTypeDefOf.RejectInput, false);
                    }
                }
                TooltipHandler.TipRegion(evolveRect, "RPD_Tip_Evolve".Translate());
            }

            var worldComp = Find.World.GetComponent<DirectorWorldComponent>();
            if (worldComp == null)
                return;

            int lastTick = worldComp.GetLastEvolveTick(pawn);
            string timeLabel = "RPD_Button_SetTime".Translate();
            string tooltipTime = "RPD_Tip_SetTime_Empty".Translate();

            if (lastTick > 0)
            {
                int days = (GenTicks.TicksGame - lastTick) / 60000;
                timeLabel = "RPD_Button_TimeAgo".Translate(days);
                long ageBioYears = worldComp.GetLastEvolveBioAgeTicks(pawn) / 3600000;
                tooltipTime = "RPD_Tip_SetTime_Info".Translate(days, ageBioYears);
            }

            if (Widgets.ButtonText(timeRect, timeLabel))
            {
                string snapshot = DirectorUtils.BuildCustomCharacterData(pawn, true, false);
                worldComp.SetTimestamp(pawn, snapshot);
                Messages.Message("RPD_Msg_TimestampUpdated".Translate(), MessageTypeDefOf.PositiveEvent, false);
            }
            TooltipHandler.TipRegion(timeRect, tooltipTime);
        }
    }
}
