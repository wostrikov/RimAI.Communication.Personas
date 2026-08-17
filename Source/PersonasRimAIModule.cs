using Ustas.RimAI.Core.Handshake;
using Ustas.RimAI.Core.Modules;
using Verse;

namespace Ustas.RimAI.Communication.Personas
{
    [StaticConstructorOnStartup]
    public static class PersonasRimAIModule
    {
        static PersonasRimAIModule()
        {
            if (!RimAiHandshake.IsApproved(RimAiModuleIds.Personas))
            {
                return;
            }

            RimAIModuleRegistry.Current.Register(new RimAIModuleDescriptor(
                "personas",
                "RimAI.Communication.Personas",
                "RimAI.Communication.Personas",
                "Communication",
                "RimAI.Communication"));
        }
    }
}
