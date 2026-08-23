using HarmonyLib;
using Ustas.RimAI.Communication.Client;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Prompt;
using Ustas.RimAI.Communication.Service;
using Ustas.RimAI.Communication.UI;
using Ustas.RimAI.Communication.Util;
using Ustas.RimAI.Core.Memory;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using Ustas.RimAI.Core.Diagnostics;
using Ustas.RimAI.Communication.Personas.Policy;

namespace Ustas.RimAI.Communication.Personas;

public static class DirectorPersonaEvolve
{
        public static (TalkRequest request, string currentPersona) PrepareEvolveRequest(Pawn p, Window editorWindow)
        {
            try
            {
                string currentPersona = GetWindowText(editorWindow);
                if (string.IsNullOrEmpty(currentPersona))
                {
                    var hediff = Hediff_Persona.GetOrAddNew(p);
                    currentPersona = hediff?.Personality;
                }
                if (string.IsNullOrEmpty(currentPersona)) return (null, null);

                DirectorDataEngine.TempCurrentPersona = currentPersona;

                string presetName = PersonasMod.Settings.rimTalkPreset_Evolve;
                string finalPrompt = "";
                string finalContext = "";

                if (!string.IsNullOrEmpty(presetName) && presetName != "None (Use Internal)")
                {
                    var presets = Ustas.RimAI.Communication.API.RimTalkPromptAPI.GetAllPresets();
                    var targetPreset = presets.FirstOrDefault(x => x.Name == presetName);

                    if (targetPreset != null)
                    {
                        PromptContext contextObj = new PromptContext(p);
                        StringBuilder systemSb = new StringBuilder();
                        StringBuilder userSb = new StringBuilder();

                        foreach (var entry in targetPreset.Entries)
                        {
                            if (!entry.Enabled) continue;

                            string renderedText = ScribanParser.Render(entry.Content, contextObj, true);

                            if (string.IsNullOrWhiteSpace(renderedText)) continue;

                            string roleStr = entry.Role.ToString().ToLowerInvariant();

                            if (roleStr == "system")
                            {
                                if (systemSb.Length > 0) systemSb.AppendLine("\n");
                                systemSb.Append(renderedText);
                            }
                            else
                            {
                                if (userSb.Length > 0) userSb.AppendLine("\n");
                                userSb.Append(renderedText);
                            }
                        }

                        // Hard constraint — changing this breaks an invariant. (JSON)
                        systemSb.AppendLine("\n" + PersonasSettings.HiddenTechnicalPrompt_Single);

                        finalContext = systemSb.ToString();
                        finalPrompt = userSb.ToString();

                        if (PersonasMod.Settings.EnableDebugLog)
                            RimAiLog.Info(RimAiLogCategory.Personas, $"[Director] Advanced Preset Rendered.\nContext Len: {finalContext.Length}\nPrompt Len: {finalPrompt.Length}");
                    }
                    else
                    {
                        RimAiLog.Warning(RimAiLogCategory.Personas, $"[Director] Preset '{presetName}' not found. Falling back to internal.");
                    }
                }

                if (string.IsNullOrEmpty(finalPrompt))
                {
                    var worldComp = Find.World.GetComponent<DirectorWorldComponent>();
                    string timeInfo = "No previous update record.";
                    string diffReport = "";
                    int lastTick = -1;

                    if (worldComp != null)
                    {
                        lastTick = worldComp.GetLastEvolveTick(p);
                        if (lastTick > 0)
                        {
                            int daysPassed = (GenTicks.TicksGame - lastTick) / 60000;
                            long ageThen = worldComp.GetLastEvolveBioAgeTicks(p) / 3600000;
                            long ageNow = p.ageTracker.AgeBiologicalYears;

                            timeInfo = $"Time passed since last update: {daysPassed} days.";
                            if (ageNow > ageThen) timeInfo += $" Character aged from {ageThen} to {ageNow}.";

                            if (PersonasMod.Settings.Context.Inc_DataComparison)
                            {
                                string oldSnapshot = worldComp.GetSnapshot(p);
                                if (!string.IsNullOrEmpty(oldSnapshot))
                                {
                                    string currentSnapshot = DirectorCharacterDataBuilder.BuildCustomCharacterData(p, true);
                                    diffReport = DirectorDiffReport.GenerateDiffReport(oldSnapshot, currentSnapshot);
                                }
                            }
                        }
                    }

                    StringBuilder contextSb = new StringBuilder();
                    var ctx = PersonasMod.Settings.Context;

                    contextSb.AppendLine("[Basic Info]");
                    contextSb.AppendLine($"Name: {p.LabelShortCap}");
                    contextSb.AppendLine($"Gender: {p.gender}");
                    contextSb.AppendLine($"Age: {p.ageTracker.AgeBiologicalYears}");
                    contextSb.AppendLine($"Status: {DirectorPawnStatus.GetPawnSocialStatus(p)}");
                    contextSb.AppendLine();

                    string memories = DirectorMemoryContext.GetExternalMemories(p, lastTick);
                    contextSb.Append(PersonaEvolutionPolicy.Compose(
                        currentPersona,
                        timeInfo,
                        diffReport,
                        PersonasMod.Settings.directorNotes,
                        ctx.Inc_DirectorNotes,
                        memories));

                    if (ctx.Inc_CommonKnowledge)
                    {
                        StringBuilder searchSource = new StringBuilder();
                        searchSource.Append($"{p.LabelShort} {p.gender} Age:{p.ageTracker.AgeBiologicalYears} ");
                        searchSource.Append($"{DirectorPawnStatus.GetPawnSocialStatus(p)} ");
                        searchSource.Append($"{currentPersona} ");
                        if (!string.IsNullOrEmpty(memories)) searchSource.Append($"{memories} ");
                        if (!string.IsNullOrEmpty(PersonasMod.Settings.directorNotes)) searchSource.Append($"{PersonasMod.Settings.directorNotes} ");

                        string ck = DirectorMemoryContext.GetCommonKnowledge(searchSource.ToString(), p);
                        if (!string.IsNullOrEmpty(ck))
                            contextSb.AppendLine($"[Common Knowledge]\n{ck}\n");
                    }

                    string userInstruction = PersonasMod.Settings.presets[3].text.Replace("{LANG}", Constant.Lang);
                    string technicalProtocol = PersonasSettings.HiddenTechnicalPrompt_Single;

                    finalContext = userInstruction + "\n\n" + technicalProtocol;
                    finalPrompt = "[Update Data]\n" + contextSb.ToString();
                }

                // Threading/concurrency constraint — do not race this state. (D. TalkRequest)
                var request = new TalkRequest(finalPrompt, p)
                {
                    Context = finalContext
                };

                return (request, currentPersona);
            }
            catch (Exception ex)
            {
                RimAiLog.Error(RimAiLogCategory.Personas, $"[Director] PrepareEvolveRequest failed: {ex}");
                return (null, null);
            }
            finally
            {
                DirectorDataEngine.TempCurrentPersona = "";
            }
        }

