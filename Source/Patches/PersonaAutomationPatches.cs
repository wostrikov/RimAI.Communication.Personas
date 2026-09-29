using System;
using HarmonyLib;
using RimWorld;
using Ustas.RimAI.Communication.Personas.Diagnostics;
using Verse;
using Verse.Profile;

namespace Ustas.RimAI.Communication.Personas.Patches
{
    /// <summary>
    /// Installs the persona automation hooks once defs exist.
    ///
    /// They were [HarmonyPatch] classes picked up by the PatchAll in
    /// PersonasComposition.Start, which runs from the Verse.Mod constructor -
    /// before a single def is loaded. Patching PregnancyUtility.ApplyBirthOutcome
    /// there ran PregnancyUtility's static constructor, which keys a dictionary by
    /// defs that were still null, so the type was dead for the session and every
    /// mod that patched or called it after us failed with it: Vanilla Expanded
    /// Framework, Alpha Memes, VRE Highmate and Waster, Integrated Implants,
    /// BG Inheritance. AGENTS.md describes exactly this; here it is applied by
    /// hand from a [StaticConstructorOnStartup], as it says to.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class PersonaAutomationPatches
    {
        static PersonaAutomationPatches()
        {
            var harmony = new Harmony("ustas.rimai.communication.personas.automation");
            int installed = 0;

            installed += Postfix(harmony, AccessTools.Method(typeof(MarriageCeremonyUtility), nameof(MarriageCeremonyUtility.Married)), typeof(Patch_PersonaEvolveOnMarriage));
            installed += Postfix(harmony, AccessTools.Method(typeof(SpouseRelationUtility), nameof(SpouseRelationUtility.DoDivorce)), typeof(Patch_PersonaEvolveOnDivorce));
            installed += Postfix(harmony, AccessTools.Method(typeof(InteractionWorker_Breakup), nameof(InteractionWorker_Breakup.Interacted)), typeof(Patch_PersonaEvolveOnBreakup));
            installed += Postfix(harmony, AccessTools.Method(typeof(PregnancyUtility), nameof(PregnancyUtility.ApplyBirthOutcome)), typeof(Patch_PersonaEvolveOnBirth));
            installed += Both(harmony, AccessTools.Method(typeof(Pawn), nameof(Pawn.Kill)), typeof(Patch_PersonaEvolveOnFamilyDeath));
            installed += Both(harmony, AccessTools.Method(typeof(TraitSet), nameof(TraitSet.GainTrait)), typeof(Patch_PersonaEvolveOnTraitGained));
            installed += Postfix(harmony, AccessTools.Method(typeof(Pawn), nameof(Pawn.SpawnSetup), new[] { typeof(Map), typeof(bool) }), typeof(Patch_PersonaNewPawnSpawned));
            installed += Both(harmony, AccessTools.Method(typeof(Pawn), nameof(Pawn.SetFaction)), typeof(Patch_PersonaRoleOnSetFaction));
            installed += Both(harmony, AccessTools.Method(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.SetGuestStatus)), typeof(Patch_PersonaRoleOnGuestStatus));
            installed += Prefix(harmony, AccessTools.Method(typeof(MemoryUtility), nameof(MemoryUtility.ClearAllMapsAndWorld)), typeof(Patch_PersonaAutomationOnWorldCleared));

            ModuleLog.Message("[RimAI.Personas] automation hooks installed: " + installed + " of 10");
        }

        private static int Postfix(Harmony harmony, System.Reflection.MethodBase target, Type patch)
        {
            harmony.Patch(target, postfix: new HarmonyMethod(AccessTools.Method(patch, "Postfix")));
            return 1;
        }

        private static int Prefix(Harmony harmony, System.Reflection.MethodBase target, Type patch)
        {
            harmony.Patch(target, prefix: new HarmonyMethod(AccessTools.Method(patch, "Prefix")));
            return 1;
        }

        private static int Both(Harmony harmony, System.Reflection.MethodBase target, Type patch)
        {
            harmony.Patch(target,
                prefix: new HarmonyMethod(AccessTools.Method(patch, "Prefix")),
                postfix: new HarmonyMethod(AccessTools.Method(patch, "Postfix")));
            return 1;
        }
    }
}
