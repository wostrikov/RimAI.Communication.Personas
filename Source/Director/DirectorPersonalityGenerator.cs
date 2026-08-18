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

namespace Ustas.RimAI.Communication.Personas;

public static class DirectorPersonalityGenerator
{
        private static async Task<PersonalityData> GenerateFromPreset(Pawn p, string presetName, bool isBatch)
        {
            // A. 查找预设
            var presets = Ustas.RimAI.Communication.API.RimTalkPromptAPI.GetAllPresets();
            var targetPreset = presets.FirstOrDefault(x => x.Name == presetName);
            if (targetPreset == null) return null;

            PromptContext contextObj;
            try
            {
                contextObj = new PromptContext(p);
                contextObj.PawnContext = PromptService.CreatePawnContext(p, PromptService.InfoLevel.Normal);
            }
            catch
            {
                if (PersonasMod.Settings.EnableDebugLog) Log.Warning("[Director] Failed to create Scriban Context.");
                return null;
            }

            // C. 扁平化构建 (Flattening)
            StringBuilder systemBuilder = new StringBuilder();
            StringBuilder userBuilder = new StringBuilder();

            foreach (var entry in targetPreset.Entries)
            {
                if (!entry.Enabled) continue;

                string renderedText = ScribanParser.Render(entry.Content, contextObj, true);
                if (string.IsNullOrWhiteSpace(renderedText)) continue;

                // 逻辑：System 角色放入 Context，其他角色放入 Prompt
                // 这样能最大程度保留信息，同时适配 Query 接口
                string roleStr = entry.Role.ToString();

                if (roleStr == "System")
                {
                    if (systemBuilder.Length > 0) systemBuilder.AppendLine("\n");
                    systemBuilder.Append(renderedText);
                }
                else
                {
                    // User, Assistant 等都放入 User 消息流
                    if (userBuilder.Length > 0) userBuilder.AppendLine("\n");
                    if (roleStr == "Assistant") userBuilder.Append("Assistant: "); // 简单标记一下 Assistant
                    userBuilder.Append(renderedText);
                }
            }

            if (systemBuilder.Length == 0 && userBuilder.Length == 0) return null;

            // D. 注入 JSON 协议
            string technicalProtocol = isBatch
                ? PersonasSettings.HiddenTechnicalPrompt_Batch
                : PersonasSettings.HiddenTechnicalPrompt_Single;

            // 协议追加在 User 内容最后
            userBuilder.AppendLine("\n" + technicalProtocol);

            // E. 发送请求
            // 使用标准的 AIService.Query
            var request = new TalkRequest(userBuilder.ToString(), p)
            {
                Context = systemBuilder.ToString()
            };

            return await AIService.Query<PersonalityData>(request);
        }

        private static string ExtractJsonSmart(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int startIndex = text.IndexOf('{');
            int endIndex = text.LastIndexOf('}');
            if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
            {
                return text.Substring(startIndex, endIndex - startIndex + 1);
            }
            return text;
        }

        public static async Task<PersonalityData> GeneratePersonalityTask(string characterData, string pawnNameForLog, Pawn pawn)
        {
            try
            {
                if (PersonasMod.Settings.EnableDebugLog)
                    Log.Message($"[Director] Gen Data for {pawnNameForLog}...");

                // 优先尝试高级预设
                string presetName = PersonasMod.Settings.rimTalkPreset_Single;
                if (!string.IsNullOrEmpty(presetName) && presetName != "None (Use Internal)")
                {
                    var result = await GenerateFromPreset(pawn, presetName, false);
                    if (result != null) return result;
                }

                // 回退到内置逻辑
                string userPrompt = PersonasMod.Settings.GetActivePrompt(false);
                if (string.IsNullOrEmpty(userPrompt)) userPrompt = PersonasSettings.DefaultPrompt_Standard;

                string instruction = userPrompt.Replace("{LANG}", DirectorPromptComposer.CurrentLanguage) + "\n" + PersonasSettings.HiddenTechnicalPrompt_Single;
                string data = $"[Character Data]\n{characterData}";

                // 标准调用：数据在 Prompt，指令在 Context
                var request = new TalkRequest(data, pawn)
                {
                    Context = instruction
                };

                // 直接调用 AIService，日志会自动记录
                return await AIService.Query<PersonalityData>(request);
            }
            catch (Exception e)
            {
                Log.Error($"[Director] Generation failed: {e.Message}");
                return new PersonalityData("Error generating persona.", 0.5f);
            }
        }

        public static async Task<PersonalityData> GenerateBatchPersonaTask(string combinedData, Pawn representative)
        {
            try
            {
                if (PersonasMod.Settings.EnableDebugLog)
                {
                    Log.Message($"[Director] Batch Gen Data:\n{combinedData}");
                }

                string userInstruction = PersonasMod.Settings.GetActivePrompt(false);
                if (string.IsNullOrEmpty(userInstruction)) userInstruction = PersonasSettings.DefaultPrompt_Standard;
                string finalInstruction = userInstruction.Replace("{LANG}", DirectorPromptComposer.CurrentLanguage) + "\n" + PersonasSettings.HiddenTechnicalPrompt_Batch;
                string finalData = $"[Character Data]\n{combinedData}";

                var request = new TalkRequest(finalData, representative)
                {
                    Context = finalInstruction
                };

                return await AIService.Query<PersonalityData>(request);
            }
            catch (Exception e)
            {
                Log.Error($"[Director] Batch Gen failed: {e.Message}");
                return null;
            }
        }

