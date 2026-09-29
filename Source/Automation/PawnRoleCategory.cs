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
