using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Core.Diagnostics;
using Verse;

namespace Ustas.RimAI.Communication.Personas;

/// <summary>
/// Authoritative pawn → preset/personality resolution for Personas.
/// Owns rule matching, enabled-preset pool selection, and short-lived de-dupe cache.
/// PresetSynchronizer remains separate (pool sync into Communication Constant).
/// </summary>
public static class PersonaResolver
{
    // Module-lifetime de-dupe; cleared on PersonasComposition.Stop.
    static Dictionary<string, int> recentAssignments = new Dictionary<string, int>();
    const int CacheDurationTicks = 600;
    const int MaxRetryAttempts = 2;

    public static PersonalityData AssignViaRulesOrRandom(IEnumerable<PersonalityData> vanillaPool, Pawn pawn)
    {
        if (pawn == null || !pawn.RaceProps.Humanlike)
            return vanillaPool.RandomElement();

        CustomPreset preset = FindPresetFor(pawn);
        if (preset != null)
        {
            if (PersonasMod.Settings.EnableDebugLog)
                RimAiLog.Info(RimAiLogCategory.Personas, $"[Director] Auto-assigned '{preset.label}' to {pawn.Name} via rule or global pool.");
            return new PersonalityData(preset.personaText, preset.chattiness);
        }

        if (PersonasMod.Settings.EnableDebugLog)
            RimAiLog.Info(RimAiLogCategory.Personas, $"[Director] No presets found for {pawn.Name}. Falling back to vanilla random pool.");

        return vanillaPool.RandomElement();
    }

    /// <summary>
    /// Rule match and enabled-preset draw. Returns null when the library has no candidates.
    /// </summary>
    public static CustomPreset FindPresetFor(Pawn p)
    {
        var settings = PersonasMod.Settings;
        if (settings.userPresets == null || !settings.userPresets.Any())
            return null;

        List<string> candidateIds = new List<string>();

        if (settings.assignmentRules != null && settings.assignmentRules.Any())
        {
            var matchingRules = settings.assignmentRules.Where(r => r.enabled && IsMatch(p, r)).ToList();
            if (matchingRules.Any())
            {
                int maxPriority = matchingRules.Max(r => r.priority);
                var activeRules = matchingRules.Where(r => r.priority == maxPriority);

                HashSet<string> ruleIds = new HashSet<string>();
                foreach (var rule in activeRules)
                    if (rule.allowedPresetIds != null)
                        foreach (var id in rule.allowedPresetIds)
                            ruleIds.Add(id);

                candidateIds.AddRange(ruleIds);
            }
        }

        if (candidateIds.Count == 0)
        {
            candidateIds.AddRange(settings.userPresets
                .Where(pr => pr.enabled)
                .Select(pr => pr.id));
        }

        if (candidateIds.Count == 0)
            return null;

        CleanupExpiredCache();
        string pickId = null;
        int currentTick = Find.TickManager.TicksGame;

        for (int attempt = 0; attempt <= MaxRetryAttempts; attempt++)
        {
            if (attempt > 0)
                Rand.Range(0, 1000);

            pickId = candidateIds.RandomElement();

            if (!recentAssignments.ContainsKey(pickId))
                break;

            if (attempt == MaxRetryAttempts)
            {
                if (PersonasMod.Settings.EnableDebugLog)
                    RimAiLog.Info(RimAiLogCategory.Personas, $"[Director] Preset '{pickId}' was recently used, but accepting after {MaxRetryAttempts + 1} attempts (pool size: {candidateIds.Count})");
                break;
            }

            if (PersonasMod.Settings.EnableDebugLog)
                RimAiLog.Info(RimAiLogCategory.Personas, $"[Director] Preset '{pickId}' was recently used, retrying... (attempt {attempt + 1}/{MaxRetryAttempts + 1})");
        }

        recentAssignments[pickId] = currentTick;
        return settings.userPresets.Find(x => x.id == pickId);
    }

    public static void ClearAssignmentCache()
    {
        recentAssignments.Clear();
    }

    static void CleanupExpiredCache()
    {
        int currentTick = Find.TickManager.TicksGame;
        var expiredKeys = recentAssignments
            .Where(kvp => currentTick - kvp.Value > CacheDurationTicks)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
            recentAssignments.Remove(key);
    }

    static bool IsMatch(Pawn p, AssignmentRule rule)
    {
        if (string.IsNullOrEmpty(rule.targetDefName))
            return false;
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
