using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Personas.History;
using Ustas.RimAI.Core.Personas;
using Verse;

namespace Ustas.RimAI.Communication.Personas.Automation
{
    /// <summary>
    /// Answers "does this pawn have a persona of its own yet?" for Auto-Gen.
    ///
    /// A pawn gets a persona the moment anything asks for one: the talk system
    /// draws it at random from the preset pool. That draw is a placeholder, not
    /// a persona anyone wrote for this pawn, so it counts as none. Anything the
    /// history knows about, or any text outside the pool, was written on purpose
    /// and is never replaced automatically.
    /// </summary>
    public static class PersonaOwnership
    {
        /// <summary>
        /// The pawn's persona text, without creating the persona hediff. Asking
        /// through PersonaService would create one and draw a random persona as
        /// a side effect, which is exactly the state this check must see past.
        /// </summary>
        public static string ReadWithoutCreating(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return "";
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(PersonaScribeLabels.Hediff.DefName);
            if (def == null) return "";
            return (pawn.health.hediffSet.GetFirstHediffOfDef(def) as Hediff_Persona)?.Personality?.Trim() ?? "";
        }

        public static bool HasNoOwnPersona(Pawn pawn, PersonaHistoryStore history)
        {
            if (pawn == null) return false;
            if (history != null && history.HasRecords(pawn)) return false;
            string current = ReadWithoutCreating(pawn);
            return current.Length == 0 || IsPoolPersona(current);
        }

        public static bool IsPoolPersona(string text)
        {
            string wanted = (text ?? "").Trim();
            if (wanted.Length == 0) return false;

            List<CustomPreset> library = PersonasMod.Settings?.userPresets;
            if (library != null)
            {
                foreach (CustomPreset preset in library)
                {
                    if (Same(preset?.personaText, wanted)) return true;
                }
            }

            if (Contains(Constant.Personalities, wanted)) return true;
            return Contains(PersonasSettings.OriginalVanillaCache, wanted);
        }

        static bool Contains(IEnumerable<PersonalityData> pool, string wanted)
        {
            if (pool == null) return false;
            foreach (PersonalityData entry in pool)
            {
                if (Same(entry?.Persona, wanted)) return true;
            }
            return false;
        }

        static bool Same(string candidate, string wanted) =>
            candidate != null && string.Equals(candidate.Trim(), wanted, StringComparison.Ordinal);
    }
}
