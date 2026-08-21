using Verse;
using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Core.Handshake;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Communication.Personas
{
    public static class DirectorStartup
    {
        public static void Initialize()
        {
            try
            {
                if (!PersonasComposition.Current.IsStarted)
                    return;
                if (!RimAiHandshake.IsApproved(RimAiModuleIds.Personas))
                {
                    return;
                }

                var settings = PersonasMod.Settings;
                if (settings == null) return;

                if (settings.userPresets == null) settings.userPresets = new List<CustomPreset>();
                if (settings.assignmentRules == null) settings.assignmentRules = new List<AssignmentRule>();

                if (PersonasSettings.OriginalVanillaCache == null && Constant.Personalities != null)
                {
                    if (Constant.Personalities is IEnumerable<PersonalityData> list)
                    {
                        PersonasSettings.OriginalVanillaCache = new List<PersonalityData>(list);
                        RimAiLog.Info(RimAiLogCategory.Personas, $"[RimAI.Personas] Cached {PersonasSettings.OriginalVanillaCache.Count} original vanilla presets.");
                    }
                }

                if (!settings._libraryInitialized)
                {
                    if (settings.userPresets.Count == 0 && settings.assignmentRules.Count == 0)
                    {
                        RimAiLog.Info(RimAiLogCategory.Personas, "[RimAI.Personas] First time setup detected. Initializing library...");
                        settings.InitLibrary();
                    }

                    settings._libraryInitialized = true;
                    settings.Write();
                }

                PresetSynchronizer.SyncToRimTalk();
                RimAiLog.Info(RimAiLogCategory.Personas, "[RimAI.Personas] Sync to RimTalk completed.");
            }
            catch (Exception ex)
            {
                RimAiLog.Error(RimAiLogCategory.Personas, $" Initialization Failed: {ex}");
            }
        }
    }
}