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
            // 1. 如果 Mod 没开，直接返回 false
            if (!ModsConfig.IsActive("Orion.Hospitality")) return false;

            // 2. 初始化反射类型 (只做一次)
            if (!_hospitalityChecked)
            {
                _hospitalityCompType = AccessTools.TypeByName("Hospitality.CompGuest");
                _hospitalityChecked = true;
            }

            // 3. 如果找不到类型，说明没装或者版本不对
            if (_hospitalityCompType == null) return false;

            // 4. 检查 Pawn 身上是否有这个组件
            var comp = p.AllComps.FirstOrDefault(c => _hospitalityCompType.IsAssignableFrom(c.GetType()));

            return comp != null;
        }

        public static string GetPawnSocialStatus(Pawn p)
        {
            string socialStatus = "Unknown";

            // 1. 第一优先级：与殖民地的直接关系 (囚犯/奴隶)
            if (p.IsPrisonerOfColony)
            {
                // 检查 Pawn 是否与任何正在进行的任务相关联
                bool isQuestInvolved = Find.QuestManager.QuestsListForReading
                    .Any(q => q.State == QuestState.Ongoing && q.QuestReserves(p));

                socialStatus = isQuestInvolved ? "Quest Prisoner" : "Prisoner";
            }
            else if (p.IsSlaveOfColony)
            {
                socialStatus = "Slave";
            }

            // 2. 第二优先级：异象身份
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

            // 3. 第三优先级：殖民地成员 (细分)
            if (socialStatus == "Unknown" && p.Faction == Faction.OfPlayer)
            {
                // 优先判断是否为临时成员
                if (p.HasExtraHomeFaction() || p.IsQuestLodger())
                {
                    socialStatus = GetTemporaryColonistDescription(p); // 调用下面更新过的辅助函数
                }
                else if (p.IsFreeColonist) socialStatus = "Colonist";
                else if (p.RaceProps.IsMechanoid) socialStatus = "Mechanoid (Colony Controlled)";
                else if (p.RaceProps.Animal) socialStatus = "Tame Animal";
                else socialStatus = "Colony Member";
            }

            // 4. 第四优先级：外部人员
            if (socialStatus == "Unknown" && p.Faction != null)
            {
                // A. 敌对派系人员
                if (p.Faction.HostileTo(Faction.OfPlayer))
                {
                    if (p.DevelopmentalStage == DevelopmentalStage.Baby || p.DevelopmentalStage == DevelopmentalStage.Child)
                    {
                        socialStatus = "Hostile Faction Child (Non-combatant, in colony, usually the child of prisoner in colony)";
                    }
                    else socialStatus = "Enemy / Raider";
                }
                // B. 中立/友好派系人员 (商人会被归入此类)
                else
                {
                    // ★★★ 核心修复：通过 LordJob 判断访客意图 ★★★
                    // 获取控制这个 Pawn 的领主任务 (LordJob)
                    var lord = p.GetLord();

                    if (p.IsSlave)
                    {
                        socialStatus = "Visitor (as a Slave)";
                    }
                    else if (lord != null && lord.LordJob != null)
                    {
                        // A. 商队 (Trader Caravan)
                        // LordJob_TradeWithColony 代表这群人是来做生意的
                        // 即使是保镖，只要在这个队里，也会被标记为 Trader Group
                        if (lord.LordJob is LordJob_TradeWithColony)
                        {
                            socialStatus = "Trader Caravan Member";
                        }
                        // B. 访客 (Guest / Visitor)
                        // LordJob_VisitColony 代表这群人是来串门的
                        else if (lord.LordJob is LordJob_VisitColony)
                        {
                            if (IsHospitalityGuest(p))
                                socialStatus = "Hospitality Guest";
                            else
                                socialStatus = "Visitor";
                        }
                        // C. 路人 (Traveler)
                        // LordJob_TravelAndExit 代表他们只是路过地图，不打算停留
                        else if (lord.LordJob is LordJob_TravelAndExit)
                        {
                            socialStatus = "Traveler (Passing through)";
                        }
                        // D. 援军 (Ally)
                        // LordJob_AssistColony 代表他们是来帮忙打架的
                        else if (lord.LordJob is LordJob_AssistColony || lord.LordJob is LordJob_DefendPoint)
                        {
                            socialStatus = "Ally Reinforcement";
                        }
                        else
                        {
                            // 其他情况 (比如参加婚礼、参加仪式等)
                            socialStatus = "Visitor";
                        }
                    }
                    else if (IsHospitalityGuest(p))
                    {
                        // 只要有这个组件，无论他在干嘛，他就是 Hospitality 的客人
                        socialStatus = "Hospitality Guest";
                    }
                    else
                    {
                        // 没有 Lord 的散人
                        socialStatus = "Visitor";
                    }
                }
            }

            // 5. 第五优先级：无派系/野生
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
            // 1. 使用 QuestReserves 查找任务
            if (p.IsQuestLodger())
            {
                // 查找任何正在进行的、且“保留”了该 Pawn 的任务
                var foundQuest = Find.QuestManager.QuestsListForReading
                    .FirstOrDefault(q => q.State == QuestState.Ongoing && q.QuestReserves(p));

                if (foundQuest != null)
                {
                    // ★★★ 核心修复：增加空检查 ★★★
                    string questName = foundQuest.name ?? "Unnamed Quest";

                    // 确保 description 不是 null
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

            // 2. 检查盟友协助
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
                // 核心修正：添加对盟友和中立的判断
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

            // 如果以上都不是，则不加标签
            return "";
        }
}
