using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Ustas.RimAI.Communication.Personas.Automation;
using Ustas.RimAI.Communication.Personas.Config;
using Verse;

namespace Ustas.RimAI.Communication.Personas.UI
{
    /// <summary>
    /// Auto-Evolve pawn by pawn: leave one out, or give it its own schedule.
    /// Lists the pawns Auto-Evolve can cover on every map; captives are listed
    /// but greyed while the setting that covers them is off. Ticked rows take
    /// the bar's actions together, which is what makes forty colonists workable.
    /// </summary>
    public sealed class Window_AutoEvolveRoster : Window
    {
        const float RowHeight = 30f;
        const float SelectWidth = 30f;
        const float NameWidth = 200f;
        const float RoleWidth = 120f;
        const float CheckWidth = 100f;
        const float IntervalWidth = 140f;
        const float BarHeight = 26f;
        const float BarGap = 6f;

        readonly PersonaAutomationSettings _settings;
        readonly DirectorWorldComponent _world;
        readonly PawnRosterView _roster = new PawnRosterView();
        readonly Dictionary<int, string> _intervalBuffers = new Dictionary<int, string>();
        // The ticked rows, by pawn id; kept across the search so a selection
        // can be gathered from several searches.
        readonly HashSet<int> _selected = new HashSet<int>();
        string _bulkInterval = string.Empty;
        List<Pawn> _pawns;
        int _refreshAtFrame;

        public Window_AutoEvolveRoster(PersonaAutomationSettings settings, DirectorWorldComponent world)
        {
            _settings = settings;
            _world = world;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(860f, 580f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "RPD_AutoEvolve_RosterTitle".Translate());
            Text.Font = GameFont.Small;

            if (_world == null || Current.ProgramState != ProgramState.Playing)
            {
                Widgets.Label(new Rect(inRect.x, inRect.y + 40f, inRect.width, 60f), "RPD_AutoEvolve_RosterNoGame".Translate());
                return;
            }

            if (_pawns == null || Time.frameCount >= _refreshAtFrame)
            {
                _pawns = CollectPawns();
                _refreshAtFrame = Time.frameCount + 120;
                ForgetSelectionOfMissingPawns();
            }

            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(inRect.x, inRect.y + 36f, inRect.width, 20f),
                "RPD_AutoEvolve_RosterHint".Translate(_settings.autoEvolveIntervalDays));
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            DrawBulkBar(new Rect(inRect.x, inRect.y + 58f, inRect.width, BarHeight));
            DrawHeader(new Rect(inRect.x, inRect.y + 90f, inRect.width, 24f));
            Rect body = new Rect(inRect.x, inRect.y + 116f, inRect.width, inRect.height - 116f - 50f);
            _roster.Draw(body, _pawns, RowHeight, DrawRow);
        }

        List<Pawn> CollectPawns()
        {
            var pawns = new List<Pawn>();
            foreach (Map map in Find.Maps)
            {
                foreach (Pawn pawn in map.mapPawns.AllHumanlikeSpawned)
                {
                    if (!AutoEvolveEligibility.IsLivePersonaPawn(pawn)) continue;
                    PawnRoleCategory role = PawnRoleClassifier.Classify(pawn);
                    if (role == PawnRoleCategory.Colonist || role == PawnRoleCategory.Prisoner || role == PawnRoleCategory.Slave)
                        pawns.Add(pawn);
                }
            }
            pawns.SortBy(p => (int)PawnRoleClassifier.Classify(p), p => p.LabelShortCap);
            return pawns;
        }

        void ForgetSelectionOfMissingPawns()
        {
            if (_selected.Count == 0) return;
            var listed = new HashSet<int>();
            foreach (Pawn pawn in _pawns) listed.Add(pawn.thingIDNumber);
            _selected.RemoveWhere(id => !listed.Contains(id));
        }

