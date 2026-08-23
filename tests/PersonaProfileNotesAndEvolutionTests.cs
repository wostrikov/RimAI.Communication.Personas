using System;
using System.IO;
using Ustas.RimAI.Communication.Personas.Policy;

internal static class PersonaProfileNotesAndEvolutionTests
{
    public static int Run()
    {
        int n = 0;
        void T(bool x, string s)
        {
            if (!x)
                throw new Exception("FAILED " + s);
            n++;
        }

        const string profile =
            "--- Basic Info ---\nName: Twiddle\n--- Genes ---\n[Endogenes (Natural)]: Fast learner,\n[Xenogenes (Artificial)]: Fire spew,\n--- Skills ---\nShooting 8\n--- Key Relationships ---\nLover: Ada\n--- Ideology ---\nTranshumanist";
        T(PersonaProfileExtractPolicy.HasRequiredFacets(profile), "profile-facets");
        T(!PersonaProfileExtractPolicy.HasRequiredFacets("--- Basic Info ---\nName only"), "profile-incomplete");

        const string notes = "Keep the colony wry and exhausted.";
        string withNotes = PersonaDirectorNotesPolicy.ApplySharedBackground(profile, notes, true);
        T(PersonaDirectorNotesPolicy.NotesAffectPrompt(withNotes, notes), "notes-in-prompt");
        T(withNotes.Contains(PersonaDirectorNotesPolicy.NotesHeader), "notes-header");
        T(PersonaDirectorNotesPolicy.ApplySharedBackground(profile, notes, false) == profile, "notes-toggle-off");
        string pawnA = PersonaDirectorNotesPolicy.ApplySharedBackground("pawn-A", notes, true);
        string pawnB = PersonaDirectorNotesPolicy.ApplySharedBackground("pawn-B", notes, true);
        T(PersonaDirectorNotesPolicy.NotesAffectPrompt(pawnA, notes)
            && PersonaDirectorNotesPolicy.NotesAffectPrompt(pawnB, notes), "batch-shared-notes");

        string evolve = PersonaEvolutionPolicy.Compose(
            "old voice",
            "Time passed since last update: 3 days.",
            "Shooting 7 -> 8",
            notes,
            true,
            "- remembered the raid");
        T(PersonaEvolutionPolicy.HasSnapshotEvolution(evolve), "evolve-markers");
        T(evolve.Contains("old voice"), "evolve-previous");
        T(evolve.Contains("Shooting 7 -> 8"), "evolve-diff");
        T(evolve.Contains("- remembered the raid"), "evolve-memory");
        T(evolve.Contains(notes), "evolve-notes");

        string builder = Read("DirectorCharacterDataBuilder.cs.src");
        T(builder.Contains("PersonaProfileExtractPolicy.IdentityMarker"), "builder-identity");
        T(builder.Contains("PersonaProfileExtractPolicy.EndogeneMarker"), "builder-endo");
        T(builder.Contains("PersonaProfileExtractPolicy.XenogeneMarker"), "builder-xeno");
        T(builder.Contains("PersonaProfileExtractPolicy.SkillsMarker"), "builder-skills");
        T(builder.Contains("PersonaProfileExtractPolicy.RelationsMarker"), "builder-relations");
        T(builder.Contains("PersonaProfileExtractPolicy.IdeologyMarker"), "builder-ideo");
        T(builder.Contains("PersonaDirectorNotesPolicy.AppendNotes"), "builder-notes");

        string evolveSrc = Read("DirectorPersonaEvolve.cs.src");
        T(evolveSrc.Contains("PersonaEvolutionPolicy.Compose"), "evolve-uses-policy");

        string batch = Read("Window_BatchDirector.cs.src");
        T(batch.Contains("directorNotes"), "batch-shares-director-notes");
        return n;
    }

    static string Read(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, name);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
