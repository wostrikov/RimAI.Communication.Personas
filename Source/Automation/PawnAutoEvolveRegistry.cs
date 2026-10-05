using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Personas.Policy;
using Verse;

namespace Ustas.RimAI.Communication.Personas.Automation
{
    /// <summary>One pawn's departure from the Auto-Evolve defaults.</summary>
    public sealed class PawnAutoEvolveOverride : IExposable
    {
        public const int UseDefaultInterval = -1;

        public bool excluded;
        // Days between scheduled evolves for this pawn; -1 follows the global
        // interval, 0 means events only.
        public int intervalDays = UseDefaultInterval;

        public bool IsDefault => !excluded && intervalDays == UseDefaultInterval;

        public void ExposeData()
        {
            Scribe_Values.Look(ref excluded, "excluded", false);
            Scribe_Values.Look(ref intervalDays, "intervalDays", UseDefaultInterval);
        }
    }

    /// <summary>
    /// Per-pawn Auto-Evolve choices for one game: leave a pawn out entirely, or
    /// give it its own schedule. Saved with the world component; a pawn with no
    /// entry follows the global settings, so the registry only ever holds
    /// departures from them.
    /// </summary>
    public sealed class PawnAutoEvolveRegistry : IExposable
    {
        Dictionary<int, PawnAutoEvolveOverride> _overrides = new Dictionary<int, PawnAutoEvolveOverride>();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref _overrides, "overrides", LookMode.Value, LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (_overrides == null) _overrides = new Dictionary<int, PawnAutoEvolveOverride>();
                var drop = new List<int>();
                foreach (KeyValuePair<int, PawnAutoEvolveOverride> entry in _overrides)
                {
                    if (entry.Value == null || entry.Value.IsDefault) drop.Add(entry.Key);
                    else if (entry.Value.intervalDays != PawnAutoEvolveOverride.UseDefaultInterval)
                        entry.Value.intervalDays = PersonaAutomationPolicy.ClampIntervalDays(entry.Value.intervalDays);
                }
                foreach (int id in drop) _overrides.Remove(id);
            }
        }

        public bool IsExcluded(Pawn pawn) =>
            pawn != null && _overrides.TryGetValue(pawn.thingIDNumber, out PawnAutoEvolveOverride entry) && entry.excluded;

        /// <summary>The pawn's own interval, or -1 when it follows the global one.</summary>
        public int OwnInterval(Pawn pawn) =>
            pawn != null && _overrides.TryGetValue(pawn.thingIDNumber, out PawnAutoEvolveOverride entry)
                ? entry.intervalDays
                : PawnAutoEvolveOverride.UseDefaultInterval;

        public int IntervalFor(Pawn pawn, int globalDays)
        {
            int own = OwnInterval(pawn);
            return own == PawnAutoEvolveOverride.UseDefaultInterval ? globalDays : own;
        }

        public bool AnyOwnSchedule
        {
            get
            {
                foreach (PawnAutoEvolveOverride entry in _overrides.Values)
                    if (!entry.excluded && entry.intervalDays > 0) return true;
                return false;
            }
        }

        public void SetExcluded(Pawn pawn, bool excluded) => Update(pawn, entry => entry.excluded = excluded);

        public void SetInterval(Pawn pawn, int days) =>
            Update(pawn, entry => entry.intervalDays = days < 0
                ? PawnAutoEvolveOverride.UseDefaultInterval
                : PersonaAutomationPolicy.ClampIntervalDays(days));

        /// <summary>Forgets pawns the game no longer has.</summary>
        public void RemoveWhere(Func<int, bool> isGone)
        {
            var drop = new List<int>();
            foreach (int id in _overrides.Keys)
                if (isGone(id)) drop.Add(id);
            foreach (int id in drop) _overrides.Remove(id);
        }

        void Update(Pawn pawn, Action<PawnAutoEvolveOverride> change)
        {
            if (pawn == null) return;
            if (!_overrides.TryGetValue(pawn.thingIDNumber, out PawnAutoEvolveOverride entry))
                entry = new PawnAutoEvolveOverride();
            change(entry);
            if (entry.IsDefault) _overrides.Remove(pawn.thingIDNumber);
            else _overrides[pawn.thingIDNumber] = entry;
        }
    }
}
