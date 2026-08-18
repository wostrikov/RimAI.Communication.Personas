using HarmonyLib;
using Ustas.RimAI.Communication.Personas.Integration;
using Ustas.RimAI.Core.Composition;
using Ustas.RimAI.Core.Handshake;
using Ustas.RimAI.Core.Modules;
using Verse;

namespace Ustas.RimAI.Communication.Personas;

/// <summary>
/// Module composition root for RimAI.Communication.Personas.
/// Owns Harmony (process lifetime), Communication bridge, Director Scriban surface,
/// and Director library warm-up scheduling. LongEvent preserves RimWorld load timing.
/// </summary>
public sealed class PersonasComposition : IRimAiModuleComposition
{
    public static PersonasComposition Current { get; } = new();

    public string ModuleId => RimAiModuleIds.Personas;

    public bool IsStarted { get; private set; }

    Harmony _harmony;

    public void Start()
    {
        if (IsStarted)
            return;

        _harmony = new Harmony("ustas.rimai.communication.personas");
        _harmony.PatchAll();
        CommunicationBridge.Register();
        RimAIModuleRegistry.Current.Register(new RimAIModuleDescriptor(
            "personas",
            "RimAI.Communication.Personas",
            "RimAI.Communication.Personas",
            "Communication",
            "RimAI.Communication"));

        // Defer Director warm-up and Scriban surface until after defs/Constant settle
        // (same LongEvent timing as the former PersonasMod / StaticConstructor paths).
        LongEventHandler.ExecuteWhenFinished(DirectorStartup.Initialize);
        LongEventHandler.ExecuteWhenFinished(DirectorApiAdapter.RegisterSurface);

        IsStarted = true;
    }

    public void Stop()
    {
        if (!IsStarted)
            return;

        // Do not UnpatchAll — Harmony is process-lifetime (matches Memory/Communication).
        CommunicationBridge.Unregister();
        DirectorApiAdapter.UnregisterSurface();
        PersonaResolver.ClearAssignmentCache();
        IsStarted = false;
    }
}
