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
                catch { }
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
                List<Pawn> allPawns = DirectorContextTracker.GetPawns();
                PromptContext contextObj;
                if (allPawns != null && allPawns.Contains(p))
                {
                    contextObj = new PromptContext(allPawns)
                    {
                        CurrentPawn = p
                    };
                }
                else
                {
                    contextObj = new PromptContext(p);
                }

                return ScribanParser.Render(rawText, contextObj, false) ?? rawText;
            }
            catch (Exception)
            {
                return rawText;
            }
        }
}
