using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Personas.Automation
{
    /// <summary>A pawn's standing towards the player's colony, as Auto-Gen sees it.</summary>
    public enum PawnRoleCategory
    {
        None,
        Colonist,
        Prisoner,
        Slave,
        Visitor,
        Enemy,
        Other
    }

    /// <summary>
    /// Where a pawn stood at one moment: its role and the factions that role is
    /// made of. Taken before a faction or guest-status change so the persona
    /// update can say what the pawn came from; the factions are kept as they
    /// are and put into words later, on the world tick.
    /// </summary>
    public struct PawnRoleSnapshot
    {
        public PawnRoleCategory Role;
        public Faction Faction;
        // Whose prisoner the pawn is, and the faction a slave was taken from.
        public Faction HeldBy;
        public Faction TakenFrom;

        public static PawnRoleSnapshot Of(Pawn pawn)
        {
            var snapshot = new PawnRoleSnapshot { Role = PawnRoleClassifier.Classify(pawn) };
            if (snapshot.Role == PawnRoleCategory.None) return snapshot;
            snapshot.Faction = pawn.Faction;
            Pawn_GuestTracker guest = pawn.guest;
            if (guest != null)
            {
                if (guest.IsPrisoner) snapshot.HeldBy = guest.HostFaction;
                if (guest.IsSlave) snapshot.TakenFrom = guest.SlaveFaction;
            }
            return snapshot;
        }
    }

    public static class PawnRoleClassifier
    {
        public static readonly PawnRoleCategory[] Ordered =
        {
            PawnRoleCategory.Colonist,
            PawnRoleCategory.Prisoner,
            PawnRoleCategory.Slave,
            PawnRoleCategory.Visitor,
            PawnRoleCategory.Enemy,
            PawnRoleCategory.Other
        };

        public static PawnRoleCategory Classify(Pawn pawn)
        {
            if (pawn?.RaceProps == null || !pawn.RaceProps.Humanlike) return PawnRoleCategory.None;

            // Prisoners and slaves first: both answer yes to questions about the
            // player's faction that a free colonist would.
            if (pawn.IsPrisonerOfColony) return PawnRoleCategory.Prisoner;
            if (pawn.IsSlaveOfColony) return PawnRoleCategory.Slave;
            if (pawn.IsFreeColonist) return PawnRoleCategory.Colonist;

            Faction player = Faction.OfPlayerSilentFail;
            if (pawn.Faction != null && player != null && pawn.Faction != player)
                return pawn.Faction.HostileTo(player) ? PawnRoleCategory.Enemy : PawnRoleCategory.Visitor;

            return PawnRoleCategory.Other;
        }

        public static string Label(PawnRoleCategory category)
        {
            switch (category)
            {
                case PawnRoleCategory.Colonist: return "RPD_AutoGen_CategoryColonist".Translate();
                case PawnRoleCategory.Prisoner: return "RPD_AutoGen_CategoryPrisoner".Translate();
                case PawnRoleCategory.Slave: return "RPD_AutoGen_CategorySlave".Translate();
                case PawnRoleCategory.Visitor: return "RPD_AutoGen_CategoryVisitor".Translate();
                case PawnRoleCategory.Enemy: return "RPD_AutoGen_CategoryEnemy".Translate();
                case PawnRoleCategory.Other: return "RPD_AutoGen_CategoryOther".Translate();
                default: return category.ToString();
            }
        }
    }
}
