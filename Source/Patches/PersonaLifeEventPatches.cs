using HarmonyLib;
using RimWorld;
using Ustas.RimAI.Communication.Personas.Automation;
using Verse;

namespace Ustas.RimAI.Communication.Personas.Patches
{
    // Life events that can ask Auto-Evolve for an update. Each hook checks its
    // own switch first, so with Auto-Evolve off they cost one settings read on
    // the vanilla path, and otherwise they only record the event.

    [HarmonyPatch(typeof(MarriageCeremonyUtility), nameof(MarriageCeremonyUtility.Married))]
    internal static class Patch_PersonaEvolveOnMarriage
    {
        static void Postfix(Pawn firstPawn, Pawn secondPawn)
        {
            LifeEventHook.Record(PersonaGameEventKind.Marriage, firstPawn, secondPawn);
        }
    }

    [HarmonyPatch(typeof(SpouseRelationUtility), nameof(SpouseRelationUtility.DoDivorce))]
    internal static class Patch_PersonaEvolveOnDivorce
    {
        static void Postfix(Pawn initiator, Pawn recipient)
        {
            LifeEventHook.Record(PersonaGameEventKind.Breakup, initiator, recipient);
        }
    }

    [HarmonyPatch(typeof(InteractionWorker_Breakup), nameof(InteractionWorker_Breakup.Interacted))]
    internal static class Patch_PersonaEvolveOnBreakup
    {
        static void Postfix(Pawn initiator, Pawn recipient)
        {
            LifeEventHook.Record(PersonaGameEventKind.Breakup, initiator, recipient);
        }
    }

    [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.ApplyBirthOutcome))]
    internal static class Patch_PersonaEvolveOnBirth
    {
        static void Postfix(Thing __result, Pawn geneticMother, Thing birtherThing, Pawn father)
        {
            // A stillbirth returns a corpse, not a child; that is a different
            // event and the "became a parent" context would misdescribe it.
            if (!(__result is Pawn child) || child.Dead) return;
            LifeEventHook.Record(PersonaGameEventKind.Birth, child, geneticMother ?? birtherThing as Pawn, father);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    internal static class Patch_PersonaEvolveOnFamilyDeath
    {
        static void Prefix(Pawn __instance, out bool __state)
        {
            // Only a death that happens here counts; Kill on a corpse is a no-op.
            __state = __instance != null && !__instance.Dead;
        }

        static void Postfix(Pawn __instance, bool __state)
        {
            if (!__state || !__instance.Dead || __instance.RaceProps == null || !__instance.RaceProps.Humanlike) return;
            LifeEventHook.Record(PersonaGameEventKind.Death, __instance);
        }
    }

    [HarmonyPatch(typeof(TraitSet), nameof(TraitSet.GainTrait))]
    internal static class Patch_PersonaEvolveOnTraitGained
    {
        static void Prefix(TraitSet __instance, Pawn ___pawn, Trait trait, out bool __state)
        {
            // Only a trait the pawn did not have counts; a degree change is not
            // news, and neither is a pawn still being generated.
            __state = trait?.def != null
                && ___pawn != null
                && !PawnGenerator.IsBeingGenerated(___pawn)
                && PersonaHookBoundary.Inbox?.WantsEvolveEvent(PersonaGameEventKind.TraitGained) == true
                && !__instance.HasTrait(trait.def);
        }

        static void Postfix(TraitSet __instance, Pawn ___pawn, Trait trait, bool __state)
        {
            if (!__state || !__instance.HasTrait(trait.def)) return;
            PersonaHookBoundary.Inbox?.Record(new PersonaGameEvent { Kind = PersonaGameEventKind.TraitGained, Subject = ___pawn, Trait = trait });
        }
    }

    internal static class LifeEventHook
    {
        internal static void Record(PersonaGameEventKind kind, Pawn subject, Pawn other = null, Pawn third = null)
        {
            PersonaGameEventInbox inbox = PersonaHookBoundary.Inbox;
            if (subject == null || inbox == null || !inbox.WantsEvolveEvent(kind)) return;
            inbox.Record(new PersonaGameEvent { Kind = kind, Subject = subject, Other = other, Third = third });
        }
    }
}
