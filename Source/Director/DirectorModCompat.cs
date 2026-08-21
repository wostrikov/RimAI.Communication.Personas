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

public static class DirectorModCompat
{
        private static MethodInfo vseMethodCache;
        private static bool vseReflectionFailed = false;

        public static Def GetVSEPassionDef(SkillRecord skill)
        {
            if (!ModsConfig.IsActive("vanillaexpanded.skills") || vseReflectionFailed)
                return null;

            try
            {
                if (vseMethodCache == null)
                {
                    Type managerType = AccessTools.TypeByName("VSE.Passions.PassionManager");

                    if (managerType == null)
                    {
                        var assembly = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(a => a.GetName().Name == "VSE");

                        if (assembly != null)
                        {
                            managerType = assembly.GetType("VSE.Passions.PassionManager");
                        }
                    }

                    if (managerType != null)
                    {
                        vseMethodCache = AccessTools.Method(managerType, "PassionToDef", new[] { typeof(Passion) });
                    }

                    if (vseMethodCache == null)
                    {
                        vseReflectionFailed = true;
                        return null;
                    }
                }

                return (Def)vseMethodCache.Invoke(null, new object[] { skill.passion });
            }
            catch
            {
                vseReflectionFailed = true;
            }

            return null;
        }

        public static string GetPassionInfoHardcoded(Passion passion)
        {
            int val = (int)passion;
            switch (val)
            {
                case 0: return "";
                case 1: return "[Minor]: Interested in this skill.";
                case 2: return "[Major]: Burning passion for this skill.";
                case 3: return "[Apathy]: Absolutely no interest in this skill.";
                case 4: return "[Natural]: Naturally good at this skill, but won't practice";
                case 5: return "[Critical]: Extremely important skill due to past events.";
                default: return $"[Level: {val}]";
            }
        }

        public static string GetMauxRimPsycheData(Pawn p)
        {
            StringBuilder psySb = new StringBuilder();
            object comp = p.AllComps.FirstOrDefault(c => c.GetType().FullName.Contains("RimPsyche") || c.GetType().Name.Contains("Psyche"));
            if (comp == null) return "";
            try
            {
                Type utilityType = AccessTools.TypeByName("Maux36.RimPsyche.Rimpsyche_Utility");
                if (utilityType != null)
                {
                    MethodInfo method = AccessTools.Method(utilityType, "GetPersonalityDescriptionWord", new Type[] { typeof(Pawn), typeof(int) });
                    if (method != null)
                    {
                        int limit = PersonasMod.Settings.Context.Inc_RimPsyche_All ? 0 : 5;
                        object result = method.Invoke(null, new object[] { p, limit });
                        if (result != null)
                        {
                            psySb.AppendLine("- Personality Profile:");
                            psySb.AppendLine((string)result);
                        }
                    }
                }
            }
            catch { }
            try
            {
                object interestsTracker = null;
                Type compType = comp.GetType();
                var interestsProp = AccessTools.Property(compType, "Interests");
                if (interestsProp != null) interestsTracker = interestsProp.GetValue(comp, null);
                else
                {
                    var interestsField = AccessTools.Field(compType, "Interests");
                    if (interestsField != null) interestsTracker = interestsField.GetValue(comp);
                }
                if (interestsTracker == null)
                {
                    var psycheField = compType.GetField("psyche") ?? compType.GetField("personality");
                    if (psycheField != null) interestsTracker = psycheField.GetValue(comp);
                }
                if (interestsTracker != null)
                {
                    var interestField = AccessTools.Field(interestsTracker.GetType(), "interestScore");
                    if (interestField != null)
                    {
                        var interestDict = interestField.GetValue(interestsTracker) as IDictionary;
                        if (interestDict != null && interestDict.Count > 0)
                        {
                            StringBuilder interestSb = new StringBuilder();
                            foreach (DictionaryEntry entry in interestDict)
                            {
                                string key = entry.Key.ToString();
                                float score = Convert.ToSingle(entry.Value);
                                bool isSignificant = score > 20f || score < -20f;
                                bool showAll = PersonasMod.Settings.Context.Inc_RimPsyche_All;
                                if (showAll || isSignificant)
                                {
                                    string detail = GetInterestDetails(key);
                                    interestSb.Append($"{detail}: {score:F1}, ");
                                }
                            }
                            if (interestSb.Length > 0)
                            {
                                psySb.AppendLine("\n- Interests (Topics, Range: -35.0 to 35.0):");
                                psySb.AppendLine(interestSb.ToString().TrimEnd(',', ' '));
                            }
                        }
                    }
                }
            }
            catch { }
            return psySb.ToString();
        }

        private static Dictionary<string, string> _interestCache = null;

        private static string GetInterestDetails(string key)
        {
            if (_interestCache == null) BuildInterestCache();
            if (_interestCache.TryGetValue(key, out string val)) return val;
            return key;
        }

        private static void BuildInterestCache()
        {
            _interestCache = new Dictionary<string, string>();
            try
            {
                Type domainType = AccessTools.TypeByName("Maux36.RimPsyche.InterestDomainDef");
                if (domainType != null)
                {
                    Type dbType = typeof(DefDatabase<>).MakeGenericType(domainType);
                    PropertyInfo allDefsProp = dbType.GetProperty("AllDefs");
                    if (allDefsProp != null)
                    {
                        IEnumerable allDomains = allDefsProp.GetValue(null) as IEnumerable;
                        if (allDomains != null)
                        {
                            foreach (object domain in allDomains)
                            {
                                FieldInfo interestsField = domain.GetType().GetField("interests");
                                if (interestsField == null) continue;
                                IEnumerable interestsList = interestsField.GetValue(domain) as IEnumerable;
                                if (interestsList == null) continue;
                                foreach (object interest in interestsList)
                                {
                                    Type intType = interest.GetType();
                                    string name = (string)intType.GetField("name")?.GetValue(interest);
                                    string label = (string)intType.GetField("label")?.GetValue(interest);
                                    string desc = (string)intType.GetField("description")?.GetValue(interest);
                                    if (!string.IsNullOrEmpty(name))
                                    {
                                        string display = !string.IsNullOrEmpty(label) ? label : name;
                                        if (!string.IsNullOrEmpty(desc)) display += $" ({desc})";
                                        _interestCache[name] = display;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }
}
