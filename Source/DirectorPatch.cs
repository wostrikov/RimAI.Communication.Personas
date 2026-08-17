using HarmonyLib;
using Ustas.RimAI.Communication.Personas.Integration;
using Ustas.RimAI.Core.Handshake;
using Verse;

namespace Ustas.RimAI.Communication.Personas
{
    [StaticConstructorOnStartup]
    public static class Patcher
    {
        static Patcher()
        {
            if (!RimAiHandshake.IsApproved(RimAiModuleIds.Personas))
            {
                return;
            }

            var harmony = new Harmony("ustas.rimai.communication.personas");
            harmony.PatchAll();
            CommunicationBridge.Register();
            RimAiHandshakeRegistry.Current.MarkActivated(RimAiModuleIds.Personas);
        }
    }
}
