using System.Collections.Generic;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Prompt;
using Ustas.RimAI.Communication.UI;
using Ustas.RimAI.Core.Communication;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Personas.Integration;

/// <summary>
/// Owns Communication hook wiring for Personas. Register/Unregister are idempotent
/// and only remove Personas-owned handlers (never TalkLifecycle.Clear()).
/// </summary>
public static class CommunicationBridge
{
    static bool _registered;
    static IPersonaGenerator _ownedGenerator;
    static System.Func<System.Collections.Generic.IEnumerable<PersonalityData>, Pawn, PersonalityData> _ownedSelectPersonality;

    public static void Register()
    {
        if (_registered)
            return;
        _registered = true;
        _ownedGenerator = new DirectorPersonaGenerator();
        _ownedSelectPersonality = PersonaResolver.AssignViaRulesOrRandom;
        PersonaService.OverrideGenerator = _ownedGenerator;
        Hediff_Persona.SelectPersonality = _ownedSelectPersonality;
        TalkLifecycle.ScribanRenderStarted += OnScribanRenderStarted;
        TalkLifecycle.ContextBuildStarted += OnContextBuildStarted;
        TalkLifecycle.ContextBuildCompleted += OnContextBuildCompleted;
        TalkLifecycle.PromptDecorateStarted += OnPromptDecorateStarted;
        TalkLifecycle.PromptDecorated += OnPromptDecorated;
        TalkLifecycle.PawnContextTransformed += OnPawnContextTransformed;
        PersonaEditorChrome.DrawFooter += Patch_PersonaEditorWindow_DirectorFeatures.DrawFooter;
        PersonaEditorChrome.TryHandleRollGen += HandleRollGen;
    }

    /// <summary>
    /// Unsubscribes Personas-owned handlers only. Does not call TalkLifecycle.Clear()
    /// (that would wipe Memory/Voices/Relations subscribers).
    /// </summary>
    public static void Unregister()
    {
        if (!_registered)
            return;

        TalkLifecycle.ScribanRenderStarted -= OnScribanRenderStarted;
        TalkLifecycle.ContextBuildStarted -= OnContextBuildStarted;
        TalkLifecycle.ContextBuildCompleted -= OnContextBuildCompleted;
        TalkLifecycle.PromptDecorateStarted -= OnPromptDecorateStarted;
        TalkLifecycle.PromptDecorated -= OnPromptDecorated;
        TalkLifecycle.PawnContextTransformed -= OnPawnContextTransformed;
        PersonaEditorChrome.DrawFooter -= Patch_PersonaEditorWindow_DirectorFeatures.DrawFooter;
        PersonaEditorChrome.TryHandleRollGen -= HandleRollGen;

        if (ReferenceEquals(PersonaService.OverrideGenerator, _ownedGenerator))
            PersonaService.OverrideGenerator = null;
        if (Hediff_Persona.SelectPersonality == _ownedSelectPersonality)
            Hediff_Persona.SelectPersonality = null;

        _ownedGenerator = null;
        _ownedSelectPersonality = null;
        DirectorContextTracker.Clear();
        _registered = false;
    }

    static void OnScribanRenderStarted(object context)
    {
        if (context is PromptContext promptContext && promptContext.AllPawns != null && promptContext.AllPawns.Count > 0)
            DirectorContextTracker.SetPawns(promptContext.AllPawns);
    }

    static void OnContextBuildStarted(object pawns)
    {
        if (pawns is List<Pawn> list)
            DirectorContextTracker.SetPawns(list);
    }

    static void OnContextBuildCompleted() => DirectorContextTracker.Clear();

    static void OnPromptDecorateStarted(object pawns)
    {
        if (pawns is List<Pawn> list)
            DirectorContextTracker.SetPawns(list);
    }

    static void OnPromptDecorated(object _, object __, string ___) => DirectorContextTracker.Clear();

    static string OnPawnContextTransformed(object pawnObj, string result)
    {
        return Patch_PromptService.Transform(pawnObj as Pawn, result);
    }

    static bool HandleRollGen(PersonaEditorWindow window, Pawn pawn)
    {
        if (window == null || pawn == null)
            return false;
        Find.WindowStack.Add(new Window_PresetBrowser(pawn, window));
        if (string.IsNullOrEmpty(window.EditingPersonality))
            window.EditingPersonality = " ";
        return true;
    }

    sealed class DirectorPersonaGenerator : IPersonaGenerator
    {
        public bool TryGenerate(Pawn pawn, out Task<PersonalityData> result)
        {
            string preGeneratedData = DirectorUtils.BuildCustomCharacterData(pawn);
            string safeName = pawn.LabelShortCap;
            result = DirectorUtils.GeneratePersonalityTask(preGeneratedData, safeName, pawn);
            return true;
        }
    }
}
