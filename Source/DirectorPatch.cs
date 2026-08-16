using HarmonyLib;
using Ustas.RimAI.Communication.Personas.Integration;
using Verse;

namespace Ustas.RimAI.Communication.Personas
{
    [StaticConstructorOnStartup]
    public static class Patcher
    {
        static Patcher()
        {
            var harmony = new Harmony("ustas.rimai.communication.personas");
            harmony.PatchAll();
            CommunicationBridge.Register();
        }
    }
}
