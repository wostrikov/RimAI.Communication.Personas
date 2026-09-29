using HarmonyLib;
using RimWorld;
using Ustas.RimAI.Communication.Personas.Automation;
using Verse;
using Verse.Profile;

namespace Ustas.RimAI.Communication.Personas.Patches
{
    // Hooks that tell the persona automation who arrived, who changed sides, and
    // when the world goes away. Each one records and returns.

    internal static class Patch_PersonaNewPawnSpawned
    {
        static void Postfix(Pawn __instance, bool respawningAfterLoad)
        {
            if (respawningAfterLoad || __instance?.RaceProps == null || !__instance.RaceProps.Humanlike) return;
            PersonaGameEventInbox inbox = PersonaHookBoundary.Inbox;
            if (inbox == null || !inbox.WantsNewPawns) return;
            inbox.Record(new PersonaGameEvent { Kind = PersonaGameEventKind.NewPawn, Subject = __instance });
        }
    }

    internal static class Patch_PersonaRoleOnSetFaction
    {
        static void Prefix(Pawn __instance, out PawnRoleCategory __state)
        {
            __state = PersonaRoleHook.Capture(__instance);
        }

        static void Postfix(Pawn __instance, PawnRoleCategory __state)
        {
            PersonaRoleHook.Record(__instance, __state);
        }
    }

    internal static class Patch_PersonaRoleOnGuestStatus
    {
        static void Prefix(Pawn ___pawn, out PawnRoleCategory __state)
        {
            __state = PersonaRoleHook.Capture(___pawn);
        }

        static void Postfix(Pawn ___pawn, PawnRoleCategory __state)
        {
            PersonaRoleHook.Record(___pawn, __state);
        }
    }

    /// <summary>Going to the main menu or loading a save clears the world; the automation goes with it.</summary>
    internal static class Patch_PersonaAutomationOnWorldCleared
    {
        static void Prefix()
        {
            if (PersonasComposition.Current.IsStarted) PersonasComposition.Current.Automation.Reset();
        }
    }

    internal static class PersonaRoleHook
    {
        internal static PawnRoleCategory Capture(Pawn pawn)
        {
            if (pawn?.RaceProps == null || !pawn.RaceProps.Humanlike || PawnGenerator.IsBeingGenerated(pawn)) return PawnRoleCategory.None;
            PersonaGameEventInbox inbox = PersonaHookBoundary.Inbox;
            if (inbox == null || !inbox.WantsRoleChanges) return PawnRoleCategory.None;
            return PawnRoleClassifier.Classify(pawn);
        }

        /// <summary>Records where the pawn started; its new role is read once the change has settled.</summary>
        internal static void Record(Pawn pawn, PawnRoleCategory before)
        {
            if (before == PawnRoleCategory.None) return;
            PersonaHookBoundary.Inbox?.Record(new PersonaGameEvent { Kind = PersonaGameEventKind.RoleChange, Subject = pawn, From = before });
        }
    }
}
