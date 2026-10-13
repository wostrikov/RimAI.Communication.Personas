using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;
using Ustas.RimAI.Core.Personas;
using Ustas.RimAI.Communication.Personas.Automation;
using Ustas.RimAI.Communication.Personas.History;

namespace Ustas.RimAI.Communication.Personas
{
    public class DirectorWorldComponent : WorldComponent
    {
        private PersonaHistoryStore _history = new PersonaHistoryStore();

        public PersonaHistoryStore History => _history;

        private PawnAutoEvolveRegistry _autoEvolvePawns = new PawnAutoEvolveRegistry();

        /// <summary>Per-pawn Auto-Evolve choices for this game.</summary>
        public PawnAutoEvolveRegistry AutoEvolvePawns => _autoEvolvePawns;

        private Dictionary<int, int> _lastEvolveTicks = new Dictionary<int, int>();
        private Dictionary<int, long> _lastEvolveBioAgeTicks = new Dictionary<int, long>(); 
        private Dictionary<int, string> _dataSnapshots = new Dictionary<int, string>();
        private Dictionary<int, string> _dailySnapshots = new Dictionary<int, string>();
        private Dictionary<int, int> _dailySnapshotDays = new Dictionary<int, int>();

        public int lastRuleCheckTick = 0;

        // When the per-pawn records are next checked against the pawns the game
        // still holds. Not saved: a load checks on its first tick, see FinalizeInit.
        private int _nextPruneTick = int.MaxValue;

