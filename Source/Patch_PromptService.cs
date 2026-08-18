using Verse;

namespace Ustas.RimAI.Communication.Personas
{
    /// <summary>
    /// TalkLifecycle Transform hook. Typed persona path presents precomputed projections;
    /// this remains an identity (no late string replace).
    /// </summary>
    public static class Patch_PromptService
    {
        public static string Transform(Pawn pawn, string result) => result;
    }
}
