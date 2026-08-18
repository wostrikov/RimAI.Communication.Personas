using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Core.Personas;
using Verse;

namespace Ustas.RimAI.Communication.Personas;

/// <summary>
/// Renders pawn personality templates once for PromptContext.TypedPersonaProjections.
/// Owned by <see cref="PersonasComposition"/> via <see cref="PersonaProjectionAccess"/>.
/// </summary>
public sealed class DirectorPersonaProjectionProvider : IPersonaProjectionProvider
{
    public PersonaProjection GetProjection(string pawnId, string rawPersonality)
    {
        string raw = rawPersonality ?? string.Empty;
        string projection = raw;
        if (raw.IndexOf("{{", System.StringComparison.Ordinal) >= 0)
        {
            Pawn pawn = ResolvePawn(pawnId);
            if (pawn != null)
                projection = DirectorUtils.RenderScribanText(raw, pawn) ?? raw;
        }

        return new PersonaProjection
        {
            PawnId = pawnId ?? string.Empty,
            RawPersonality = raw,
            Projection = projection,
            Source = "typed",
        };
    }

    static Pawn ResolvePawn(string pawnId)
    {
        if (string.IsNullOrEmpty(pawnId))
            return null;

        var tracked = DirectorContextTracker.GetPawns();
        if (tracked != null)
        {
            for (int i = 0; i < tracked.Count; i++)
            {
                var p = tracked[i];
                if (p != null && p.ThingID == pawnId)
                    return p;
            }
        }

        if (Find.Maps == null)
            return null;

        foreach (var map in Find.Maps)
        {
            if (map?.mapPawns?.AllPawns == null)
                continue;
            foreach (var p in map.mapPawns.AllPawns)
            {
                if (p != null && p.ThingID == pawnId)
                    return p;
            }
        }

        return null;
    }
}
