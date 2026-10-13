using System.Linq;
using System.Text;
using Ustas.RimAI.Communication.API;
using Ustas.RimAI.Communication.Personas.Diagnostics;
using Ustas.RimAI.Communication.Prompt;
using Ustas.RimAI.Core.Diagnostics;
using Verse;

namespace Ustas.RimAI.Communication.Personas
{
    /// <summary>
    /// Renders a Communication prompt preset into a Director request: System
    /// entries become the context, every other role the prompt, and the JSON
    /// protocol the answer is parsed with is appended either way. While a preset
    /// renders, {{director_trigger_context}} answers with the events that asked
    /// for the request, so a preset can use them; outside a render it is empty.
    /// So are the two notes variables and {{pawn.d_evolve_memories}}, which
    /// belong to a request about one pawn and mean nothing in a dialogue.
    /// </summary>
    public static class DirectorPresetRenderer
    {
        public const string NoPreset = "None (Use Internal)";

        // Rendering runs on the main thread (the editor's click, the automation's
        // tick); the previous value is restored, so a render inside a render is safe.
        static string _triggerContext = string.Empty;
        static bool _automatic;
        static int _renderDepth;
        static bool _renderingNotes;

        public static string CurrentTriggerContext => _triggerContext ?? string.Empty;

        /// <summary>True while a preset is being rendered into a Director request.</summary>
        public static bool IsRendering => _renderDepth > 0;

        public static bool IsSet(string presetName) =>
            !string.IsNullOrWhiteSpace(presetName) && presetName != NoPreset;

        /// <summary>
        /// False when the preset is missing or renders nothing; the caller then uses its built-in prompt.
        /// <paramref name="automatic"/> says the request is Auto-Gen's or Auto-Evolve's rather than
        /// the player's own click, which is when the automatic notes apply.
        /// </summary>
        public static bool TryRender(string presetName, Pawn pawn, string triggerContext, out string context, out string prompt, bool automatic = true)
        {
            context = string.Empty;
            prompt = string.Empty;
            if (!IsSet(presetName) || pawn == null)
                return false;

            var preset = RimTalkPromptAPI.GetAllPresets()?.FirstOrDefault(candidate => candidate.Name == presetName);
            if (preset == null)
            {
                RimAiLog.Warning(RimAiLogCategory.Personas, $"[Director] Preset '{presetName}' not found. Falling back to internal.");
                return false;
            }

            var system = new StringBuilder();
            var user = new StringBuilder();
            string previous = _triggerContext;
            bool previousAutomatic = _automatic;
            try
            {
                _triggerContext = triggerContext?.Trim() ?? string.Empty;
                _automatic = automatic;
                _renderDepth++;
                var contextObj = new PromptContext(pawn);
                foreach (var entry in preset.Entries)
                {
                    if (entry == null || !entry.Enabled) continue;
                    string rendered = ScribanParser.Render(entry.Content, contextObj, true);
                    if (string.IsNullOrWhiteSpace(rendered)) continue;
                    StringBuilder target = entry.Role.ToString().ToLowerInvariant() == "system" ? system : user;
                    if (target.Length > 0) target.AppendLine("\n");
                    target.Append(rendered);
                }
            }
            finally
            {
                _renderDepth--;
                _automatic = previousAutomatic;
                _triggerContext = previous;
            }

            if (user.Length == 0)
            {
                RimAiLog.Warning(RimAiLogCategory.Personas, $"[Director] Preset '{presetName}' rendered no prompt. Falling back to internal.");
                return false;
            }

            // Hard constraint — changing this breaks an invariant. (JSON)
            system.AppendLine("\n" + PersonasSettings.HiddenTechnicalPrompt_Single);
            context = system.ToString();
            prompt = user.ToString();
            ModuleLog.Message($"[Director] Preset '{presetName}' rendered. Context {context.Length}, prompt {prompt.Length} chars.");
            return true;
        }

        /// <summary>Player notes for automatic requests, with the preset variables resolved for this pawn.</summary>
        public static string RenderNotes(string notes, Pawn pawn)
        {
            if (string.IsNullOrWhiteSpace(notes) || pawn == null) return string.Empty;
            // Notes are a template, and the variable that inserts them is one of
            // the variables a template may name: notes quoting themselves end here.
            if (_renderingNotes) return string.Empty;
            try
            {
                _renderingNotes = true;
                return ScribanParser.Render(notes, new PromptContext(pawn), true)?.Trim() ?? string.Empty;
            }
            finally
            {
                _renderingNotes = false;
            }
        }

        /// <summary>
        /// What a notes variable answers inside a preset: the notes while an automatic
        /// request renders, and nothing for the player's own Evolve or in a dialogue -
        /// the same rule the built-in prompts follow.
        /// </summary>
        public static string NotesForPreset(string notes, Pawn pawn) =>
            IsRendering && _automatic ? RenderNotes(notes, pawn) : string.Empty;
    }
}
