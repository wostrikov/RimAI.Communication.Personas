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

public static class DirectorPawnStatus
{
        private static Type _hospitalityCompType;
        private static bool _hospitalityChecked = false;

        private static bool IsHospitalityGuest(Pawn p)
        {
            if (!ModsConfig.IsActive("Orion.Hospitality")) return false;

            if (!_hospitalityChecked)
            {
                _hospitalityCompType = AccessTools.TypeByName("Hospitality.CompGuest");
                _hospitalityChecked = true;
            }

            if (_hospitalityCompType == null) return false;

            var comp = p.AllComps.FirstOrDefault(c => _hospitalityCompType.IsAssignableFrom(c.GetType()));

            return comp != null;
        }

        public static string GetPawnSocialStatus(Pawn p)
        {
            string socialStatus = "Unknown";

            if (p.IsPrisonerOfColony)
            {
                bool isQuestInvolved = Find.QuestManager.QuestsListForReading
                    .Any(q => q.State == QuestState.Ongoing && q.QuestReserves(p));

                socialStatus = isQuestInvolved ? "Quest Prisoner" : "Prisoner";
            }
            else if (p.IsSlaveOfColony)
            {
                socialStatus = "Slave";
            }

            if (socialStatus == "Unknown" && ModsConfig.AnomalyActive)
            {
                if (p.IsMutant) socialStatus = "Mutant";
                else if (p.IsCreepJoiner) socialStatus = "Creep Joiner (Mysterious Stranger)";
                else if (p.def.race.IsAnomalyEntity)
                {
                    bool isContained = p.ParentHolder is Building;
                    socialStatus = isContained ? "Anomaly Entity (Contained)" : "Anomaly Entity (Hostile)";
                }
            }

            if (socialStatus == "Unknown" && p.Faction == Faction.OfPlayer)
            {
                if (p.HasExtraHomeFaction() || p.IsQuestLodger())
                {
                    socialStatus = GetTemporaryColonistDescription(p);
                }
                else if (p.IsFreeColonist) socialStatus = "Colonist";
                else if (p.RaceProps.IsMechanoid) socialStatus = "Mechanoid (Colony Controlled)";
                else if (p.RaceProps.Animal) socialStatus = "Tame Animal";
                else socialStatus = "Colony Member";
            }

            if (socialStatus == "Unknown" && p.Faction != null)
            {
                if (p.Faction.HostileTo(Faction.OfPlayer))
                {
                    if (p.DevelopmentalStage == DevelopmentalStage.Baby || p.DevelopmentalStage == DevelopmentalStage.Child)
                    {
                        socialStatus = "Hostile Faction Child (Non-combatant, in colony, usually the child of prisoner in colony)";
                    }
                    else socialStatus = "Enemy / Raider";
                }
                else
                {
                    var lord = p.GetLord();

                    if (p.IsSlave)
                    {
                        socialStatus = "Visitor (as a Slave)";
                    }
                    else if (lord != null && lord.LordJob != null)
                    {
                        if (lord.LordJob is LordJob_TradeWithColony)
                        {
                            socialStatus = "Trader Caravan Member";
                        }
                        else if (lord.LordJob is LordJob_VisitColony)
                        {
                            if (IsHospitalityGuest(p))
                                socialStatus = "Hospitality Guest";
                            else
                                socialStatus = "Visitor";
                        }
                        else if (lord.LordJob is LordJob_TravelAndExit)
                        {
                            socialStatus = "Traveler (Passing through)";
                        }
                        else if (lord.LordJob is LordJob_AssistColony || lord.LordJob is LordJob_DefendPoint)
                        {
                            socialStatus = "Ally Reinforcement";
                        }
                        else
                        {
                            socialStatus = "Visitor";
                        }
                    }
                    else if (IsHospitalityGuest(p))
                    {
                        socialStatus = "Hospitality Guest";
                    }
                    else
                    {
                        socialStatus = "Visitor";
                    }
                }
            }

            if (socialStatus == "Unknown")
            {
                if (p.RaceProps.Animal) socialStatus = "Wild Animal";
                else if (p.RaceProps.IsMechanoid) socialStatus = "Rogue Mechanoid";
                else socialStatus = "Wild / Independent";
            }

            return socialStatus;
        }

        public static string GetTemporaryColonistDescription(Pawn p)
        {
            if (p.IsQuestLodger())
            {
                var foundQuest = Find.QuestManager.QuestsListForReading
                    .FirstOrDefault(q => q.State == QuestState.Ongoing && q.QuestReserves(p));

                if (foundQuest != null)
                {
                    string questName = foundQuest.name ?? "Unnamed Quest";

                    string questDesc = "";
                    if (foundQuest.description != null)
                    {
                        questDesc = foundQuest.description.Resolve()
                                         .StripTags()
                                         .Replace("\n", " ")
                                         .Replace("\r", "")
                                         .Trim();
                    }

                    if (!string.IsNullOrEmpty(questDesc))
                        return $"Temporary member, here for quest (Quest: {questName})\n- Quest Context: {questDesc}";
                    else
                        return $"Temporary member, here for quest (Quest: {questName})";
                }
                return "Temporary member (reason unknown, likely quest-related)";
            }

            if (p.HasExtraHomeFaction() && p.GetExtraHomeFaction() != null)
            {
                return $"Ally Helper (Lent by {p.GetExtraHomeFaction().Name})";
            }

            return "Temporary Member";
        }

        public static string GetPawnShortStatus(Pawn p)
        {
            if (p.Dead) return "(Deceased)";
            if (p.IsPrisonerOfColony) return "(Prisoner in Colony)";
            if (p.IsSlaveOfColony) return "(Slave in Colony)";
            if (p.Faction == Faction.OfPlayer) return "(In Colony)";

            if (p.Faction != null)
            {
                if (p.Faction.HostileTo(Faction.OfPlayer))
                {
                    return "(Hostile)";
                }
                if (p.Faction.PlayerRelationKind == FactionRelationKind.Ally)
                {
                    return "(Ally)";
                }
                if (p.Faction.PlayerRelationKind == FactionRelationKind.Neutral)
                {
                    return "(Neutral)";
                }
            }

            if (p.IsWorldPawn()) return "(Elsewhere)";

            return "";
        }
}
