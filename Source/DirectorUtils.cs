using Ustas.RimAI.Communication.Data;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Verse;

namespace Ustas.RimAI.Communication.Personas;

public static class DirectorUtils
{
    public static float NormalizeChattiness(float chattiness) =>
        DirectorPromptComposer.NormalizeChattiness(chattiness);

    public static string CurrentLanguage => DirectorPromptComposer.CurrentLanguage;

    public static string GetFinalPrompt(bool isBatch, string dataContent) =>
        DirectorPromptComposer.GetFinalPrompt(isBatch, dataContent);

    public static Task<PersonalityData> GeneratePersonalityTask(string characterData, string pawnNameForLog, Pawn pawn) =>
        DirectorPersonalityGenerator.GeneratePersonalityTask(characterData, pawnNameForLog, pawn);

    public static Task<PersonalityData> GenerateBatchPersonaTask(string combinedData, Pawn representative) =>
        DirectorPersonalityGenerator.GenerateBatchPersonaTask(combinedData, representative);

    public static void ApplyPersonalityToPawn(Pawn pawn, PersonalityData data) =>
        DirectorPersonalityGenerator.ApplyPersonalityToPawn(pawn, data);

    public static int ParseAndApplyBatchResult(List<Pawn> pawns, string combinedPersona) =>
        DirectorPersonalityGenerator.ParseAndApplyBatchResult(pawns, combinedPersona);

    public static string GetCurrentPersonality(Pawn pawn) =>
        DirectorPersonalityGenerator.GetCurrentPersonality(pawn);

    public static string BuildCombinedCharacterData(List<Pawn> pawns) =>
        DirectorPersonalityGenerator.BuildCombinedCharacterData(pawns);

    public static void OpenRimTalkDialog(Pawn target) =>
        DirectorPersonalityGenerator.OpenRimTalkDialog(target);

    public static string GetExternalMemories(Pawn p, int lastTick) =>
        DirectorMemoryContext.GetExternalMemories(p, lastTick);

    public static string GetCommonKnowledge(string context, Pawn p) =>
        DirectorMemoryContext.GetCommonKnowledge(context, p);

    public static (TalkRequest request, string currentPersona) PrepareEvolveRequest(Pawn p, Window editorWindow) =>
        DirectorPersonaEvolve.PrepareEvolveRequest(p, editorWindow);

    public static PersonalityData ExecuteEvolveTask(TalkRequest request) =>
        DirectorPersonaEvolve.ExecuteEvolveTask(request);

    public static string ExecuteEvolve(TalkRequest request, string originalPersona) =>
        DirectorPersonaEvolve.ExecuteEvolve(request, originalPersona);

    public static void TryLogErrorToApiHistory(TalkRequest request, Exception ex) =>
        DirectorPersonaEvolve.TryLogErrorToApiHistory(request, ex);

    public static string GenerateDiffReport(string oldSnapshot, string newSnapshot) =>
        DirectorDiffReport.GenerateDiffReport(oldSnapshot, newSnapshot);

    public static string GetWindowText(Window window) =>
        DirectorPersonaEvolve.GetWindowText(window);

    public static void SetWindowText(Window window, string text) =>
        DirectorPersonaEvolve.SetWindowText(window, text);

    public static string GetPawnSocialStatus(Pawn p) =>
        DirectorPawnStatus.GetPawnSocialStatus(p);

    public static string BuildCustomCharacterData(Pawn p, bool isSnapshot = false, bool simpleEquipment = false) =>
        DirectorCharacterDataBuilder.BuildCustomCharacterData(p, isSnapshot, simpleEquipment);

    public static string GetTemporaryColonistDescription(Pawn p) =>
        DirectorPawnStatus.GetTemporaryColonistDescription(p);

    public static string GetPawnShortStatus(Pawn p) =>
        DirectorPawnStatus.GetPawnShortStatus(p);

    public static Def GetVSEPassionDef(SkillRecord skill) =>
        DirectorModCompat.GetVSEPassionDef(skill);

    public static string GetPassionInfoHardcoded(Passion passion) =>
        DirectorModCompat.GetPassionInfoHardcoded(passion);

    public static string GetMauxRimPsycheData(Pawn p) =>
        DirectorModCompat.GetMauxRimPsycheData(p);

    public static string RenderScribanText(string rawText, Pawn p) =>
        DirectorPromptComposer.RenderScribanText(rawText, p);
}
