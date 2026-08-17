using Verse;
using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Data; // 引用以访问 Constant
using Ustas.RimAI.Core.Handshake;

namespace Ustas.RimAI.Communication.Personas
{
    public static class DirectorStartup
    {
        public static void Initialize()
        {
            try
            {
                if (!RimAiHandshake.IsApproved(RimAiModuleIds.Personas))
                {
                    return;
                }

                var settings = PersonasMod.Settings;
                if (settings == null) return;

                // 防空
                if (settings.userPresets == null) settings.userPresets = new List<CustomPreset>();
                if (settings.assignmentRules == null) settings.assignmentRules = new List<AssignmentRule>();

                // ★★★ 核心修复 C：先备份真·原版数据 ★★★
                // 在我们做任何同步/覆盖之前，先看看 Constant.Personalities 里有什么
                // 此时游戏刚加载完，Constant 里肯定是干净的原版数据
                if (PersonasSettings.OriginalVanillaCache == null && Constant.Personalities != null)
                {
                    if (Constant.Personalities is IEnumerable<PersonalityData> list)
                    {
                        PersonasSettings.OriginalVanillaCache = new List<PersonalityData>(list);
                        Log.Message($"[RimAI.Personas] Cached {PersonasSettings.OriginalVanillaCache.Count} original vanilla presets.");
                    }
                }

                // 2. 执行新用户初始化
                if (!settings._libraryInitialized)
                {
                    if (settings.userPresets.Count == 0 && settings.assignmentRules.Count == 0)
                    {
                        Log.Message("[RimAI.Personas] First time setup detected. Initializing library...");
                        settings.InitLibrary();
                    }

                    settings._libraryInitialized = true;
                    settings.Write();
                }

                // 3. ★★★ 最后再同步 ★★★
                // 现在可以用我们的数据去覆盖原版了，因为原版已经备份过了
                PresetSynchronizer.SyncToRimTalk();
                Log.Message("[RimAI.Personas] Sync to RimTalk completed.");
            }
            catch (Exception ex)
            {
                Log.Error($" Initialization Failed: {ex}");
            }
        }
    }
}