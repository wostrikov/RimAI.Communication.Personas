using System.Collections.Generic;
using Ustas.RimAI.Communication.Personas.Config;
using Ustas.RimAI.Communication.Personas.Policy;
using Verse;

namespace Ustas.RimAI.Communication.Personas.Automation
{
    /// <summary>
    /// Finds pawns whose scheduled Auto-Evolve is due. The schedule counts from
    /// the last evolve the world component recorded (manual or automatic), so
    /// pressing "Set time" in the editor also restarts it.
    /// </summary>
    internal sealed class AutoEvolveScheduler
    {
        // One in-game hour: frequent enough for a schedule counted in days.
        const int ScanIntervalTicks = 2500;
        // A request that failed or was skipped waits a day before the schedule
        // offers the pawn again, so a broken provider is not asked every hour.
        const int RetryBackoffTicks = PersonaAutomationPolicy.TicksPerDay;

        readonly Dictionary<int, int> _retryAfterTick = new Dictionary<int, int>();
        int _nextScanTick;

        public void Clear()
        {
            _retryAfterTick.Clear();
            _nextScanTick = 0;
        }

        public void Backoff(Pawn pawn, int nowTick)
        {
            if (pawn != null) _retryAfterTick[pawn.thingIDNumber] = nowTick + RetryBackoffTicks;
        }

        public void Tick(PersonaAutomationCoordinator coordinator, DirectorWorldComponent world, int nowTick)
        {
            PersonaAutomationSettings settings = PersonasMod.Settings?.Automation;
            if (settings == null || !settings.autoEvolveEnabled) return;
            // With no global schedule only pawns given their own are scanned for.
            if (settings.autoEvolveIntervalDays <= 0 && !world.AutoEvolvePawns.AnyOwnSchedule) return;
            if (nowTick < _nextScanTick) return;
            _nextScanTick = nowTick + ScanIntervalTicks;

            List<Map> maps = Find.Maps;
            if (maps == null) return;
            foreach (Map map in maps)
            {
                foreach (Pawn pawn in map.mapPawns.AllHumanlikeSpawned)
                {
                    if (!AutoEvolveEligibility.IsEligible(pawn, settings)) continue;

                    int last = world.GetLastEvolveTick(pawn);
                    if (last < 0)
                    {
                        // First sighting anchors the schedule and records the data
                        // snapshot the first evolve will be compared against.
                        world.SetTimestamp(pawn);
                        continue;
                    }

                    if (_retryAfterTick.TryGetValue(pawn.thingIDNumber, out int retryAt) && nowTick < retryAt) continue;
                    int days = world.AutoEvolvePawns.IntervalFor(pawn, settings.autoEvolveIntervalDays);
                    if (!PersonaAutomationPolicy.IsEvolveDue(last, nowTick, days)) continue;

                    coordinator.Enqueue(pawn, PersonaAutomationKind.Evolve, false, "schedule:" + pawn.thingIDNumber + ":" + last, null);
                }
            }
        }
    }
}
