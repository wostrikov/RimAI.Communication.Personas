using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Personas.Config;
using Ustas.RimAI.Communication.Personas.History;
using Ustas.RimAI.Communication.Personas.Policy;
using Verse;

namespace Ustas.RimAI.Communication.Personas.Automation
{
    /// <summary>
    /// Evolves a pawn's persona through the same request the editor's Evolve
    /// button builds, adding the events that triggered it.
    /// </summary>
    internal sealed class AutoEvolveJobHandler : IPersonaJobHandler
    {
        public TalkRequest Prepare(PersonaAutomationJob job, DirectorWorldComponent world)
        {
            PersonaAutomationSettings settings = PersonasMod.Settings?.Automation;
            if (settings == null || !settings.autoEvolveEnabled) return null;
            if (!AutoEvolveEligibility.IsEligible(job.Pawn, settings)) return null;

            string current = PersonaOwnership.ReadWithoutCreating(job.Pawn);
            if (current.Length == 0) return null;

            var (request, original) = DirectorPersonaEvolve.PrepareEvolveRequest(
                job.Pawn,
                null,
                job.TriggerText,
                settings.autoEvolveMode == AutoEvolveMode.Overwrite);
            if (request == null) return null;

            job.OriginalPersona = (original ?? current).Trim();
            return request;
        }

        public bool Apply(PersonaAutomationJob job, PersonalityData result, DirectorWorldComponent world)
        {
            PersonaAutomationSettings settings = PersonasMod.Settings?.Automation;
            string generated = result?.Persona?.Trim();
            if (settings == null || !settings.autoEvolveEnabled || string.IsNullOrEmpty(generated)) return false;
            if (!AutoEvolveEligibility.IsEligible(job.Pawn, settings)) return false;

            // The player edited the persona while the model was answering: their
            // text wins, and the answer describes a persona that no longer exists.
            if (PersonaOwnership.ReadWithoutCreating(job.Pawn) != job.OriginalPersona) return false;

            string evolved = PersonaAutomationPolicy.ComposeEvolved(
                job.OriginalPersona,
                generated,
                settings.autoEvolveMode == AutoEvolveMode.Overwrite);
            string context = job.TriggerContexts.Count > 0 ? job.TriggerText : "RPD_History_Scheduled".Translate().ToString();
            if (!PersonaHistoryService.Apply(job.Pawn, evolved, PersonaChangeSource.AutoEvolve, context)) return false;

            world.SetTimestamp(job.Pawn);
            return true;
        }
    }
}
