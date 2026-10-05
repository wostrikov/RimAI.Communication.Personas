using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Personas.Config;
using Ustas.RimAI.Communication.Personas.History;
using Verse;

namespace Ustas.RimAI.Communication.Personas.Automation
{
    /// <summary>Writes a first persona for a pawn that only has a placeholder.</summary>
    internal sealed class AutoGenJobHandler : IPersonaJobHandler
    {
        public TalkRequest Prepare(PersonaAutomationJob job, DirectorWorldComponent world)
        {
            PersonasSettings settings = PersonasMod.Settings;
            if (!IsAllowed(job, settings, out AutoGenCategorySettings category, out PawnRoleCategory role)) return null;
            if (!PersonaOwnership.HasNoOwnPersona(job.Pawn, world.History)) return null;

            job.Category = role;
            job.OriginalPersona = PersonaOwnership.ReadWithoutCreating(job.Pawn);

            string trigger = job.TriggerContexts.Count > 0 ? job.TriggerText : string.Empty;
            ContextSettings context = category.EffectiveContext(settings.Context);
            if (DirectorPresetRenderer.TryRender(category.advancedPreset, job.Pawn, trigger, out string presetContext, out string presetPrompt))
            {
                var presetRequest = new TalkRequest(presetPrompt, job.Pawn) { Context = presetContext };
                PawnPortraitCapture.Attach(presetRequest, job.Pawn, context);
                return presetRequest;
            }

            string data = DirectorCharacterDataBuilder.BuildCustomCharacterData(job.Pawn, context: context);
            string notes = DirectorPresetRenderer.RenderNotes(settings.Automation?.autoGenNotes, job.Pawn);
            if (notes.Length > 0)
                data += "\n\n[Auto-Gen Notes]\n" + notes;
            if (trigger.Length > 0)
                data += "\n\n[Trigger Event]\n" + trigger;

            string instruction = PromptFor(settings, category.presetSlot).Replace("{LANG}", DirectorPromptComposer.CurrentLanguage)
                + "\n\n" + PersonasSettings.HiddenTechnicalPrompt_Single;
            var request = new TalkRequest("[Character Data]\n" + data, job.Pawn)
            {
                Context = instruction
            };
            PawnPortraitCapture.Attach(request, job.Pawn, context);
            return request;
        }

        public bool Apply(PersonaAutomationJob job, PersonalityData result, DirectorWorldComponent world)
        {
            string persona = result?.Persona?.Trim();
            if (string.IsNullOrEmpty(persona)) return false;

            // Re-check everything the answer depended on. The pawn may have changed
            // role, and the player may have written a persona or switched the feature
            // off, all during the time the model took to answer.
            if (!IsAllowed(job, PersonasMod.Settings, out _, out PawnRoleCategory role) || role != job.Category) return false;
            if (PersonaOwnership.ReadWithoutCreating(job.Pawn) != job.OriginalPersona) return false;

            DirectorPersonalityGenerator.ApplyPersonalityToPawn(
                job.Pawn,
                result,
                PersonaChangeSource.AutoGen,
                job.TriggerText);
            world.SetTimestamp(job.Pawn);
            return true;
        }

        static bool IsAllowed(
            PersonaAutomationJob job,
            PersonasSettings settings,
            out AutoGenCategorySettings category,
            out PawnRoleCategory role)
        {
            category = null;
            role = PawnRoleCategory.None;
            PersonaAutomationSettings automation = settings?.Automation;
            if (automation == null || !automation.autoGenEnabled) return false;
            if (!AutoEvolveEligibility.IsLivePersonaPawn(job.Pawn)) return false;

            role = PawnRoleClassifier.Classify(job.Pawn);
            category = automation.GetCategory(role);
            if (category == null) return false;
            return job.RoleChange ? category.onRoleChange : category.enabled;
        }

        static string PromptFor(PersonasSettings settings, int slot)
        {
            int index = AutoGenCategorySettings.ClampSlot(slot);
            if (settings.presets == null || settings.presets.Count <= index) settings.InitPresets();
            string text = settings.presets[index]?.text;
            if (!string.IsNullOrWhiteSpace(text)) return text;
            return index == AutoGenCategorySettings.LastSinglePersonaSlot
                ? PersonasSettings.DefaultPrompt_Strict
                : PersonasSettings.DefaultPrompt_Simple;
        }
    }
}
