using Verse;
using Ustas.RimAI.Communication.Data;
using System;
using Ustas.RimAI.Core.Diagnostics;
using Ustas.RimAI.Core.Personas;

namespace Ustas.RimAI.Communication.Personas
{
    public static class Patch_PromptService
    {
        [ThreadStatic]
        private static bool _isPatching = false;

        public static string Transform(Pawn pawn, string result)
        {
            if (_isPatching || pawn == null || string.IsNullOrEmpty(result))
                return result;

            // Wave C: when PersonaProjectionDefaults.UseTypedPersonaProjection is true,
            // Talk presents precomputed projections and this late replace path is removed.
            if (PersonaProjectionDefaults.UseTypedPersonaProjection)
                return result;

            string rawPersona = Hediff_Persona.GetOrAddNew(pawn)?.Personality;
            if (string.IsNullOrEmpty(rawPersona))
                return result;

            try
            {
                _isPatching = true;
                string renderedPersona = DirectorUtils.RenderScribanText(rawPersona, pawn);
                return PersonaTalkTransform.ApplyLateReplace(result, rawPersona, renderedPersona);
            }
            catch (Exception ex)
            {
                if (PersonasMod.Settings.EnableDebugLog)
                    RimAiLog.Warning(RimAiLogCategory.Personas, $"[Director] Failed to patch context for {pawn.LabelShort}: {ex.Message}");
                return result;
            }
            finally
            {
                _isPatching = false;
            }
        }
    }
}
