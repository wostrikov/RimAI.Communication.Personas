using System.Collections.Generic;
using Ustas.RimAI.Communication.Data;
using Verse;

namespace Ustas.RimAI.Communication.Personas
{
    /// <summary>
    /// Compatibility façade over <see cref="PersonaResolver"/>. Prefer PersonaResolver.
    /// </summary>
    public static class Patch_GetOrAddNew
    {
        public static PersonalityData AssignViaRulesOrRandom(IEnumerable<PersonalityData> vanillaPool, Pawn pawn) =>
            PersonaResolver.AssignViaRulesOrRandom(vanillaPool, pawn);

        public static CustomPreset FindPresetFor(Pawn p) =>
            PersonaResolver.FindPresetFor(p);
    }
}
