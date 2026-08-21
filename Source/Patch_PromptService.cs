using RimAI.Core.Runtime;
using Verse;

namespace Ustas.RimAI.Communication.Personas
{
    /// <summary>
    /// TalkLifecycle Transform hook. Typed persona path presents precomputed projections;
    /// mutable prompt decoration is owned by Runtime.
    /// </summary>
    public static class Patch_PromptService
    {
        public static string Transform(Pawn pawn, string result)
        {
            var policy = RimAiRuntimeGateway.ResolvePersonasPromptPolicy(pawn?.ThingID);
            if (string.IsNullOrEmpty(policy.Decoration))
                return result;
            return policy.Decoration + result;
        }
    }
}
