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
    /// but greyed while the setting that covers them is off.
    /// </summary>
    public sealed class Window_AutoEvolveRoster : Window
    {
        const float RowHeight = 30f;
        const float NameWidth = 190f;
        const float RoleWidth = 110f;
        const float CheckWidth = 90f;
        const float IntervalWidth = 130f;

        readonly PersonaAutomationSettings _settings;
        readonly DirectorWorldComponent _world;
        readonly PawnRosterView _roster = new PawnRosterView();
        readonly Dictionary<int, string> _intervalBuffers = new Dictionary<int, string>();
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

        public override Vector2 InitialSize => new Vector2(720f, 560f);

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
            }

            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(inRect.x, inRect.y + 36f, inRect.width, 20f),
                "RPD_AutoEvolve_RosterHint".Translate(_settings.autoEvolveIntervalDays));
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            DrawHeader(new Rect(inRect.x, inRect.y + 58f, inRect.width, 24f));
            Rect body = new Rect(inRect.x, inRect.y + 84f, inRect.width, inRect.height - 84f - 50f);
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

        static void DrawHeader(Rect row)
        {
            GUI.color = Color.gray;
            float x = row.x + 4f;
            Header(ref x, row, NameWidth, "RPD_AutoEvolve_RosterPawn".Translate(), null);
            Header(ref x, row, RoleWidth, "RPD_AutoEvolve_RosterRole".Translate(), null);
            Header(ref x, row, CheckWidth, "RPD_AutoEvolve_RosterEnabled".Translate(), "RPD_AutoEvolve_RosterEnabledTip".Translate());
            Header(ref x, row, IntervalWidth, "RPD_AutoEvolve_RosterInterval".Translate(), "RPD_AutoEvolve_RosterIntervalTip".Translate());
            Header(ref x, row, row.xMax - x, "RPD_AutoEvolve_RosterLast".Translate(), null);
            GUI.color = Color.white;
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
                string digits = new string(System.Array.FindAll(edited.ToCharArray(), char.IsDigit));
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
