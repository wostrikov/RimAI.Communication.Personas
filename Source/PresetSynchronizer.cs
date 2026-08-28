using Verse;
using Ustas.RimAI.Communication.Data;
using System.Collections.Generic;
using System;
using Ustas.RimAI.Core.Diagnostics;
using Ustas.RimAI.Communication.Personas.Diagnostics;

namespace Ustas.RimAI.Communication.Personas
{
    public static class PresetSynchronizer
    {
        public static void SyncToRimTalk()
        {
            var settings = PersonasMod.Settings;
            if (settings == null || settings.userPresets == null) return;

            try
            {
                List<PersonalityData> syncList = new List<PersonalityData>();

                foreach (var preset in settings.userPresets)
                {
                    if (preset.enabled)
                    {
                        float chat = preset.chattiness;
                        try
                        {
                            chat = DirectorUtils.NormalizeChattiness(chat);
                        }
                        // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY - chattiness kept its raw value
                        catch (System.Exception ex)
                        {
                            ModuleLog.Message("[RimAI.Personas] chattiness kept its raw value: " + ex.Message);
                        }

                        syncList.Add(new PersonalityData(preset.personaText, chat));
                    }
                }

                if (syncList.Count == 0) return;

                Constant.ReplacePersonalities(syncList.ToArray());
            }
            catch (Exception ex)
            {
                RimAiLog.Warning(RimAiLogCategory.Personas, $"[RimAI.Personas] Sync failed: {ex.Message}");
            }
        }
    }
}
