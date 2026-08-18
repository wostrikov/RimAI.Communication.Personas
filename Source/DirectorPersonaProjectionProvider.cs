using Ustas.RimAI.Core.Personas;
using Verse;

namespace Ustas.RimAI.Communication.Personas;

/// <summary>
/// Renders pawn personality templates once for PromptContext.TypedPersonaProjections.
/// Uses the pawn hint from Attach — no ThreadStatic lookup required on the Talk attach path.
/// </summary>
public sealed class DirectorPersonaProjectionProvider : IPersonaProjectionProvider
{
    public PersonaProjection GetProjection(string pawnId, string rawPersonality, object pawnHint)
    {
        string raw = rawPersonality ?? string.Empty;
        string projection = raw;
        if (raw.IndexOf("{{", System.StringComparison.Ordinal) >= 0)
        {
            Pawn pawn = pawnHint as Pawn;
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
}
