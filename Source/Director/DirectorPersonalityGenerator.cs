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
using Ustas.RimAI.Communication.Personas.Diagnostics;

namespace Ustas.RimAI.Communication.Personas;

public static class DirectorPersonalityGenerator
{
        private static async Task<PersonalityData> GenerateFromPreset(Pawn p, string presetName, bool isBatch)
        {
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
                if (PersonasMod.Settings.EnableDebugLog) RimAiLog.Warning(RimAiLogCategory.Personas, "[Director] Failed to create Scriban Context.");
                return null;
            }

            StringBuilder systemBuilder = new StringBuilder();
            StringBuilder userBuilder = new StringBuilder();

            foreach (var entry in targetPreset.Entries)
            {
                if (!entry.Enabled) continue;

                string renderedText = ScribanParser.Render(entry.Content, contextObj, true);
                if (string.IsNullOrWhiteSpace(renderedText)) continue;

                string roleStr = entry.Role.ToString();

                if (roleStr == "System")
                {
                    if (systemBuilder.Length > 0) systemBuilder.AppendLine("\n");
                    systemBuilder.Append(renderedText);
                }
                else
                {
                    if (userBuilder.Length > 0) userBuilder.AppendLine("\n");
                    if (roleStr == "Assistant") userBuilder.Append("Assistant: ");
                    userBuilder.Append(renderedText);
                }
            }

            if (systemBuilder.Length == 0 && userBuilder.Length == 0) return null;

            string technicalProtocol = isBatch
                ? PersonasSettings.HiddenTechnicalPrompt_Batch
                : PersonasSettings.HiddenTechnicalPrompt_Single;

            userBuilder.AppendLine("\n" + technicalProtocol);

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
                    RimAiLog.Info(RimAiLogCategory.Personas, $"[Director] Gen Data for {pawnNameForLog}...");

                string presetName = PersonasMod.Settings.rimTalkPreset_Single;
                if (!string.IsNullOrEmpty(presetName) && presetName != "None (Use Internal)")
                {
                    var result = await GenerateFromPreset(pawn, presetName, false);
                    if (result != null) return result;
                }

                string userPrompt = PersonasMod.Settings.GetActivePrompt(false);
                if (string.IsNullOrEmpty(userPrompt)) userPrompt = PersonasSettings.DefaultPrompt_Standard;

                string instruction = PersonaVariantGenerationPolicy.ComposeInstruction(
                    userPrompt,
                    DirectorPromptComposer.CurrentLanguage,
                    PersonaVariantGenerationPolicy.DefaultCount,
                    PersonasSettings.HiddenTechnicalPrompt_Single);
                string data = $"[Character Data]\n{characterData}";

                var request = new TalkRequest(data, pawn)
                {
                    Context = instruction
                };

                return await AIService.Query<PersonalityData>(request);
            }
            catch (Exception e)
            {
                RimAiLog.Error(RimAiLogCategory.Personas, $"[Director] Generation failed: {e.Message}");
                return new PersonalityData("Error generating persona.", 0.5f);
            }
        }

        public static async Task<PersonalityData> GenerateBatchPersonaTask(string combinedData, Pawn representative)
        {
            try
            {
                if (PersonasMod.Settings.EnableDebugLog)
                {
                    RimAiLog.Info(RimAiLogCategory.Personas, $"[Director] Batch Gen Data:\n{combinedData}");
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
                RimAiLog.Error(RimAiLogCategory.Personas, $"[Director] Batch Gen failed: {e.Message}");
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
                    hediff.Severity = 1.0f;


                    if (data.Chattiness < 0.05f)
                    {
                        hediff.TalkInitiationWeight = 0.5f;
                    }
                    else
                    {
                        hediff.TalkInitiationWeight = Mathf.Clamp(data.Chattiness, 0.1f, 1.0f);
                    }

                    pawn.health.Notify_HediffChanged(hediff);
                }
            }
            catch (Exception e)
            {
                RimAiLog.Error(RimAiLogCategory.Personas, $"[Director] Failed to apply personality: {e.Message}");
            }
        }

        public static int ParseAndApplyBatchResult(List<Pawn> pawns, string combinedPersona)
        {
            int appliedCount = 0;
            if (string.IsNullOrEmpty(combinedPersona)) return 0;

            var personaParts = combinedPersona.Split(new[] { "---" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in personaParts)
            {
                int bracketIndex = part.IndexOf(']');

                if (bracketIndex == -1)
                {
                    if (PersonasMod.Settings.EnableDebugLog) RimAiLog.Warning(RimAiLogCategory.Personas, $"[Director] Invalid format (no bracket found): {part.Trim()}");
                    continue;
                }

                string keyPart = part.Substring(0, bracketIndex + 1).Trim();

                string text = part.Substring(bracketIndex + 1).TrimStart(':', ' ', '\n', '\r').Trim();

                Pawn target = null;

                if (keyPart.StartsWith("[ID:") && keyPart.EndsWith("]"))
                {
                    if (keyPart.Length > 5)
                    {
                        string id = keyPart.Substring(4, keyPart.Length - 5);
                        target = pawns.FirstOrDefault(p => p.ThingID == id);
                    }
                }

                if (target == null)
                {
                    string cleanName = keyPart.TrimStart('[').TrimEnd(']').Trim();
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
                        RimAiLog.Warning(RimAiLogCategory.Personas, $"[Director] Could not match result key '{keyPart}' to any pawn.");
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
            // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY - persona hediff unreadable, using the not-set label
            catch (System.Exception ex)
            {
                ModuleLog.Message("[RimAI.Personas] persona hediff unreadable, using the not-set label: " + ex.Message);
            }
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
                RimAiLog.Error(RimAiLogCategory.Personas, $"[Director] Failed to open RimTalk window: {ex}");
            }
        }
}