        /// <summary>What the ticked rows do together: in or out of Auto-Evolve, and one interval for all.</summary>
        void DrawBulkBar(Rect row)
        {
            PawnAutoEvolveRegistry registry = _world.AutoEvolvePawns;
            bool any = _selected.Count > 0;
            float x = row.x;

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(x, row.y, 120f, row.height), "RPD_AutoEvolve_SelectedCount".Translate(_selected.Count));
            Text.Anchor = TextAnchor.UpperLeft;
            x += 120f + BarGap;

            if (BarButton(ref x, row, 100f, "RPD_AutoEvolve_EnableSelected", any))
                ForSelected(pawn => registry.SetExcluded(pawn, false));
            if (BarButton(ref x, row, 100f, "RPD_AutoEvolve_DisableSelected", any))
                ForSelected(pawn => registry.SetExcluded(pawn, true));

            x += BarGap;
            Rect intervalStart = new Rect(x, row.y, 0f, row.height);
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(x, row.y, 120f, row.height), "RPD_AutoEvolve_BatchInterval".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            x += 120f + BarGap;
            _bulkInterval = DigitsOnly(Widgets.TextField(new Rect(x, row.y + 1f, 56f, row.height - 2f), _bulkInterval));
            x += 56f + BarGap;
            if (BarButton(ref x, row, 110f, "RPD_AutoEvolve_ApplyInterval", any))
            {
                int days = _bulkInterval.Length == 0 ? PawnAutoEvolveOverride.UseDefaultInterval : int.Parse(_bulkInterval);
                ForSelected(pawn =>
                {
                    registry.SetInterval(pawn, days);
                    // The row shows what it was typed; drop that so it shows what it is now.
                    _intervalBuffers.Remove(pawn.thingIDNumber);
                });
            }
            intervalStart.xMax = x - BarGap;
            TooltipHandler.TipRegion(intervalStart, "RPD_AutoEvolve_BatchIntervalTip".Translate());