        public DirectorWorldComponent(World world) : base(world) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref _lastEvolveTicks, PersonaScribeLabels.WorldComponent.LastEvolveTicks, LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref _lastEvolveBioAgeTicks, PersonaScribeLabels.WorldComponent.LastEvolveBioAgeTicks, LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref _dataSnapshots, PersonaScribeLabels.WorldComponent.DataSnapshots, LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref _dailySnapshots, PersonaScribeLabels.WorldComponent.DailySnapshots, LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref _dailySnapshotDays, PersonaScribeLabels.WorldComponent.DailySnapshotDays, LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref lastRuleCheckTick, PersonaScribeLabels.WorldComponent.LastRuleCheckTick, 0);
            Scribe_Deep.Look(ref _history, "personaHistory");
            Scribe_Deep.Look(ref _autoEvolvePawns, "autoEvolvePawns");
            if (Scribe.mode == LoadSaveMode.LoadingVars && _autoEvolvePawns == null) _autoEvolvePawns = new PawnAutoEvolveRegistry();
            // World.FinalizeInit runs before the loader's PostLoadInit pass, so a save
            // written before persona history existed must have its store now, not there.
            if (Scribe.mode == LoadSaveMode.LoadingVars && _history == null) _history = new PersonaHistoryStore();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (_history == null) _history = new PersonaHistoryStore();
                if (_lastEvolveTicks == null) _lastEvolveTicks = new Dictionary<int, int>();
                if (_lastEvolveBioAgeTicks == null) _lastEvolveBioAgeTicks = new Dictionary<int, long>();
                if (_dataSnapshots == null) _dataSnapshots = new Dictionary<int, string>();
                if (_dailySnapshots == null) _dailySnapshots = new Dictionary<int, string>();
                if (_dailySnapshotDays == null) _dailySnapshotDays = new Dictionary<int, int>();
            }
        }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            // Not pruned here: on a load this runs before the maps are read, so every
            // pawn on a map would look missing and lose its history. The first tick
            // knows them all. A new game starts at tick zero with nothing to prune.
            _nextPruneTick = fromLoad ? 0 : GenDate.TicksPerDay;
            if (!PersonasComposition.Current.IsStarted) return;
            // A new game or a loaded save: whatever the automation held belongs
            // to the world before this one.
            PersonasComposition.Current.Automation.BindWorld(world);
        }

        // The game already guards each world component's tick and logs what it
        // throws. The automation takes every job and event off its queue before
        // working on it, so a fault drops that one item instead of repeating.
        public override void WorldComponentTick()
        {
            base.WorldComponentTick();
            int now = GenTicks.TicksGame;
            if (now >= _nextPruneTick)
            {
                _nextPruneTick = now + GenDate.TicksPerDay;
                RemoveRecordsOfMissingPawns();
            }
            if (PersonasComposition.Current.IsStarted)
                PersonasComposition.Current.Automation.Tick(this);
        }

        /// <summary>
        /// Forgets every pawn the game no longer holds. A prisoner Auto-Evolve had
        /// anchored, a visitor whose daily status a dialogue asked for: each left
        /// a timestamp and a character sheet here, and until 2026-10-13 only the
        /// history and the Auto-Evolve choices were ever cleared - once, after a
        /// load - so a long colony's save kept a sheet for pawns long gone.
        /// </summary>
        void RemoveRecordsOfMissingPawns()
        {
            HashSet<int> known = PersonaPawnCensus.KnownIds();
            _history.RemoveMissingPawns(known);
            _autoEvolvePawns.RemoveWhere(id => !known.Contains(id));
            RemoveMissingKeys(_lastEvolveTicks, known);
            RemoveMissingKeys(_lastEvolveBioAgeTicks, known);
            RemoveMissingKeys(_dataSnapshots, known);
            RemoveMissingKeys(_dailySnapshots, known);
            RemoveMissingKeys(_dailySnapshotDays, known);
        }

        static void RemoveMissingKeys<T>(Dictionary<int, T> byPawn, HashSet<int> known)
        {
            if (byPawn.Count == 0) return;
            List<int> missing = null;
            foreach (int id in byPawn.Keys)
            {
                if (!known.Contains(id)) (missing ??= new List<int>()).Add(id);
            }
            if (missing == null) return;
            foreach (int id in missing) byPawn.Remove(id);
        }

        public void SaveDailySnapshot(Pawn p)
        {
            if (p == null) return;
            string snapshot = DirectorUtils.BuildCustomCharacterData(p, isSnapshot: true, simpleEquipment: true);

            _dailySnapshots[p.thingIDNumber] = snapshot;
            _dailySnapshotDays[p.thingIDNumber] = GenDate.DaysPassed;
        }

        public string GetOrUpdateDailyDiff(Pawn p)
        {
            if (p == null) return "";
            int id = p.thingIDNumber;
            int currentDay = GenDate.DaysPassed;

            string currentSnapshot = DirectorUtils.BuildCustomCharacterData(p, true, true);

            if (!_dailySnapshots.TryGetValue(id, out string storedSnapshot))
            {
                _dailySnapshots[id] = currentSnapshot;
                _dailySnapshotDays[id] = currentDay;
                return "Daily monitoring started just now.";
            }

            string diff = DirectorUtils.GenerateDiffReport(storedSnapshot, currentSnapshot);

            int storedDay = _dailySnapshotDays.TryGetValue(id, out int val) ? val : -1;

            if (currentDay > storedDay)
            {
                _dailySnapshots[id] = currentSnapshot;
                _dailySnapshotDays[id] = currentDay;
                return diff == "No significant changes." ? "No changes since yesterday." : diff;
            }

            return diff == "No significant changes." ? "No changes today." : diff;
        }

        /// <summary>
        /// Records now as the moment the pawn's persona was last brought up to
        /// date, with the character sheet the next update is compared against.
        /// A caller that has just built that sheet hands it over; it used to be
        /// accepted and then built a second time.
        /// </summary>
        public void SetTimestamp(Pawn p, string snapshotData = null)
        {
            if (p == null) return;
            string snapshot = snapshotData ?? DirectorUtils.BuildCustomCharacterData(p, isSnapshot: true, simpleEquipment: false);

            int id = p.thingIDNumber;
            _lastEvolveTicks[id] = GenTicks.TicksGame;
            _lastEvolveBioAgeTicks[id] = p.ageTracker.AgeBiologicalTicks;
            _dataSnapshots[id] = snapshot;
        }

        public string GetSnapshot(Pawn p)
        {
            if (p != null && _dataSnapshots.TryGetValue(p.thingIDNumber, out string data))
            {
                return data;
            }
            return null;
        }

        public int GetLastEvolveTick(Pawn p)
        {
            if (p != null && _lastEvolveTicks.TryGetValue(p.thingIDNumber, out int tick)) return tick;
            return -1;
        }

        public long GetLastEvolveBioAgeTicks(Pawn p)
        {
            if (p != null && _lastEvolveBioAgeTicks.TryGetValue(p.thingIDNumber, out long ageTicks)) return ageTicks;
            return -1;
        }
    }
}