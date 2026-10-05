using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Ustas.RimAI.Communication.Personas.Automation;
using Ustas.RimAI.Communication.Personas.History;
using Ustas.RimAI.Communication.UI;
using Verse;

namespace Ustas.RimAI.Communication.Personas.UI
{
    /// <summary>
    /// A pawn's persona changes, newest first, each with a way back: restore the
    /// text from before the change or from after it. A restore is itself
    /// recorded, so it can be undone the same way.
    /// </summary>
    public sealed class Window_PersonaHistory : Window
    {
        const float TextBoxHeight = 90f;
        const float CurrentBoxHeight = 110f;
        const float ButtonWidth = 170f;

        readonly Pawn _pawn;
        readonly PersonaEditorWindow _editor;
        Vector2 _scroll;

        public Window_PersonaHistory(Pawn pawn, PersonaEditorWindow editor = null)
        {
            _pawn = pawn;
            _editor = editor;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(640f, 620f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "RPD_History_Title".Translate(_pawn?.LabelShortCap ?? ""));
            Text.Font = GameFont.Small;

            // The persona as it stands, so each record below can be read against it.
            float top = inRect.y + 40f;
            if (_pawn != null)
                top = DrawText(new Rect(inRect.x, 0f, inRect.width - 16f, 0f), top, "RPD_History_CurrentPersona".Translate(), CurrentPersona(), CurrentBoxHeight) + 8f;

            IReadOnlyList<PersonaHistoryRecord> records = PersonaHistoryService.CurrentStore?.GetRecords(_pawn);
            Rect body = new Rect(inRect.x, top, inRect.width, inRect.yMax - 50f - top);
            if (_pawn == null || records == null || records.Count == 0)
            {
                Widgets.Label(body, "RPD_History_NoRecords".Translate());
                return;
            }

            float entryHeight = 24f + 24f + TextBoxHeight * 2f + 24f * 2f + 40f;
            Rect view = new Rect(0f, 0f, body.width - 16f, entryHeight * records.Count);
            Widgets.BeginScrollView(body, ref _scroll, view);
            float y = 0f;
            for (int i = 0; i < records.Count; i++)
            {
                DrawRecord(new Rect(0f, y, view.width, entryHeight - 8f), records[i], i, records.Count);
                y += entryHeight;
            }
            Widgets.EndScrollView();
        }

        void DrawRecord(Rect rect, PersonaHistoryRecord record, int index, int count)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(6f);
            float y = inner.y;

            int daysAgo = Mathf.Max(0, ((Find.TickManager?.TicksGame ?? 0) - record.tick) / GenDate.TicksPerDay);
            string header = "RPD_History_Record".Translate(index + 1, count) + " — "
                + PersonaHistoryService.SourceLabel(record.source);
            Widgets.Label(new Rect(inner.x, y, inner.width, 24f), header);
            y += 24f;

            GUI.color = Color.gray;
            string context = string.IsNullOrWhiteSpace(record.context) ? "RPD_History_NoContext".Translate().ToString() : record.context;
            string contextLine = "RPD_History_Context".Translate(daysAgo) + " " + context.Replace('\n', ' ');
            Rect contextRect = new Rect(inner.x, y, inner.width, 24f);
            Widgets.Label(contextRect, contextLine.Truncate(contextRect.width));
            TooltipHandler.TipRegion(contextRect, context);
            GUI.color = Color.white;
            y += 24f;

            y = DrawText(inner, y, "RPD_History_BeforePersona".Translate(), record.before);
            y = DrawText(inner, y, "RPD_History_AfterPersona".Translate(), record.after);

            Rect restoreBefore = new Rect(inner.x, y + 4f, ButtonWidth, 28f);
            Rect restoreAfter = new Rect(restoreBefore.xMax + 10f, y + 4f, ButtonWidth, 28f);
            if (Widgets.ButtonText(restoreBefore, "RPD_History_RestoreBefore".Translate()))
                ConfirmRestore(record.before, "RPD_History_RestoreBeforeConfirm".Translate());
            if (Widgets.ButtonText(restoreAfter, "RPD_History_RestoreAfter".Translate()))
                ConfirmRestore(record.after, "RPD_History_RestoreAfterConfirm".Translate());
        }

        /// <summary>The open editor's text when there is one - that is what Save will write.</summary>
        string CurrentPersona()
        {
            if (_editor != null && Find.WindowStack.IsOpen(_editor) && !string.IsNullOrEmpty(_editor.EditingPersonality))
                return _editor.EditingPersonality;
            return PersonaOwnership.ReadWithoutCreating(_pawn);
        }

        static float DrawText(Rect inner, float y, string label, string text, float height = TextBoxHeight)
        {
            Widgets.Label(new Rect(inner.x, y, inner.width, 24f), label);
            y += 24f;
            Rect box = new Rect(inner.x, y, inner.width, height);
            Widgets.DrawBoxSolid(box, new Color(0f, 0f, 0f, 0.25f));
            Text.Font = GameFont.Tiny;
            Widgets.Label(box.ContractedBy(4f), string.IsNullOrEmpty(text) ? "RPD_History_Empty".Translate().ToString() : text);
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(box, text ?? "");
            return y + height;
        }

        void ConfirmRestore(string text, string question)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(question, () =>
            {
                if (!PersonaHistoryService.Restore(_pawn, text)) return;
                // An open editor would write its stale text back on Save.
                if (_editor != null && Find.WindowStack.IsOpen(_editor))
                    _editor.EditingPersonality = text.Trim();
                Messages.Message("RPD_History_RestoreSuccess".Translate(_pawn.LabelShortCap), _pawn, MessageTypeDefOf.TaskCompletion, false);
            }));
        }
    }
}
