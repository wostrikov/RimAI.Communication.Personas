using System.Collections.Generic;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Prompt;
using Ustas.RimAI.Communication.UI;
using Ustas.RimAI.Core.Communication;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Personas.Integration;

public static class CommunicationBridge
{
    static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;
        _registered = true;
        PersonaService.OverrideGenerator = new DirectorPersonaGenerator();
        Hediff_Persona.SelectPersonality = Patch_GetOrAddNew.AssignViaRulesOrRandom;
        TalkLifecycle.ScribanRenderStarted += OnScribanRenderStarted;
        TalkLifecycle.ContextBuildStarted += OnContextBuildStarted;
        TalkLifecycle.ContextBuildCompleted += OnContextBuildCompleted;
        TalkLifecycle.PromptDecorateStarted += OnPromptDecorateStarted;
        TalkLifecycle.PromptDecorated += OnPromptDecorated;
        TalkLifecycle.PawnContextTransformed += OnPawnContextTransformed;
        PersonaEditorChrome.DrawFooter += Patch_PersonaEditorWindow_DirectorFeatures.DrawFooter;
        PersonaEditorChrome.TryHandleRollGen += HandleRollGen;
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
