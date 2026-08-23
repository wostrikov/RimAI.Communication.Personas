using System.Text;

namespace Ustas.RimAI.Communication.Personas.Policy
{
    /// <summary>
    /// Evolve-from-snapshot prompt: previous persona, elapsed time, optional
    /// status diff, director notes, and Memory-module memories.
    /// </summary>
    public static class PersonaEvolutionPolicy
    {
        public const string PreviousPersonaMarker = "[Previous Persona (The Starting Point)]";
        public const string TimeContextMarker = "[Time Context]";
        public const string StatusChangesMarker = "[Status Changes (since last update)]:";
        public const string NotesMarker = "[Director's Notes]";
        public const string NewMemoriesMarker = "[New Memories]";
        public const string NoNewMemoriesLine = "No new significant memories since last update.";

        public static string Compose(
            string previousPersona,
            string timeInfo,
            string diffReport,
            string notes,
            bool includeNotes,
            string memories)
        {
            var sb = new StringBuilder();
            sb.AppendLine(PreviousPersonaMarker);
            sb.AppendLine(previousPersona ?? string.Empty);
            sb.AppendLine();
            sb.AppendLine(TimeContextMarker);
            sb.AppendLine(string.IsNullOrEmpty(timeInfo) ? "No previous update record." : timeInfo);
            sb.AppendLine();
            if (!string.IsNullOrEmpty(diffReport))
            {
                sb.AppendLine(StatusChangesMarker);
                sb.AppendLine(diffReport);
                sb.AppendLine();
            }

            if (includeNotes && !string.IsNullOrEmpty(notes))
            {
                sb.AppendLine(NotesMarker);
                sb.AppendLine(notes);
                sb.AppendLine();
            }

            sb.AppendLine(NewMemoriesMarker);
            sb.AppendLine(string.IsNullOrEmpty(memories) ? NoNewMemoriesLine : memories);
            sb.AppendLine();
            return sb.ToString();
        }

        public static bool HasSnapshotEvolution(string prompt)
        {
            return Contains(prompt, PreviousPersonaMarker)
                && Contains(prompt, TimeContextMarker)
                && Contains(prompt, NewMemoriesMarker);
        }

        static bool Contains(string text, string marker) =>
            !string.IsNullOrEmpty(text) && text.IndexOf(marker, System.StringComparison.Ordinal) >= 0;
    }
}