        public static PersonalityData ExecuteEvolveTask(TalkRequest request)
        {
            try
            {
                var task = AIService.Query<PersonalityData>(request);
                return task.Result;
            }
            catch (Exception ex)
            {
                RimAiLog.Error(RimAiLogCategory.Personas, $"[Director] AI Request failed: {ex.Message}");
                return null;
            }
        }

        public static string ExecuteEvolve(TalkRequest request, string originalPersona)
        {
            if (request == null) return null;

            try
            {
                // Threading/concurrency constraint — do not race this state.
                Task<PersonalityData> task = AIService.Query<PersonalityData>(request);
                PersonalityData result = task.Result;  // Threading/concurrency constraint — do not race this state. (UI)

                if (result != null && !string.IsNullOrEmpty(result.Persona))
                {
                    return result.Persona.Trim();
                }
            }
            catch (Exception ex)
            {
                // Threading/concurrency constraint — do not race this state.
                RimAiLog.Error(RimAiLogCategory.Personas, $"[Director] Evolve execution failed: {ex.Message}");
            }
            return null;
        }

        public static void TryLogErrorToApiHistory(TalkRequest request, Exception ex)
        {
            try
            {
                var apiLog = ApiHistory.AddRequest(request, Channel.Query);
                if (apiLog != null)
                {
                    apiLog.IsError = true;
                    apiLog.Response = $"[Director] Task failed: {ex.Message}";
                }
            }
            catch { }
        }

        public static string GetWindowText(Window window)
        {
            return window is PersonaEditorWindow editor ? editor.EditingPersonality : null;
        }

        public static void SetWindowText(Window window, string text)
        {
            if (window is PersonaEditorWindow editor)
                editor.EditingPersonality = text;
        }
}