            x += BarGap;
            if (BarButton(ref x, row, 130f, "RPD_AutoEvolve_ClearSelection", any))
                _selected.Clear();
        }

        static bool BarButton(ref float x, Rect row, float width, string key, bool active)
        {
            bool clicked = Widgets.ButtonText(new Rect(x, row.y, width, row.height), key.Translate(), active: active);
            x += width + BarGap;
            return clicked && active;
        }

        void ForSelected(Action<Pawn> change)
        {
            foreach (Pawn pawn in _pawns)
            {
                if (_selected.Contains(pawn.thingIDNumber)) change(pawn);
            }
        }

        // Four digits is past the longest interval the setting accepts, and
        // keeps the number inside an int whatever is pasted.
        static string DigitsOnly(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string digits = new string(Array.FindAll(text.ToCharArray(), char.IsDigit));
            return digits.Length > 4 ? digits.Substring(0, 4) : digits;
        }

        void DrawHeader(Rect row)
        {
            DrawSelectAll(new Rect(row.x + 4f, row.y, SelectWidth, row.height));

            GUI.color = Color.gray;
            float x = row.x + 4f + SelectWidth;
            Header(ref x, row, NameWidth, "RPD_AutoEvolve_RosterPawn".Translate(), null);
            Header(ref x, row, RoleWidth, "RPD_AutoEvolve_RosterRole".Translate(), null);
            Header(ref x, row, CheckWidth, "RPD_AutoEvolve_RosterEnabled".Translate(), "RPD_AutoEvolve_RosterEnabledTip".Translate());
            Header(ref x, row, IntervalWidth, "RPD_AutoEvolve_RosterInterval".Translate(), "RPD_AutoEvolve_RosterIntervalTip".Translate());
            Header(ref x, row, row.xMax - x, "RPD_AutoEvolve_RosterLast".Translate(), null);
            GUI.color = Color.white;
        }

        /// <summary>Ticks or clears every row the search is showing; rows it hides keep their own state.</summary>
        void DrawSelectAll(Rect rect)
        {
            IReadOnlyList<Pawn> shown = _roster.Shown;
            bool all = shown.Count > 0;
            foreach (Pawn pawn in shown)
            {
                if (!_selected.Contains(pawn.thingIDNumber))
                {
                    all = false;
                    break;
                }
            }

            bool ticked = all;
            Widgets.Checkbox(new Vector2(rect.x, rect.y), ref ticked, disabled: shown.Count == 0);
            if (ticked != all)
            {
                foreach (Pawn pawn in shown)
                {
                    if (ticked) _selected.Add(pawn.thingIDNumber);
                    else _selected.Remove(pawn.thingIDNumber);
                }
            }
            TooltipHandler.TipRegion(rect, "RPD_AutoEvolve_SelectAllTip".Translate());
        }

        static void Header(ref float x, Rect row, float width, string text, string tip)
        {
            Rect rect = new Rect(x, row.y, width, row.height);
            Widgets.Label(rect, text);
            if (tip != null) TooltipHandler.TipRegion(rect, tip);
            x += width;
        }

        void DrawRow(Rect row, Pawn pawn)
        {
            PawnAutoEvolveRegistry registry = _world.AutoEvolvePawns;
            PawnRoleCategory role = PawnRoleClassifier.Classify(pawn);
            bool covered = role == PawnRoleCategory.Colonist || _settings.autoEvolveIncludeCaptives;

            float x = row.x + 4f;
            bool selected = _selected.Contains(pawn.thingIDNumber);
            bool wasSelected = selected;
            Widgets.Checkbox(new Vector2(x, row.y + 3f), ref selected);
            if (selected != wasSelected)
            {
                if (selected) _selected.Add(pawn.thingIDNumber);
                else _selected.Remove(pawn.thingIDNumber);
            }
            x += SelectWidth;

            Text.Anchor = TextAnchor.MiddleLeft;
            if (!covered) GUI.color = Color.gray;
            Widgets.Label(new Rect(x, row.y, NameWidth, row.height), pawn.LabelShortCap);
            x += NameWidth;
            Widgets.Label(new Rect(x, row.y, RoleWidth, row.height), PawnRoleClassifier.Label(role));
            x += RoleWidth;
            Text.Anchor = TextAnchor.UpperLeft;

            bool included = !registry.IsExcluded(pawn);
            bool before = included;
            Widgets.Checkbox(new Vector2(x + 20f, row.y + 3f), ref included, disabled: !covered);
            if (included != before) registry.SetExcluded(pawn, !included);
            x += CheckWidth;

            DrawInterval(new Rect(x, row.y + 3f, IntervalWidth - 10f, 24f), pawn, registry);
            x += IntervalWidth;

            int last = _world.GetLastEvolveTick(pawn);
            string lastText = last < 0
                ? "RPD_AutoEvolve_RosterNever".Translate().ToString()
                : "RPD_AutoEvolve_RosterDaysAgo".Translate(Mathf.Max(0, (Find.TickManager.TicksGame - last) / GenDate.TicksPerDay)).ToString();
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(x, row.y, row.xMax - x, row.height), lastText);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        /// <summary>Empty means the global interval; a number of days is this pawn's own, 0 meaning events only.</summary>
        void DrawInterval(Rect rect, Pawn pawn, PawnAutoEvolveRegistry registry)
        {
            int id = pawn.thingIDNumber;
            if (!_intervalBuffers.TryGetValue(id, out string buffer))
            {
                int own = registry.OwnInterval(pawn);
                buffer = own == PawnAutoEvolveOverride.UseDefaultInterval ? string.Empty : own.ToString();
            }
            string edited = Widgets.TextField(rect, buffer);
            if (edited != buffer)
            {
                string digits = DigitsOnly(edited);
                _intervalBuffers[id] = digits;
                registry.SetInterval(pawn, digits.Length == 0 ? PawnAutoEvolveOverride.UseDefaultInterval : int.Parse(digits));
            }
            else
            {
                _intervalBuffers[id] = buffer;
            }
            if (buffer.NullOrEmpty())
            {
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(rect.x + 6f, rect.y, rect.width - 6f, rect.height),
                    "RPD_AutoEvolve_RosterDefault".Translate(_settings.autoEvolveIntervalDays));
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
            TooltipHandler.TipRegion(rect, "RPD_AutoEvolve_RosterIntervalTip".Translate());
        }
    }
}
