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

namespace Ustas.RimAI.Communication.Personas;

public static class DirectorPersonaEvolve
{
        public static (TalkRequest request, string currentPersona) PrepareEvolveRequest(Pawn p, Window editorWindow)
        {
            try
            {
                // A. 获取当前文本 (UI 操作)
                string currentPersona = GetWindowText(editorWindow);
                if (string.IsNullOrEmpty(currentPersona))
                {
                    var hediff = Hediff_Persona.GetOrAddNew(p);
                    currentPersona = hediff?.Personality;
                }
                if (string.IsNullOrEmpty(currentPersona)) return (null, null);

                // B. 设置临时缓存 (给 Scriban {{director_evolve_current_persona}} 使用)
                DirectorDataEngine.TempCurrentPersona = currentPersona;

                // C. 决定使用哪种逻辑
                string presetName = PersonasMod.Settings.rimTalkPreset_Evolve;
                string finalPrompt = "";
                string finalContext = "";

                // --- 分支 1: 使用 RimTalk 高级预设 ---
                if (!string.IsNullOrEmpty(presetName) && presetName != "None (Use Internal)")
                {
                    // 1. 查找预设
                    var presets = Ustas.RimAI.Communication.API.RimTalkPromptAPI.GetAllPresets();
                    var targetPreset = presets.FirstOrDefault(x => x.Name == presetName);

                    if (targetPreset != null)
                    {
                        // 2. 准备渲染上下文 (利用反射创建 Context)
                        PromptContext contextObj = new PromptContext(p);
                        StringBuilder systemSb = new StringBuilder();
                        StringBuilder userSb = new StringBuilder();

                        foreach (var entry in targetPreset.Entries)
                        {
                            if (!entry.Enabled) continue;

                            string renderedText = ScribanParser.Render(entry.Content, contextObj, true);

                            if (string.IsNullOrWhiteSpace(renderedText)) continue;

                            // 根据角色拼接到不同的缓冲区
                            // Ustas.RimAI.Communication.Data.Role 枚举: System, User, AI
                            // PromptEntry.Role 可能是字符串也可能是枚举，我们要判断
                            string roleStr = entry.Role.ToString().ToLowerInvariant();

                            if (roleStr == "system")
                            {
                                if (systemSb.Length > 0) systemSb.AppendLine("\n");
                                systemSb.Append(renderedText);
                            }
                            else
                            {
                                // User 或 Assistant 都作为 Prompt 的一部分
                                if (userSb.Length > 0) userSb.AppendLine("\n");
                                userSb.Append(renderedText);
                            }
                        }

                        // 4. 加上 JSON 协议 (这是硬性要求，必须加在最后)
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

                // --- 分支 2: 使用内置逻辑 (保底或默认) ---
                if (string.IsNullOrEmpty(finalPrompt))
                {
                    // 准备内置数据
                    var worldComp = Find.World.GetComponent<DirectorWorldComponent>();
                    string timeInfo = "No previous update record.";
                    string comparisonBlock = "";
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
                                    string diffReport = DirectorDiffReport.GenerateDiffReport(oldSnapshot, currentSnapshot);
                                    comparisonBlock = $"\n[Status Changes (since last update)]:\n{diffReport}\n";
                                }
                            }
                        }
                    }

                    // 组装数据包
                    StringBuilder contextSb = new StringBuilder();
                    var ctx = PersonasMod.Settings.Context;

                    contextSb.AppendLine("[Basic Info]");
                    contextSb.AppendLine($"Name: {p.LabelShortCap}");
                    contextSb.AppendLine($"Gender: {p.gender}");
                    contextSb.AppendLine($"Age: {p.ageTracker.AgeBiologicalYears}");
                    contextSb.AppendLine($"Status: {DirectorPawnStatus.GetPawnSocialStatus(p)}");
                    contextSb.AppendLine();

                    contextSb.AppendLine($"[Previous Persona (The Starting Point)]\n{currentPersona}\n");
                    contextSb.AppendLine($"[Time Context]\n{timeInfo}\n");

                    if (!string.IsNullOrEmpty(comparisonBlock)) contextSb.AppendLine(comparisonBlock);

                    if (ctx.Inc_DirectorNotes && !string.IsNullOrEmpty(PersonasMod.Settings.directorNotes))
                        contextSb.AppendLine($"[Director's Notes]\n{PersonasMod.Settings.directorNotes}\n");

                    string memories = DirectorMemoryContext.GetExternalMemories(p, lastTick);
                    if (!string.IsNullOrEmpty(memories))
                        contextSb.AppendLine($"[New Memories]\n{memories}\n");
                    else
                        contextSb.AppendLine("[New Memories]\nNo new significant memories since last update.\n");

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

                    // 组装最终结果
                    string userInstruction = PersonasMod.Settings.presets[3].text.Replace("{LANG}", Constant.Lang);
                    string technicalProtocol = PersonasSettings.HiddenTechnicalPrompt_Single;

                    // 指令进 Context
                    finalContext = userInstruction + "\n\n" + technicalProtocol;
                    // 数据进 Prompt
                    finalPrompt = "[Update Data]\n" + contextSb.ToString();
                }

                // D. 构造 TalkRequest
                // 必须在主线程构造
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
                // 确保缓存被清理
                DirectorDataEngine.TempCurrentPersona = "";
            }
        }

        public static PersonalityData ExecuteEvolveTask(TalkRequest request)
        {
            try
            {
                // 调用 AIService.Query (它内部是异步的，但在 Task.Run 里我们可以直接 .Result 阻塞等待)
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
                // ★★★ 核心：在后台线程中阻塞等待 ★★★
                Task<PersonalityData> task = AIService.Query<PersonalityData>(request);
                PersonalityData result = task.Result; // 阻塞后台线程，不影响 UI

                if (result != null && !string.IsNullOrEmpty(result.Persona))
                {
                    return result.Persona.Trim();
                }
            }
            catch (Exception ex)
            {
                // 后台线程记录错误
                RimAiLog.Error(RimAiLogCategory.Personas, $"[Director] Evolve execution failed: {ex.Message}");
                // (可选) 调用 TryLogErrorToApiHistory
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
