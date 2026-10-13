using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Ustas.RimAI.Communication.Personas.History
{
    /// <summary>
    /// Every pawn the game still holds, asked before anything kept per pawn is
    /// thrown away. It errs towards keeping.
    ///
    /// PawnsFinder's "alive or dead" lists are the living pawns plus the dead
    /// world pawns, and a colonist who died on a map is neither: MapPawns
    /// leaves the dead out of what it reports as unspawned, so the body in the
    /// grave - which a resurrector serum can still bring back - is in none of
    /// them. Caravans and transporters in flight are a list of their own as
    /// well, and AllMapsWorldAndTemporary_AliveOrDead, the obvious one to
    /// ask, does not include it. Asked that way, a pawn that is only away or
    /// only dead loses its persona history and its Auto-Evolve choices on the
    /// next load.
    /// </summary>
    internal static class PersonaPawnCensus
    {
        static readonly List<Pawn> HeldPawns = new List<Pawn>();
        static readonly List<Thing> CarriedThings = new List<Thing>();

        public static HashSet<int> KnownIds()
        {
            var known = new HashSet<int>();
            foreach (Pawn pawn in PawnsFinder.All_AliveOrDead)
            {
                if (pawn != null) known.Add(pawn.thingIDNumber);
            }

            // Whatever a map holds that is not standing on it: the dead inside
            // their corpses, spawned or buried, among them.
            List<Map> maps = Find.Maps;
            if (maps != null)
            {
                foreach (Map map in maps)
                {
                    HeldPawns.Clear();
                    ThingOwnerUtility.GetAllThingsRecursively(map, ThingRequest.ForGroup(ThingRequestGroup.Pawn), HeldPawns,
                        allowUnreal: true, passCheck: null, alsoGetSpawnedThings: false);
                    foreach (Pawn pawn in HeldPawns)
                    {
                        if (pawn != null) known.Add(pawn.thingIDNumber);
                    }
                }
                HeldPawns.Clear();
            }

            // And a body a caravan is carrying home.
            List<Caravan> caravans = Find.WorldObjects?.Caravans;
            if (caravans != null)
            {
                foreach (Caravan caravan in caravans)
                {
                    CarriedThings.Clear();
                    ThingOwnerUtility.GetAllThingsRecursively(caravan, CarriedThings);
                    foreach (Thing thing in CarriedThings)
                    {
                        if (thing is Pawn pawn) known.Add(pawn.thingIDNumber);
                    }
                }
                CarriedThings.Clear();
            }

            return known;
        }
    }
}
