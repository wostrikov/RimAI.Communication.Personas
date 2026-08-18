using HarmonyLib;
using Ustas.RimAI.Communication.Personas.Integration;
using Ustas.RimAI.Core.Composition;
using Ustas.RimAI.Core.Handshake;
using Ustas.RimAI.Core.Modules;

namespace Ustas.RimAI.Communication.Personas;

/// <summary>
/// Module composition root for RimAI.Communication.Personas. Owns Harmony and
/// Communication bridge registration. Director warm-up stays on a LongEvent.
/// </summary>
public sealed class PersonasComposition : IRimAiModuleComposition
{
    public static PersonasComposition Current { get; } = new();

    public string ModuleId => RimAiModuleIds.Personas;

    public bool IsStarted { get; private set; }

    public void Start()
    {
        if (IsStarted)
            return;

        var harmony = new Harmony("ustas.rimai.communication.personas");
        harmony.PatchAll();
        CommunicationBridge.Register();
        RimAIModuleRegistry.Current.Register(new RimAIModuleDescriptor(
            "personas",
            "RimAI.Communication.Personas",
            "RimAI.Communication.Personas",
            "Communication",
            "RimAI.Communication"));
        IsStarted = true;
    }

    public void Stop()
    {
        IsStarted = false;
    }
}
