using System;
using System.Collections.Generic;

namespace Ustas.RimAI.Communication.Personas.Policy
{
    /// <summary>
    /// Bounds on the persona history kept in the save: how many records a pawn
    /// keeps, which changes are worth a record, and how much context text one
    /// record may carry. Host-free so the rules can be tested without RimWorld.
    /// </summary>
    public static class PersonaHistoryPolicy
    {
        public const int DefaultMaxRecords = 5;
        public const int MinMaxRecords = 1;
        public const int MaxMaxRecords = 20;

        // A persona this long is runaway append-mode output, not a persona. One
        // such pawn would otherwise dominate the save through its history alone.
        public const int MaxRecordedChars = 16000;
        public const int MaxContextChars = 1500;

        public static int ClampMaxRecords(int value)
        {
            if (value < MinMaxRecords) return MinMaxRecords;
            if (value > MaxMaxRecords) return MaxMaxRecords;
            return value;
        }

        /// <summary>
        /// A change is recorded only when there was something to go back to and
        /// the text really changed. An empty "before" is a pawn that had no
        /// persona yet, and restoring nothing is not an undo.
        /// </summary>
        public static bool ShouldRecord(string before, string after)
        {
            string from = Normalize(before);
            string to = Normalize(after);
            if (from.Length == 0 || to.Length == 0) return false;
            if (from.Length > MaxRecordedChars || to.Length > MaxRecordedChars) return false;
            return !string.Equals(from, to, StringComparison.Ordinal);
        }

        public static string ClampContext(string context)
        {
            string text = (context ?? string.Empty).Trim();
            return text.Length <= MaxContextChars ? text : text.Substring(0, MaxContextChars);
        }

        /// <summary>Records are kept newest first; the oldest fall off the end.</summary>
        public static void TrimNewestFirst<T>(List<T> records, int maxRecords)
        {
            if (records == null) return;
            int limit = ClampMaxRecords(maxRecords);
            if (records.Count > limit)
                records.RemoveRange(limit, records.Count - limit);
        }

        public static string Normalize(string text) => (text ?? string.Empty).Trim();
    }
}
