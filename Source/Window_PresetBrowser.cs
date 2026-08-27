using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Data;

namespace Ustas.RimAI.Communication.Personas
{
    public class Window_PresetBrowser : Window
    {
        private Pawn pawnToApply;
        private Window editorWindow;

        private Vector2 categoryScroll, presetScroll;
        private string selectedCategory = "All";
        private CustomPreset selectedPreset;

        public Window_PresetBrowser(Pawn pawn, Window editor)
        {
            pawnToApply = pawn;
            editorWindow = editor;
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;

            if (PersonasMod.Settings.userPresets == null)
            {
                PersonasMod.Settings.userPresets = new List<CustomPreset>();
            }

        }

        public override Vector2 InitialSize => new Vector2(700f, 500f);

        public override void DoWindowContents(Rect inRect)
        {
            Rect leftRect = inRect.LeftPart(0.3f).Rounded();
            Rect rightRect = inRect.RightPart(0.68f).Rounded();

            DrawCategoryList(leftRect);

            DrawPresetArea(rightRect);
        }

        private void DrawCategoryList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect innerRect = rect.ContractedBy(5f);

            var allCategories = PersonasMod.Settings.userPresets
                .Select(p => p.category ?? "Default").Distinct().OrderBy(c => c).ToList();
            allCategories.Insert(0, "All");

            float rowHeight = 30f;
            float viewHeight = allCategories.Count * rowHeight;
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, viewHeight);

            Widgets.BeginScrollView(innerRect, ref categoryScroll, viewRect);

            for (int i = 0; i < allCategories.Count; i++)
            {
                string cat = allCategories[i];
                Rect rowRect = new Rect(0f, i * rowHeight, viewRect.width, rowHeight);

                if (DrawHighlightButton(rowRect, cat, selectedCategory == cat))
                {
                    selectedCategory = cat;
                    selectedPreset = null;
                }
            }

            Widgets.EndScrollView();
        }

        private bool DrawHighlightButton(Rect rect, string label, bool highlighted)
        {
            if (highlighted)
            {
                Widgets.DrawHighlightSelected(rect);
            }
            else if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect.ContractedBy(5f), label);
            Text.Anchor = TextAnchor.UpperLeft;

            return Widgets.ButtonInvisible(rect);
        }

        private void DrawPresetArea(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect innerRect = rect.ContractedBy(8f);

            Listing_Standard listing = new Listing_Standard();
            // Verse wraps a Listing into a second column, off the visible view, as soon as
            // content passes the rect height, and CurHeight then reports that new column.
            // A scrolling settings page never wants that; see validate_scrollable_listings.
            listing.maxOneColumn = true;
            listing.Begin(innerRect);

            float listHeight = innerRect.height * 0.55f;
            Rect listOutRect = listing.GetRect(listHeight);

            var presetsToShow = PersonasMod.Settings.userPresets
                .Where(p => selectedCategory == "All" || (p.category ?? "Default") == selectedCategory)
                .ToList();

            float rowHeight = 28f;
            Rect viewRect = new Rect(0f, 0f, listOutRect.width - 16f, presetsToShow.Count * rowHeight);

            Widgets.BeginScrollView(listOutRect, ref presetScroll, viewRect);
            for (int i = 0; i < presetsToShow.Count; i++)
            {
                var p = presetsToShow[i];
                Rect row = new Rect(0f, i * rowHeight, viewRect.width, rowHeight);
                if (selectedPreset == p) Widgets.DrawHighlightSelected(row);
                if (Widgets.ButtonInvisible(row)) selectedPreset = p;
                string label = p.label;
                if (!p.enabled)
                {
                    GUI.color = Color.gray;
                }

                Widgets.Label(row.ContractedBy(4f), p.label);
                GUI.color = Color.white;
            }
            Widgets.EndScrollView();

            listing.Gap(5f);

            float descHeight = innerRect.height - listing.CurHeight - 45f;
            Rect descRect = listing.GetRect(descHeight);
            Widgets.DrawMenuSection(descRect);
            if (selectedPreset != null)
            {
                Widgets.Label(descRect.ContractedBy(8f), $"{selectedPreset.personaText}");
            }

            listing.End();

            Rect bottomRow = new Rect(rect.x, rect.yMax - 40f, rect.width, 30f);
            float btnWidth = Mathf.Min(200f, (bottomRow.width - 10f) / 2f);
            float startX = bottomRow.x + (bottomRow.width - (btnWidth * 2 + 10f)) / 2f;

            Rect applyRect = new Rect(startX, bottomRow.y, btnWidth, 30f);
            bool originalGUIState = GUI.enabled;
            if (selectedPreset == null) GUI.enabled = false;
            if (Widgets.ButtonText(applyRect, "RPD_Browser_ApplySelected".Translate()))
            {
                ApplyAndClose(selectedPreset);
            }
            GUI.enabled = originalGUIState;

            Rect randomRect = new Rect(applyRect.xMax + 10f, bottomRow.y, btnWidth, 30f);
            if (Widgets.ButtonText(randomRect, "RPD_Browser_ApplyRandom".Translate()))
            {
                if (presetsToShow.Any())
                {
                    ApplyAndClose(presetsToShow.RandomElement());
                }
            }
        }

        private void ApplyAndClose(CustomPreset preset)
        {
            DirectorUtils.ApplyPersonalityToPawn(pawnToApply, new PersonalityData(preset.personaText, preset.chattiness));

            DirectorUtils.SetWindowText(editorWindow, preset.personaText);

            LongEventHandler.ExecuteWhenFinished(() => this.Close());
        }
    }
}