        public static void ApplyPersonalityToPawn(Pawn pawn, PersonalityData data)
        {
            if (pawn == null || pawn.Destroyed || data == null) return;

            try
            {
                var hediff = Hediff_Persona.GetOrAddNew(pawn);
                if (hediff != null)
                {
                    hediff.Personality = data.Persona.Trim();
                    // 状态激活
                    hediff.Severity = 1.0f;

                    // 数值处理
                    // 如果 AI 还是因为某些原因（比如旧 Prompt 缓存）返回了 > 1 的数，Clamp 会把它修剪到 1.0

                    if (data.Chattiness < 0.05f)
                    {
                        hediff.TalkInitiationWeight = 0.5f;
                    }
                    else
                    {
                        // 钳位到 0.1 - 1.0
                        // 这样即使旧数据是 1.8，也会变成 1.0，不会出错
                        hediff.TalkInitiationWeight = Mathf.Clamp(data.Chattiness, 0.1f, 1.0f);
                    }

                    // 4. 刷新
                    pawn.health.Notify_HediffChanged(hediff);
                }
            }
            catch (Exception e)
            {
                Log.Error($"[Director] Failed to apply personality: {e.Message}");
            }
        }

        public static int ParseAndApplyBatchResult(List<Pawn> pawns, string combinedPersona)
        {
            int appliedCount = 0;
            if (string.IsNullOrEmpty(combinedPersona)) return 0;

            var personaParts = combinedPersona.Split(new[] { "---" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in personaParts)
            {
                // 寻找闭合的方括号 ']' 作为分隔点。
                int bracketIndex = part.IndexOf(']');

                // 如果没找到方括号，说明格式彻底乱了，跳过
                if (bracketIndex == -1)
                {
                    if (PersonasMod.Settings.EnableDebugLog) Log.Warning($"[Director] Invalid format (no bracket found): {part.Trim()}");
                    continue;
                }

                // 1. 提取 Key，例如 "[ID:Human123]"
                // Substring(0, length) -> 从 0 开始，截取到 ']' 为止
                string keyPart = part.Substring(0, bracketIndex + 1).Trim();

                // 2. 提取内容。从 ']' 后面开始截取，并修 trimmed掉可能存在的冒号、空格、换行
                string text = part.Substring(bracketIndex + 1).TrimStart(':', ' ', '\n', '\r').Trim();

                Pawn target = null;

                // ★ 1. 优先尝试 ID 匹配 (最准确)
                if (keyPart.StartsWith("[ID:") && keyPart.EndsWith("]"))
                {
                    // 提取 ID: [ID:123] -> 123
                    // Substring(4) 跳过 "[ID:"，Length - 5 去掉头尾的 "[ID:" 和 "]"
                    if (keyPart.Length > 5)
                    {
                        string id = keyPart.Substring(4, keyPart.Length - 5);
                        target = pawns.FirstOrDefault(p => p.ThingID == id);
                    }
                }

                // 回退机制：尝试名字匹配 (兼容旧数据或 AI 格式错误)
                if (target == null)
                {
                    string cleanName = keyPart.TrimStart('[').TrimEnd(']').Trim();
                    // 去掉可能残留的 "ID:" 前缀 (万一代码走到这)
                    if (cleanName.StartsWith("ID:")) cleanName = cleanName.Substring(3);

                    target = pawns.FirstOrDefault(p => p.Name != null && p.Name.ToStringFull == cleanName)
                          ?? pawns.FirstOrDefault(p => p.LabelShortCap == cleanName);
                }

                if (target != null)
                {
                    ApplyPersonalityToPawn(target, new PersonalityData(text, 1.0f));
                    appliedCount++;
                }
                else
                {
                    if (PersonasMod.Settings.EnableDebugLog)
                        Log.Warning($"[Director] Could not match result key '{keyPart}' to any pawn.");
                }
            }
            return appliedCount;
        }

        public static string GetCurrentPersonality(Pawn pawn)
        {
            try
            {
                var hediff = Hediff_Persona.GetOrAddNew(pawn);
                if (!string.IsNullOrEmpty(hediff?.Personality))
                {
                    return hediff.Personality;
                }
            }
            catch { }
            return "RPD_Batch_PersonalityNotSet".Translate();
        }

        public static string BuildCombinedCharacterData(List<Pawn> pawns)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("--- Group Context ---");
            if (!string.IsNullOrEmpty(PersonasMod.Settings.directorNotes)) sb.AppendLine(PersonasMod.Settings.directorNotes);

            foreach (var p in pawns)
            {
                string nameStr = p.Name != null ? p.Name.ToStringFull : p.LabelShortCap;

                sb.AppendLine($"\n\n--- Character [ID:{p.ThingID}] Name: {nameStr} ---");
                sb.AppendLine(DirectorCharacterDataBuilder.BuildCustomCharacterData(p));
            }
            return sb.ToString();
        }

        public static void OpenRimTalkDialog(Pawn target)
        {
            try
            {
                Pawn initiator = null;
                Pawn selectedPawn = Find.Selector.SingleSelectedThing as Pawn;
                if (selectedPawn != null &&
                            !selectedPawn.Dead &&
                            selectedPawn.Spawned &&
                            selectedPawn != target &&
                            (!ModsConfig.AnomalyActive || !selectedPawn.def.race.IsAnomalyEntity))
                {
                    initiator = selectedPawn;
                }
                else
                {
                    initiator = Ustas.RimAI.Communication.Data.Cache.GetPlayer();
                }

                if (initiator == null || initiator == target)
                {
                    Messages.Message("Could not determine dialogue initiator.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                Find.WindowStack.Add(new CustomDialogueWindow(initiator, target));
            }
            catch (Exception ex)
            {
                Log.Error($"[Director] Failed to open RimTalk window: {ex}");
            }
        }
}
