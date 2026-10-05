using System;
using System.Collections.Generic;

namespace Ustas.RimAI.Communication.Personas.Policy
{
    /// <summary>
    /// Host-free rules for the automatic persona work: when a scheduled evolve
    /// is due, how triggers merge into one request, and how an evolve result is
    /// folded into the persona it started from.
    /// </summary>
    public static class PersonaAutomationPolicy
    {
        public const int TicksPerDay = 60000;
        public const int MaxIntervalDays = 3600;
        public const int MaxTriggerContexts = 8;

        // The same tag the manual Evolve button appends, so a persona reads the
        // same whichever of the two wrote its development lines.
        public const string DevelopmentTag = "[Development]:";

        // Superfast: the speed at which a player is skipping time rather than watching it.
        public const int DefaultPauseAtSpeed = 3;
        public const int MaxPauseAtSpeed = 4;

        public static int ClampPauseAtSpeed(int speed)
        {
            if (speed <= 0) return 0;
            if (speed < 2) return 2;
            return speed > MaxPauseAtSpeed ? MaxPauseAtSpeed : speed;
        }

        /// <summary>Whether a new request waits because the game runs at the pause speed or faster.</summary>
        public static bool IsPausedAtSpeed(int currentSpeed, int pauseAtSpeed)
        {
            int threshold = ClampPauseAtSpeed(pauseAtSpeed);
            return threshold > 0 && currentSpeed >= threshold;
        }

        public static int ClampIntervalDays(int days)
        {
            if (days < 0) return 0;
            if (days > MaxIntervalDays) return MaxIntervalDays;
            return days;
        }

        /// <summary>
        /// Zero days means no schedule: the pawn evolves on events only. A pawn
        /// with no recorded evolve is not due; the scheduler anchors it first.
        /// </summary>
        public static bool IsEvolveDue(int lastEvolveTick, int nowTick, int intervalDays)
        {
            int days = ClampIntervalDays(intervalDays);
            if (days == 0 || lastEvolveTick < 0) return false;
            return (long)nowTick - lastEvolveTick >= (long)days * TicksPerDay;
        }

        /// <summary>
        /// Adds one trigger to a pending request. A key seen before is ignored,
        /// and the context list is capped so a busy pawn cannot grow one prompt
        /// without bound. Returns whether the key was new.
        /// </summary>
        public static bool TryAddTrigger(List<string> keys, List<string> contexts, string key, string context)
        {
            if (keys == null || contexts == null || string.IsNullOrWhiteSpace(key)) return false;
            if (keys.Contains(key)) return false;
            keys.Add(key);
            string text = (context ?? string.Empty).Trim();
            if (text.Length > 0 && contexts.Count < MaxTriggerContexts && !contexts.Contains(text))
                contexts.Add(text);
            return true;
        }

        public static string ComposeEvolved(string original, string generated, bool overwrite)
        {
            string update = (generated ?? string.Empty).Trim();
            if (update.Length == 0) return (original ?? string.Empty).Trim();
            if (overwrite) return update;
            string start = (original ?? string.Empty).Trim();
            return start.Length == 0 ? update : start + "\n\n" + DevelopmentTag + " " + update;
        }
    }

    /// <summary>
    /// Remembers which trigger keys were accepted recently, so one event seen
    /// through two hooks (a breakup that is also a divorce, twins born in the
    /// same tick) produces one request.
    /// </summary>
    public sealed class TriggerDeduplicator
    {
        const int PruneThreshold = 256;

        readonly Dictionary<string, int> _acceptedAt = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly int _windowTicks;
        readonly int _retentionTicks;

        public TriggerDeduplicator(int windowTicks, int retentionTicks)
        {
            _windowTicks = Math.Max(0, windowTicks);
            _retentionTicks = Math.Max(_windowTicks, retentionTicks);
        }

        public int Count => _acceptedAt.Count;

        public bool IsDuplicate(string key, int nowTick)
        {
            return key != null
                && _acceptedAt.TryGetValue(key, out int at)
                && nowTick - at <= _windowTicks;
        }

        public void MarkAccepted(string key, int nowTick)
        {
            if (key == null) return;
            _acceptedAt[key] = nowTick;
            if (_acceptedAt.Count <= PruneThreshold) return;

            var expired = new List<string>();
            foreach (KeyValuePair<string, int> pair in _acceptedAt)
            {
                if (nowTick - pair.Value > _retentionTicks) expired.Add(pair.Key);
            }
            foreach (string stale in expired) _acceptedAt.Remove(stale);
        }

        public void Clear() => _acceptedAt.Clear();
    }
}
