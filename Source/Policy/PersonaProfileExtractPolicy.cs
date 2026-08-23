namespace Ustas.RimAI.Communication.Personas.Policy
{
    /// <summary>
    /// Authoritative pawn-profile facets for Director extract: identity,
    /// endo/xeno genes, skills, relations, and Ideology DLC.
    /// </summary>
    public static class PersonaProfileExtractPolicy
    {
        public const string IdentityMarker = "--- Basic Info ---";
        public const string XenotypeMarker = "--- Race & Xenotype ---";
        public const string GenesMarker = "--- Genes ---";
        public const string EndogeneMarker = "[Endogenes (Natural)]:";
        public const string XenogeneMarker = "[Xenogenes (Artificial)]:";
        public const string SkillsMarker = "--- Skills ---";
        public const string RelationsMarker = "--- Key Relationships ---";
        public const string IdeologyMarker = "--- Ideology ---";

        public static bool HasRequiredFacets(string profile)
        {
            return Contains(profile, IdentityMarker)
                && Contains(profile, EndogeneMarker)
                && Contains(profile, XenogeneMarker)
                && Contains(profile, SkillsMarker)
                && Contains(profile, RelationsMarker)
                && Contains(profile, IdeologyMarker);
        }

        static bool Contains(string text, string marker) =>
            !string.IsNullOrEmpty(text) && text.IndexOf(marker, System.StringComparison.Ordinal) >= 0;
    }
}
