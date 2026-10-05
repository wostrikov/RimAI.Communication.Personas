using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Personas.UI
{
    /// <summary>
    /// A searchable, scrolling list of pawns with one row each; what a row shows
    /// is the caller's. The search matches the pawn's short name.
    /// </summary>
    internal sealed class PawnRosterView
    {
        readonly QuickSearchWidget _search = new QuickSearchWidget();
        Vector2 _scroll;

        public void Draw(Rect rect, IReadOnlyList<Pawn> pawns, float rowHeight, Action<Rect, Pawn> drawRow)
        {
            Rect searchRect = new Rect(rect.x, rect.y, Mathf.Min(260f, rect.width), QuickSearchWidget.WidgetHeight);
            _search.OnGUI(searchRect);
            TooltipHandler.TipRegion(searchRect, "RPD_Batch_SearchTip".Translate());

            var shown = new List<Pawn>();
            if (pawns != null)
            {
                foreach (Pawn pawn in pawns)
                {
                    if (pawn != null && (!_search.filter.Active || _search.filter.Matches(pawn.LabelShortCap)))
                        shown.Add(pawn);
                }
            }
            _search.noResultsMatched = _search.filter.Active && shown.Count == 0;

            Rect body = new Rect(rect.x, searchRect.yMax + 6f, rect.width, rect.height - searchRect.height - 6f);
            if (shown.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(body, "RPD_Roster_Empty".Translate());
                GUI.color = Color.white;
                return;
            }

            Rect view = new Rect(0f, 0f, body.width - 16f, rowHeight * shown.Count);
            Widgets.BeginScrollView(body, ref _scroll, view);
            for (int i = 0; i < shown.Count; i++)
            {
                Rect row = new Rect(0f, i * rowHeight, view.width, rowHeight);
                if (i % 2 == 1) Widgets.DrawLightHighlight(row);
                drawRow(row, shown[i]);
            }
            Widgets.EndScrollView();
        }
    }
}
