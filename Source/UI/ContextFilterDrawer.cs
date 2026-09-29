using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Personas.UI
{
    /// <summary>
    /// The three-column "which pawn data goes to the model" checklist. Drawn for
    /// the global selection on the Mod-settings page and for an Auto-Gen
    /// category's own selection.
    /// </summary>
    internal static class ContextFilterDrawer
    {
        const float ColumnGap = 10f;
        const int ColumnCount = 3;

        internal static void Draw(Listing_Standard listing, ContextSettings ctx, bool rimPsycheLoaded)
        {
            listing.Label("RPD_Setting_FilterLabel".Translate());
            listing.Gap(5f);

            float colWidth = (listing.ColumnWidth - (ColumnGap * (ColumnCount - 1))) / ColumnCount;
            Rect positionRect = listing.GetRect(0f);
            float startY = positionRect.y;

            Rect col1Rect = new Rect(positionRect.x, startY, colWidth, 9999f);
            Listing_Standard list1 = new Listing_Standard { ColumnWidth = colWidth };
            list1.Begin(col1Rect);
            DrawHeader(list1, "RPD_Group_Bio".Translate());
            DrawRow(list1, "RPD_Filter_Basic".Translate(), ref ctx.Inc_Basic);
            DrawRow(list1, "RPD_Filter_Race".Translate(), ref ctx.Inc_Race, ref ctx.Inc_Race_Desc);
            DrawRow(list1, "RPD_Filter_Genes".Translate(), ref ctx.Inc_Genes, ref ctx.Inc_Genes_Desc, "RPD_Tip_GenesDesc".Translate());
            DrawRow(list1, "RPD_Filter_Backstory".Translate(), ref ctx.Inc_Backstory, ref ctx.Inc_Backstory_Desc);
            DrawRow(list1, "RPD_Filter_Relations".Translate(), ref ctx.Inc_Relations);
            DrawRow(list1, "RPD_Filter_DirectorNotes".Translate(), ref ctx.Inc_DirectorNotes, "RPD_Tip_NotesDesc".Translate());
            list1.End();

            Rect col2Rect = new Rect(col1Rect.xMax + ColumnGap, startY, colWidth, 9999f);
            Listing_Standard list2 = new Listing_Standard { ColumnWidth = colWidth };
            list2.Begin(col2Rect);
            DrawHeader(list2, "RPD_Group_Traits".Translate());
            DrawRow(list2, "RPD_Filter_Traits".Translate(), ref ctx.Inc_Traits, ref ctx.Inc_Traits_Desc);
            DrawRow(list2, "RPD_Filter_Ideology".Translate(), ref ctx.Inc_Ideology, ref ctx.Inc_Ideology_Desc);
            DrawRow(list2, "RPD_Filter_Skills".Translate(), ref ctx.Inc_Skills, ref ctx.Inc_Skills_Desc);
            DrawRow(list2, "RPD_Filter_Health".Translate(), ref ctx.Inc_Health, ref ctx.Inc_Health_Desc);
            DrawRow(list2, "RPD_Filter_Equipment".Translate(), ref ctx.Inc_Equipment);
            DrawRow(list2, "RPD_Filter_Inventory".Translate(), ref ctx.Inc_Inventory);
            list2.End();

            Rect col3Rect = new Rect(col2Rect.xMax + ColumnGap, startY, colWidth, 9999f);
            Listing_Standard list3 = new Listing_Standard { ColumnWidth = colWidth };
            list3.Begin(col3Rect);
            DrawHeader(list3, "RPD_Group_ExternalData".Translate());
            DrawRow(list3, "RPD_Filter_DataComparison".Translate(), ref ctx.Inc_DataComparison);
            if (rimPsycheLoaded)
            {
                DrawRow(list3, "RPD_Filter_RimPsyche".Translate(), ref ctx.Inc_RimPsyche, ref ctx.Inc_RimPsyche_All, "RPD_Tip_RimPsyche".Translate());
            }
            if (ModsConfig.IsActive("ustas.rimai.communication.memory"))
            {
                DrawRow(list3, "RPD_Filter_Memories".Translate(), ref ctx.Inc_Memories);
                DrawRow(list3, "RPD_Filter_CommonKnowledge".Translate(), ref ctx.Inc_CommonKnowledge);
            }
            list3.End();

            float maxHeight = Mathf.Max(Mathf.Max(list1.CurHeight, list2.CurHeight), list3.CurHeight);
            listing.Gap(maxHeight);
        }

        static void DrawRow(Listing_Standard list, string label, ref bool nameSwitch, ref bool descSwitch, string descTooltip = null)
        {
            Rect rowRect = list.GetRect(24f);
            const float descCheckboxWidth = 24f;
            const float descCheckboxPadding = 5f;

            Rect mainLabelRect = new Rect(rowRect.x, rowRect.y, rowRect.width - descCheckboxWidth - descCheckboxPadding, rowRect.height);
            Widgets.CheckboxLabeled(mainLabelRect, label, ref nameSwitch);

            if (!nameSwitch) GUI.enabled = false;
            Rect descRect = new Rect(rowRect.xMax - descCheckboxWidth, rowRect.y, descCheckboxWidth, rowRect.height);
            Widgets.Checkbox(descRect.position, ref descSwitch, descCheckboxWidth, !nameSwitch);
            GUI.enabled = true;

            if (descTooltip != null) TooltipHandler.TipRegion(rowRect, descTooltip);
        }

        static void DrawRow(Listing_Standard list, string label, ref bool nameSwitch, string tooltip = null)
        {
            Rect rowRect = list.GetRect(24f);
            Widgets.CheckboxLabeled(rowRect, label, ref nameSwitch);
            if (tooltip != null) TooltipHandler.TipRegion(rowRect, tooltip);
        }

        static void DrawHeader(Listing_Standard list, string text)
        {
            GUI.color = Color.yellow;
            list.Label($"━━ {text} ━━");
            GUI.color = Color.white;
            list.Gap(2f);
        }
    }
}
