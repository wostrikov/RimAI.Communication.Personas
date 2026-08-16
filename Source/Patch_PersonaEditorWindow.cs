using Ustas.RimAI.Communication.UI;
using RimWorld;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Personas
{
    public static class Patch_PersonaEditorWindow_DirectorFeatures
    {
        private static Task<string> evolveTask = null;
        private static string evolveResult = null;
        private static Pawn evolvingPawn = null;

        private static void ClearEvolveState()
        {
            evolveTask = null;
            evolveResult = null;
            evolvingPawn = null;
        }

        public static void DrawFooter(PersonaEditorWindow window, Pawn pawn, Rect inRect)
        {
            if (window == null || pawn == null) return;

            if (evolveResult != null && evolvingPawn == pawn)
            {
                string currentText = DirectorUtils.GetWindowText(window);
                string newText = $"{currentText}\n\n[Development]: {evolveResult}";
                DirectorUtils.SetWindowText(window, newText);
                evolveResult = null;
                evolvingPawn = null;
            }

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
                    var (request, currentPersona) = DirectorUtils.PrepareEvolveRequest(pawn, window);

                    if (request != null)
                    {
                        evolveTask = Task.Run(() =>
                        {
                            var result = DirectorUtils.ExecuteEvolveTask(request);
                            if (result != null && !string.IsNullOrEmpty(result.Persona))
                                return result.Persona.Trim();
                            return null;
                        });

                        evolveTask.ContinueWith(task => {
                            if (task.IsCompleted && !task.IsFaulted)
                                evolveResult = task.Result;
                            evolveTask = null;
                        });
                    }
                    else
                    {
                        Messages.Message("Failed to prepare data.", MessageTypeDefOf.RejectInput, false);
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
