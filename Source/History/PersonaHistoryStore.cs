using System.Collections.Generic;
using RimWorld;
using Ustas.RimAI.Communication.Personas.Policy;
using Verse;

namespace Ustas.RimAI.Communication.Personas.History
{
    /// <summary>
    /// Persona history for every pawn, saved inside <see cref="DirectorWorldComponent"/>.
    /// Bounded twice: each pawn keeps at most the configured number of records
    /// (<see cref="PersonaHistoryPolicy.MaxMaxRecords"/> at the most), and pawns
    /// that no longer exist anywhere are dropped when the world finishes loading.
    /// </summary>
    public sealed class PersonaHistoryStore : IExposable
    {
        Dictionary<int, PersonaPawnHistory> _byPawn = new Dictionary<int, PersonaPawnHistory>();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref _byPawn, "byPawn", LookMode.Value, LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars) _byPawn ??= new Dictionary<int, PersonaPawnHistory>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _byPawn ??= new Dictionary<int, PersonaPawnHistory>();
                PruneAll(PersonasMod.Settings?.Automation?.historyMaxRecords ?? PersonaHistoryPolicy.DefaultMaxRecords);
            }
        }

        public bool HasRecords(Pawn pawn)
        {
            return pawn != null
                && _byPawn.TryGetValue(pawn.thingIDNumber, out PersonaPawnHistory history)
                && history?.records != null
                && history.records.Count > 0;
        }

        /// <summary>The pawn's records, newest first. A live list: callers only read it.</summary>
        public IReadOnlyList<PersonaHistoryRecord> GetRecords(Pawn pawn)
        {
            if (pawn != null
                && _byPawn.TryGetValue(pawn.thingIDNumber, out PersonaPawnHistory history)
                && history?.records != null)
            {
                return history.records;
            }
            return System.Array.Empty<PersonaHistoryRecord>();
        }

        public void Add(Pawn pawn, PersonaHistoryRecord record, int maxRecords)
        {
            if (pawn == null || record == null) return;
            if (!_byPawn.TryGetValue(pawn.thingIDNumber, out PersonaPawnHistory history) || history == null)
            {
                history = new PersonaPawnHistory();
                _byPawn[pawn.thingIDNumber] = history;
            }
            history.records.Insert(0, record);
            PersonaHistoryPolicy.TrimNewestFirst(history.records, maxRecords);
        }

        public void PruneAll(int maxRecords)
        {
            var empty = new List<int>();
            foreach (KeyValuePair<int, PersonaPawnHistory> pair in _byPawn)
            {
                List<PersonaHistoryRecord> records = pair.Value?.records;
                if (records == null || records.Count == 0)
                {
                    empty.Add(pair.Key);
                    continue;
                }
                PersonaHistoryPolicy.TrimNewestFirst(records, maxRecords);
            }
            foreach (int id in empty) _byPawn.Remove(id);
        }

        /// <summary>Drops the history of pawns the game no longer knows, alive or dead.</summary>
        public void RemoveMissingPawns()
        {
            if (_byPawn.Count == 0) return;
            var known = new HashSet<int>();
            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)
            {
                if (pawn != null) known.Add(pawn.thingIDNumber);
            }

            var missing = new List<int>();
            foreach (int id in _byPawn.Keys)
            {
                if (!known.Contains(id)) missing.Add(id);
            }
            foreach (int id in missing) _byPawn.Remove(id);
        }
    }
}
