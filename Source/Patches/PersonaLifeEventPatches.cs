using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Ustas.RimAI.Communication.Personas.Automation;
using Verse;

namespace Ustas.RimAI.Communication.Personas.Patches
{
    // Life events that can ask Auto-Evolve for an update. Each hook checks its
    // own switch first, so with Auto-Evolve off they cost one settings read on
    // the vanilla path, and otherwise they only record the event.

    internal static class Patch_PersonaEvolveOnMarriage
    {
        static void Postfix(Pawn firstPawn, Pawn secondPawn)
        {
            LifeEventHook.Record(PersonaGameEventKind.Marriage, firstPawn, secondPawn);
        }
    }

    internal static class Patch_PersonaEvolveOnDivorce
    {
        static void Postfix(Pawn initiator, Pawn recipient)
        {
            LifeEventHook.Record(PersonaGameEventKind.Breakup, initiator, recipient);
        }
    }

    internal static class Patch_PersonaEvolveOnBreakup
    {
        static void Postfix(Pawn initiator, Pawn recipient)
        {
            LifeEventHook.Record(PersonaGameEventKind.Breakup, initiator, recipient);
        }
    }

    // A rejected proposal can end the relationship outright: the asker's lover
    // or fiancé becomes an ex, and nothing passes through InteractionWorker_Breakup.
    internal static class Patch_PersonaEvolveOnRejectedProposal
    {
        static void Prefix(Pawn initiator, Pawn recipient, out bool __state)
        {
            __state = initiator?.relations != null && recipient != null
                && (initiator.relations.DirectRelationExists(PawnRelationDefOf.Lover, recipient)
                    || initiator.relations.DirectRelationExists(PawnRelationDefOf.Fiance, recipient));
        }

        static void Postfix(Pawn initiator, Pawn recipient, bool __state)
        {
            if (!__state || initiator?.relations == null || recipient == null) return;
            if (LovePartnerRelationUtility.LovePartnerRelationExists(initiator, recipient)) return;
            LifeEventHook.Record(PersonaGameEventKind.Breakup, initiator, recipient);
        }
    }

    // A successful romance attempt leaves the old lovers and fiancés of both
    // pawns, as many as their ideology no longer allows; each is a breakup.
    internal static class Patch_PersonaEvolveOnRomanceReplacement
    {
        static void Postfix(Pawn pawn, ref List<Pawn> oldLoversAndFiances)
        {
            if (pawn == null || oldLoversAndFiances == null) return;
            for (int i = 0; i < oldLoversAndFiances.Count; i++)
                LifeEventHook.Record(PersonaGameEventKind.Breakup, pawn, oldLoversAndFiances[i]);
        }
    }

    // The birth path without Biotech's PregnancyUtility, still taken by animals
    // and by mods that spawn a newborn the old way. It does not return the child.
    internal static class Patch_PersonaEvolveOnLegacyBirth
    {
        static void Postfix(Pawn mother, Pawn father)
        {
            if (mother?.RaceProps == null || !mother.RaceProps.Humanlike) return;
            LifeEventHook.RecordBirth(null, mother, father);
        }
    }

    internal static class Patch_PersonaEvolveOnBirth
    {
        static void Postfix(Thing __result, Pawn geneticMother, Thing birtherThing, Pawn father)
        {
            // A stillbirth returns a corpse, not a child; that is a different
            // event and the "became a parent" context would misdescribe it.
            if (!(__result is Pawn child) || child.Dead) return;
            LifeEventHook.RecordBirth(child, geneticMother ?? birtherThing as Pawn, father);
        }
    }

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

        /// <summary>A birth is the mother's event; the child may be unknown, so it rides along as Third.</summary>
        internal static void RecordBirth(Pawn child, Pawn mother, Pawn father)
        {
            PersonaGameEventInbox inbox = PersonaHookBoundary.Inbox;
            if (mother == null && father == null) return;
            if (inbox == null || !inbox.WantsEvolveEvent(PersonaGameEventKind.Birth)) return;
            inbox.Record(new PersonaGameEvent { Kind = PersonaGameEventKind.Birth, Subject = mother ?? father, Other = father, Third = child });
        }
    }
}
