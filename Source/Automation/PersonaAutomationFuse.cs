using System;
using Ustas.RimAI.Communication.Personas.Config;
using Ustas.RimAI.Core.Diagnostics;
using Verse;

namespace Ustas.RimAI.Communication.Personas.Automation
{
    /// <summary>
    /// Turns the automatic persona work off when it keeps failing, and keeps it
    /// off - across a restart - until the player clears it in the settings. A
    /// fault inside the work, or three requests in a row that never answer, mean
    /// the next one will not fare better, and every attempt costs a request.
    /// The reason is saved as a translation key and shown where it is cleared.
    /// </summary>
    public static class PersonaAutomationFuse
    {
        public const int MaxConsecutiveTimeouts = 3;

        public const string GenerateFailedReason = "RPD_Experimental_AutoGenRuntimeFailed";
        public const string EvolveFailedReason = "RPD_Experimental_AutoEvolveRuntimeFailed";
        public const string AutomationFailedReason = "RPD_Experimental_AutomationRuntimeFailed";
        public const string GenerateTimeoutReason = "RPD_Experimental_AutoGenTimeout";
        public const string EvolveTimeoutReason = "RPD_Experimental_AutoEvolveTimeout";

        public static bool IsTripped(PersonaAutomationSettings settings) => settings != null && settings.fuseTripped;

        public static void Trip(PersonaAutomationSettings settings, string reasonKey, Exception error)
        {
            if (settings == null || settings.fuseTripped) return;
            settings.fuseTripped = true;
            settings.fuseReason = reasonKey ?? AutomationFailedReason;
            PersonasMod.Settings?.Write();
            RimAiLog.Error(RimAiLogCategory.Personas,
                "[RimAI.Personas] automatic persona work turned off: " + settings.fuseReason
                + (error == null ? string.Empty : " " + error));
        }

        public static void Clear(PersonaAutomationSettings settings)
        {
            if (settings == null) return;
            settings.fuseTripped = false;
            settings.fuseReason = string.Empty;
            PersonasMod.Settings?.Write();
        }

        /// <summary>The saved reason as the player reads it.</summary>
        public static string ReasonText(PersonaAutomationSettings settings)
        {
            string key = settings?.fuseReason;
            if (string.IsNullOrEmpty(key)) key = AutomationFailedReason;
            return key.CanTranslate() ? key.Translate().ToString() : key;
        }
    }
}
