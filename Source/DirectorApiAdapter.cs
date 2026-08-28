using Ustas.RimAI.Communication.API;
using Ustas.RimAI.Communication.Prompt;
using System.Collections.Generic;
using Ustas.RimAI.Core.Diagnostics;
using Verse;

namespace Ustas.RimAI.Communication.Personas
{
    /// <summary>
    /// Director Scriban surface (d_* vars, director_notes, personality Override).
    /// Owned by <see cref="PersonasComposition"/> — not StaticConstructorOnStartup.
    /// </summary>
    public static class DirectorApiAdapter
    {
        private const string ModId = "director";
        static bool _surfaceRegistered;

        /// <summary>Idempotent registration of Director prompt variables and Personality Override.</summary>
        public static void RegisterSurface()
        {
            if (_surfaceRegistered)
                return;
            if (!PersonasComposition.Current.IsStarted)
                return;

            try
            {
                if (typeof(RimTalkPromptAPI) == null)
                    return;
            }
            catch
            {
                return;
            }

            RimAiLog.Info(RimAiLogCategory.Personas, "[RimAI.Personas] Registering variables for Scriban engine...");

            Reg("d_full_profile", p => DirectorDataEngine.BuildCompleteData(p), "Complete Profile with Mod Setting");
            Reg("d_memories", p => DirectorDataEngine.GetMemoryInfo(p), "Recent Memories");
            Reg("d_status_diff", p => DirectorDataEngine.GetDailyStatusDiff(p), "Daily Status Changes");
            Reg("d_evolve_current_persona", p => DirectorDataEngine.GetActiveUIText(p), "Evolve: Text currently in editor window (or hediff)");
            Reg("d_evolve_time_info", p => DirectorDataEngine.GetTimeInfo(p), "Evolve: Time passed since last update");
            RimTalkPromptAPI.RegisterPawnVariable(ModId, "d_evolve_diff",
                p => DirectorDataEngine.GetEvolveStatusDiff(p),
                "Evolve: Status changes since 'Set Time'");

            RimTalkPromptAPI.RegisterContextVariable(
                ModId,
                "director_notes",
                _ => PersonasMod.Settings.directorNotes,
                "Global notes from Persona Director"
            );

            RimTalkPromptAPI.RegisterContextVariable(ModId, "smart_history",
                ctx => DirectorDataEngine.GetSmartHistory(ctx.CurrentPawn, ctx.Pawns, ctx.IsMonologue),
                "Smart history: Dynamic quota & formatting based on context (Monologue/Dialogue).");

            try
            {
                RimTalkPromptAPI.RegisterPawnHook(
                    ModId,
                    ContextCategories.Pawn.Personality,
                    ContextHookRegistry.HookOperation.Override,
                    (pawn, originalValue) =>
                    {
                        // Typed path: pass through precomputed bag — no second Scriban render.
                        if (PromptManager.LastContext != null
                            && pawn != null
                            && !string.IsNullOrEmpty(pawn.ThingID)
                            && PromptManager.LastContext.TryGetTypedPersonaProjection(pawn.ThingID, out var precomputed)
                            && precomputed != null)
                        {
                            return precomputed;
                        }

                        return originalValue;
                    }
                );
            }
            // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY - director API adapter registration failed
            catch (System.Exception ex)
            {
                RimAiLog.WarningOnce(RimAiLogCategory.Personas, "[RimAI.Personas] director API adapter registration failed: " + ex, 1511129092);
            }

            Reg("d_basic_name", p => p.LabelShortCap);
            Reg("d_basic_fullname", p => p.Name?.ToStringFull ?? p.LabelShortCap);
            Reg("d_basic_gender", p => p.gender.ToString());
            Reg("d_basic_age", p => p.ageTracker.AgeBiologicalYears.ToString());
            Reg("d_basic_status", p => DirectorUtils.GetPawnSocialStatus(p), "With Quest");
            Reg("d_basic_faction_label", p => p.Faction?.Name ?? "None");
            Reg("d_basic_faction_desc", p => p.Faction?.def?.description?.StripTags() ?? "");

            Reg("d_race_label", p => p.def.label);
            Reg("d_race_desc", p => p.def.description.StripTags());
            Reg("d_race_xenotype", p => p.genes?.XenotypeLabel ?? "Baseliner");
            Reg("d_race_xenotype_desc", p => p.genes?.Xenotype?.description.StripTags() ?? "");

            Reg("d_genes_list", p => DirectorDataEngine.GetGenesInfo(p, includeDesc: false));
            Reg("d_genes_list_with_desc", p => DirectorDataEngine.GetGenesInfo(p, includeDesc: true));

            Reg("d_backstory_childhood_title", p => p.story?.Childhood?.TitleCapFor(p.gender) ?? "");
            Reg("d_backstory_childhood_desc", p => p.story?.Childhood?.FullDescriptionFor(p).Resolve().StripTags() ?? "");
            Reg("d_backstory_adulthood_title", p => p.story?.Adulthood?.TitleCapFor(p.gender) ?? "");
            Reg("d_backstory_adulthood_desc", p => p.story?.Adulthood?.FullDescriptionFor(p).Resolve().StripTags() ?? "");
            Reg("d_backstory_full", p => DirectorDataEngine.GetBackstoryInfo(p, true));
            Reg("d_traits_list", p => DirectorDataEngine.GetTraitsInfo(p, includeDesc: false));
            Reg("d_traits_list_with_desc", p => DirectorDataEngine.GetTraitsInfo(p, includeDesc: true));
            Reg("d_ideology_list", p => DirectorDataEngine.GetIdeologyInfo(p, includeDesc: false));
            Reg("d_ideology_list_with_desc", p => DirectorDataEngine.GetIdeologyInfo(p, includeDesc: true));
            Reg("d_skills_list", p => DirectorDataEngine.GetSkillsInfo(p, true));
            Reg("d_skills_list_with_desc", p => DirectorDataEngine.GetSkillsInfo(p, includeDesc: true), "With Passion");
            Reg("d_health_list", p => DirectorDataEngine.GetHealthInfo(p, true));
            Reg("d_health_list_with_desc", p => DirectorDataEngine.GetHealthInfo(p, includeDesc: true));
            Reg("d_relations", p => DirectorDataEngine.GetRelationsInfo(p));
            Reg("d_equipment", p => DirectorDataEngine.GetEquipmentInfo(p));
            Reg("d_inventory", p => DirectorDataEngine.GetInventoryInfo(p));
            Reg("d_rimpsyche", p => DirectorDataEngine.GetRimPsycheInfo(p));

            Reg("d_common_knowledge", p => DirectorDataEngine.GetCommonKnowledgeInfo(p, DirectorDataEngine.BuildCompleteData(p)));

            _surfaceRegistered = true;
        }

        public static void UnregisterSurface()
        {
            if (!_surfaceRegistered)
                return;
            RimTalkPromptAPI.UnregisterAllHooks(ModId);
            _surfaceRegistered = false;
        }

        private static void Reg(string name, System.Func<Pawn, string> provider, string desc = null)
        {
            RimTalkPromptAPI.RegisterPawnVariable(ModId, name, provider, desc);
        }
    }
}
