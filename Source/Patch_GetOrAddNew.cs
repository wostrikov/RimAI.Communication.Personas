using Verse;
using Ustas.RimAI.Communication.Data;
using System.Linq;
using System.Collections.Generic;
using System;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Communication.Personas
{
    public static class Patch_GetOrAddNew
    {
	// 最近分配记录：<预设ID, 分配时间>
        private static Dictionary<string, int> recentAssignments = new Dictionary<string, int>();
        private const int CACHE_DURATION_TICKS = 600; // 10秒 = 600 ticks (60 ticks/秒)
        private const int MAX_RETRY_ATTEMPTS = 2; // 最多重试2次，总共3次尝试
        public static PersonalityData AssignViaRulesOrRandom(IEnumerable<PersonalityData> vanillaPool, Pawn pawn)
        {
            // 在我们的规则逻辑执行前，先做一个基础安全检查
            if (pawn == null || !pawn.RaceProps.Humanlike)
            {
                // 对于非人类，直接使用原版逻辑
                return vanillaPool.RandomElement();
            }

            // 执行我们的规则
            CustomPreset preset = FindPresetFor(pawn);

            // 如果我们的规则找到了一个预设
            if (preset != null)
            {
                if (PersonasMod.Settings.EnableDebugLog)
                    RimAiLog.Info(RimAiLogCategory.Personas, $"[Director] Auto-assigned '{preset.label}' to {pawn.Name} via rule or global pool.");

                // 返回一个新的 PersonalityData 实例
                return new PersonalityData(preset.personaText, preset.chattiness);
            }

            // 如果我们的规则系统什么都没找到（比如库是空的），就回退到原版随机池
            if (PersonasMod.Settings.EnableDebugLog)
                RimAiLog.Info(RimAiLogCategory.Personas, $"[Director] No presets found for {pawn.Name}. Falling back to vanilla random pool.");

            return vanillaPool.RandomElement();
        }

        /// <summary>
        /// 规则匹配和随机抽取的核心逻辑。
        /// </summary>
        public static CustomPreset FindPresetFor(Pawn p)
        {
            var settings = PersonasMod.Settings;
            if (settings.userPresets == null || !settings.userPresets.Any()) return null;

            List<string> candidateIds = new List<string>();

            // 1. 尝试匹配规则
            if (settings.assignmentRules != null && settings.assignmentRules.Any())
            {
                var matchingRules = settings.assignmentRules.Where(r => r.enabled && IsMatch(p, r)).ToList();
                if (matchingRules.Any())
                {
                    int maxPriority = matchingRules.Max(r => r.priority);
                    var activeRules = matchingRules.Where(r => r.priority == maxPriority);

                    // 合并池子
                    HashSet<string> ruleIds = new HashSet<string>();
                    foreach (var rule in activeRules)
                        if (rule.allowedPresetIds != null)
                            foreach (var id in rule.allowedPresetIds) ruleIds.Add(id);

                    candidateIds.AddRange(ruleIds);
                }
            }

            // 2. 如果无规则命中，使用全局池
            if (candidateIds.Count == 0)
            {
                // 只有被用户开启的预设才会随机给路人
                candidateIds.AddRange(settings.userPresets
                    .Where(pr => pr.enabled) // ★ 只取已启用的 ★
                    .Select(pr => pr.id));
            }

            if (candidateIds.Count == 0) return null;
            // 3. 清理过期的缓存记录
            CleanupExpiredCache();
           // 4. 随机抽取（带重试机制避免短时间重复）
            string pickId = null;
            int currentTick = Find.TickManager.TicksGame;

            for (int attempt = 0; attempt <= MAX_RETRY_ATTEMPTS; attempt++)
            {
                // ★ 添加随机扰动，确保RNG状态被推进 ★
                if (attempt > 0)
                {
                    // 在重试时，先做一些随机操作来"搅动"RNG状态
                    Rand.Range(0, 1000); // 推进RNG状态
                }
                pickId = candidateIds.RandomElement();

                // 检查是否在最近使用过
                if (!recentAssignments.ContainsKey(pickId))
                {
                    // 未被最近使用，可以分配
                    break;
                }

                // 如果是最后一次尝试，即使重复也接受
                if (attempt == MAX_RETRY_ATTEMPTS)
                {
                    if (PersonasMod.Settings.EnableDebugLog)
                        RimAiLog.Info(RimAiLogCategory.Personas, $"[Director] Preset '{pickId}' was recently used, but accepting after {MAX_RETRY_ATTEMPTS + 1} attempts (pool size: {candidateIds.Count})");
                    break;
                }

                // 否则重试
                if (PersonasMod.Settings.EnableDebugLog)
                    RimAiLog.Info(RimAiLogCategory.Personas, $"[Director] Preset '{pickId}' was recently used, retrying... (attempt {attempt + 1}/{MAX_RETRY_ATTEMPTS + 1})");
            }

            // 5. 记录本次分配
            recentAssignments[pickId] = currentTick;

            return settings.userPresets.Find(x => x.id == pickId);
        }

        /// <summary>
        /// 清理超过10秒的缓存记录
        /// </summary>
        private static void CleanupExpiredCache()
        {
            int currentTick = Find.TickManager.TicksGame;
            var expiredKeys = recentAssignments
                .Where(kvp => currentTick - kvp.Value > CACHE_DURATION_TICKS)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                recentAssignments.Remove(key);
            }
        }

        /// <summary>
        /// 判断 Pawn 是否符合规则。
        /// </summary>
        private static bool IsMatch(Pawn p, AssignmentRule rule)
        {
            if (string.IsNullOrEmpty(rule.targetDefName)) return false;
            switch (rule.type)
            {
                case RuleType.FactionDef:
                    return p.Faction != null && p.Faction.def.defName == rule.targetDefName;
                case RuleType.RaceDef:
                    return p.def.defName == rule.targetDefName;
                case RuleType.XenotypeDef:
                    return p.genes != null && p.genes.Xenotype != null && p.genes.Xenotype.defName == rule.targetDefName;
                default:
                    return false;
            }
        }
    }
}
