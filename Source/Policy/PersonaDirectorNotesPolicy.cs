namespace Ustas.RimAI.Communication.Personas.Policy
{
    /// <summary>
    /// Director notes that affect generation. Batch mode reuses the same
    /// notes block as a shared background for every selected pawn.
    /// </summary>
    public static class PersonaDirectorNotesPolicy
    {
        public const string NotesHeader = "--- Director's Notes (Custom Context) ---";

        public static string AppendNotes(string profile, string notes, bool includeNotes)
        {
            if (!includeNotes || string.IsNullOrWhiteSpace(notes))
                return profile ?? string.Empty;
            return (profile ?? string.Empty).TrimEnd()
                + "\n\n" + NotesHeader + "\n"
                + notes.TrimEnd()
                + "\n";
        }

        public static string ApplySharedBackground(string perPawnProfile, string sharedNotes, bool includeNotes) =>
            AppendNotes(perPawnProfile, sharedNotes, includeNotes);

        public static bool NotesAffectPrompt(string prompt, string notes)
        {
            return !string.IsNullOrWhiteSpace(notes)
                && !string.IsNullOrEmpty(prompt)
                && prompt.IndexOf(notes.Trim(), System.StringComparison.Ordinal) >= 0;
        }
    }
}
