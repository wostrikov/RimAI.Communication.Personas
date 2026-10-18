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
using Ustas.RimAI.Communication.Personas.Diagnostics;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Communication.Personas;

public static class DirectorPromptComposer
{
        public static string CurrentLanguage
        {
            get
            {
                try
                {
                    if (LanguageDatabase.activeLanguage != null)
                        return LanguageDatabase.activeLanguage.info.friendlyNameNative;
                }
                // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY - active language name unavailable, using English
                catch (System.Exception ex)
                {
                    ModuleLog.Message("[RimAI.Personas] active language name unavailable, using English: " + ex.Message);
                }
                return "English";
            }
        }

        public static float NormalizeChattiness(float chattiness)
        {
            // If upstream values were produced on 0..2 scale, divide values greater than 1 by 2
            if (chattiness > 1.0f) return chattiness / 2f;
            return chattiness;
        }

        public static string GetFinalPrompt(bool isBatch, string dataContent)
        {
            string userPrompt = PersonasMod.Settings.GetActivePrompt();
            if (string.IsNullOrEmpty(userPrompt)) userPrompt = PersonasSettings.DefaultPrompt_Standard;

            string technicalPrompt = isBatch
                ? PersonasSettings.HiddenTechnicalPrompt_Batch
                : PersonasSettings.HiddenTechnicalPrompt_Single;

            return userPrompt.Replace("{LANG}", CurrentLanguage) +
           "\n" + technicalPrompt +
           "\n\n[Character Data]\n" + dataContent;
        }

        public static string RenderScribanText(string rawText, Pawn p)
        {
            if (string.IsNullOrEmpty(rawText) || !rawText.Contains("{{")) return rawText;

            try
            {
                // The tracked list is the dialogue's participants as they were when
                // the context was captured, and one of them can be gone by now: a
                // null among them throws in every variable that walks the list.
                List<Pawn> participants = DirectorContextTracker.GetPawns()?.FindAll(candidate => candidate != null);
                PromptContext contextObj;
                if (participants != null && participants.Contains(p))
                {
                    contextObj = new PromptContext(participants)
                    {
                        CurrentPawn = p
                    };
                    // The list constructor takes the map of whoever is first, which
                    // is not this pawn's, and is nothing when the first one is in a
                    // caravan while this one stands on a map.
                    contextObj.Map = p?.Map ?? participants.Find(candidate => candidate.Map != null)?.Map;
                }
                else
                {
                    contextObj = new PromptContext(p);
                }

                if (ScribanParser.TryRender(rawText, contextObj, out string rendered, out string renderError))
                    return rendered;

                // A persona that fails to render is left out of this prompt rather than sent
                // as its literal template text. Once per template: this runs for every talk.
                RimAiLog.WarningOnce(RimAiLogCategory.Personas,
                    $"[Director] Persona template for {p?.LabelShort ?? "a pawn"} failed to render and is left out of the prompt. {renderError}",
                    ("persona-render:" + rawText).GetHashCode());
                return string.Empty;
            }
            catch (Exception)
            {
                // Same rule as a failed render: no literal template text in the prompt.
                return string.Empty;
            }
        }
}
