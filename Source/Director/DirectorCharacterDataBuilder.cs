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

public static class DirectorCharacterDataBuilder
{
        public static string BuildCustomCharacterData(Pawn p, bool isSnapshot = false, bool simpleEquipment = false)
        {
            StringBuilder sb = new StringBuilder();
            var ctx = PersonasMod.Settings.Context;
            string data = DirectorDataEngine.BuildCompleteData(p, simpleEquipment);

            try
            {
                if (ctx.Inc_Basic)
                {
                    sb.AppendLine("--- Basic Info ---");
                    sb.AppendLine($"Name: {p.LabelShortCap}"); // 通用名字
                    sb.AppendLine($"Gender: {p.gender}");
                    sb.AppendLine($"Age: {p.ageTracker.AgeBiologicalYears}");
                    sb.AppendLine($"Current Status: {DirectorPawnStatus.GetPawnSocialStatus(p)}");

                    if (p.Faction != null)
                    {
                        // 1. 玩家派系 (Player Faction)
                        // 包括：正常殖民者、被逮捕的自己人(内部囚犯)、奴隶
                        if (p.Faction.IsPlayer)
                        {
                            sb.Append($"Faction: {p.Faction.Name} (Player Colony)");

                            // 特例：奴隶虽然属于玩家派系，但如果有原派系信息，AI需要知道
                            if (p.IsSlave && p.guest?.SlaveFaction != null)
                            {
                                sb.AppendLine();
                                sb.AppendLine($"Origin Faction (Enslaved from): {p.guest.SlaveFaction.Name}");

                                string originDesc = p.guest.SlaveFaction.def.description;
                                if (!string.IsNullOrEmpty(originDesc))
                                {
                                    sb.AppendLine($"Origin Description: {originDesc.StripTags().Replace('\n', ' ')}");
                                }
                            }
                            else
                            {
                                // 对于殖民者和内部囚犯，不需要发送派系描述
                                // AI 会根据 Current Status (如 "Prisoner (of your Colony)") 理解处境
                                sb.AppendLine();
                            }
                        }
                        // 2. 外部派系 (External Faction)
                        // 包括：未招募的外部囚犯、访客、袭击者
                        else
                        {
                            // 显示派系名和关系（如 Hostile, Ally）
                            string relStr = p.Faction.HostileTo(Faction.OfPlayer) ? "Hostile" : "Neutral/Ally";
                            sb.AppendLine($"Faction: {p.Faction.Name} ({relStr})");

                            // ★★★ 关键：发送外部派系的详细描述 ★★★
                            // 这让 AI 能理解囚犯/访客的文化背景
                            string desc = p.Faction.def.description;
                            if (!string.IsNullOrEmpty(desc))
                            {
                                sb.AppendLine($"Faction Description: {desc.StripTags().Replace('\n', ' ')}");
                            }
                        }
                    }
                    else
                    {
                        sb.AppendLine("Faction: None (Independent)");
                    }
                }
            }
            catch { }

            try
            {
                if (ctx.Inc_Race)
                {
                    sb.AppendLine("\n--- Race & Xenotype ---");
                    sb.Append($"Race: {p.def.label}");
                    if (ctx.Inc_Race_Desc) sb.AppendLine($": {p.def.description}"); else sb.AppendLine();

                    if (p.genes != null && p.genes.Xenotype != null && p.genes.Xenotype.defName != "Archite")
                    {
                        sb.Append($"Xenotype: {p.genes.XenotypeLabel}");
                        if (ctx.Inc_Race_Desc) sb.AppendLine($": {p.genes.Xenotype.description}"); else sb.AppendLine();
                    }
                }
            }
            catch { }

            try
            {
                if (ctx.Inc_Genes && p.genes != null)
                {
                    sb.AppendLine("\n--- Genes ---");
                    if (p.genes.Endogenes.Any())
                    {
                        sb.Append("[Endogenes (Natural)]: ");
                        foreach (var gene in p.genes.Endogenes)
                        {
                            if (gene.def.displayCategory != GeneCategoryDefOf.Miscellaneous && !gene.Overridden)
                            {
                                sb.Append(gene.LabelCap);
                                if (ctx.Inc_Genes_Desc) sb.Append($"({gene.def.description})");
                                sb.Append(", ");
                            }
                        }
                        sb.AppendLine();
                    }
                    if (p.genes.Xenogenes.Any())
                    {
                        sb.Append("[Xenogenes (Artificial)]: ");
                        foreach (var gene in p.genes.Xenogenes)
                        {
                            if (gene.def.displayCategory != GeneCategoryDefOf.Miscellaneous && !gene.Overridden)
                            {
                                sb.Append(gene.LabelCap);
                                if (ctx.Inc_Genes_Desc) sb.Append($"({gene.def.description})");
                                sb.Append(", ");
                            }
                        }
                        sb.AppendLine();
                    }
                }
            }
            catch { }

            try
            {
                if (ctx.Inc_Backstory && p.story != null)
                {
                    sb.AppendLine("\n--- Backstory ---");
                    if (p.story.Childhood != null)
                    {
                        // 总是先添加标题 (标签)
                        sb.Append($"Childhood: {p.story.Childhood.TitleCapFor(p.gender)}");

                        // 如果开关开启，再附加描述
                        if (ctx.Inc_Backstory_Desc)
                        {
                            // 获取原始描述，并清理一下格式
                            string desc = p.story.Childhood.FullDescriptionFor(p).Resolve();

                            string cleanDesc = desc.Replace(p.story.Childhood.title, "").Trim(); // 尝试移除标题
                            if (!string.IsNullOrWhiteSpace(cleanDesc))
                            {
                                sb.AppendLine($":\n{cleanDesc.StripTags().Trim()}");
                            }
                            else
                            {
                                sb.AppendLine();
                            }
                        }
                        else
                        {
                            sb.AppendLine();
                        }
                    }
                    if (p.story.Adulthood != null)
                    {
                        // Adulthood 做同样的处理
                        sb.Append($"Adulthood: {p.story.Adulthood.TitleCapFor(p.gender)}");
                        if (ctx.Inc_Backstory_Desc)
                        {
                            string desc = p.story.Adulthood.FullDescriptionFor(p).Resolve();
                            string cleanDesc = desc.Replace(p.story.Adulthood.title, "").Trim();
                            if (!string.IsNullOrWhiteSpace(cleanDesc))
                            {
                                sb.AppendLine($":\n{cleanDesc.StripTags().Trim()}");
                            }
                            else
                            {
                                sb.AppendLine();
                            }
                        }
                        else
                        {
                            sb.AppendLine();
                        }
                    }
                }
            }
            catch { }

            try
            {
                // 使用 RelatedPawns 以确保能获取到所有类型的关系
                if (ctx.Inc_Relations && p.relations != null && p.relations.RelatedPawns.Any())
                {
                    StringBuilder relationSb = new StringBuilder();

                    foreach (Pawn otherPawn in p.relations.RelatedPawns)
                    {
                        // 获取最重要关系
                        PawnRelationDef relation = p.GetMostImportantRelation(otherPawn);
                        if (relation == null) continue;

                        bool isRelevant =
                            // 1. 核心家庭
                            relation == PawnRelationDefOf.Parent ||
                            relation == PawnRelationDefOf.Child ||
                            relation == PawnRelationDefOf.Sibling ||
                            relation == PawnRelationDefOf.Spouse ||
                            relation == PawnRelationDefOf.Lover ||
                            relation == PawnRelationDefOf.Fiance ||
                            // 2. 牵绊 (人与动物)
                            relation == PawnRelationDefOf.Bond ||
                            // 3. 机械师主人 (Overseer)
                            relation.defName == "Overseer";

                        if (!isRelevant) continue;

                        // --- 标签处理逻辑 ---
                        string relationLabel = relation.GetGenderSpecificLabelCap(otherPawn);

                        if (relation == PawnRelationDefOf.Child && relationLabel == "Child")
                            relationLabel = otherPawn.gender == Gender.Female ? "Daughter" : "Son";
                        else if (relation == PawnRelationDefOf.Sibling && relationLabel == "Sibling")
                            relationLabel = otherPawn.gender == Gender.Female ? "Sister" : "Brother";

                        // 兜底
                        if (string.IsNullOrEmpty(relationLabel)) relationLabel = relation.label;
                        relationLabel = relationLabel.CapitalizeFirst();

                        // --- 状态与名字 ---
                        string status = DirectorPawnStatus.GetPawnShortStatus(otherPawn);

                        relationSb.AppendLine($"- {relationLabel}: {otherPawn.Name.ToStringShort} {status}".Trim());
                    }

                    if (relationSb.Length > 0)
                    {
                        sb.AppendLine("\n--- Key Relationships ---");
                        sb.Append(relationSb);
                    }
                }
            }
            catch { }

            try
            {
                if (ctx.Inc_Traits && p.story?.traits?.allTraits != null)
                {
                    sb.AppendLine("\n--- Traits ---");
                    foreach (var trait in p.story.traits.allTraits)
                    {
                        sb.Append(trait.Label);
                        if (ctx.Inc_Traits_Desc) sb.AppendLine($": {trait.CurrentData.description}"); else sb.AppendLine();
                    }
                }
            }
            catch { }

            try
            {
                if (ctx.Inc_Ideology && p.Ideo != null)
                {
                    sb.AppendLine("\n--- Ideology ---");
                    sb.AppendLine($"Religion: {p.Ideo.name}");
                    foreach (var meme in p.Ideo.memes)
                    {
                        sb.Append($"Meme [{meme.LabelCap}]");
                        if (ctx.Inc_Ideology_Desc) sb.AppendLine($": {meme.description}"); else sb.AppendLine();
                    }
                }
            }
            catch { }

            try
            {
                if (ctx.Inc_Skills && p.skills != null)
                {
                    sb.AppendLine("\n--- Skills ---");
                    foreach (var skill in p.skills.skills)
                    {
                        sb.Append($"{skill.def.label}: ");
                        bool incapable = p.WorkTagIsDisabled(WorkTags.AllWork);
                        if (!incapable)
                        {
                            foreach (var workDef in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                            {
                                if (workDef.relevantSkills.Contains(skill.def) && p.WorkTypeIsDisabled(workDef))
                                {
                                    incapable = true; break;
                                }
                            }
                        }
                        if (incapable) sb.Append("[INCAPABLE]");
                        else
                        {
                            sb.Append(skill.Level);
                            if (ctx.Inc_Skills_Desc && skill.passion != Passion.None)
                            {
                                // 1. 优先尝试从 VSE 获取 PassionDef
                                Def vsePassionDef = DirectorModCompat.GetVSEPassionDef(skill);

                                if (vsePassionDef != null)
                                {
                                    // 如果成功，使用 VSE 的数据
                                    sb.Append($" [{vsePassionDef.label.CapitalizeFirst()}]: {vsePassionDef.description}");
                                }
                                else
                                {
                                    // 2. 如果 VSE 不存在或失败，回退到硬编码方法
                                    sb.Append($" {DirectorModCompat.GetPassionInfoHardcoded(skill.passion)}");
                                }
                            }
                        }
                        sb.AppendLine();
                    }
                }
            }
            catch { }

            try
            {
                // 9. Health
                if (ctx.Inc_Health && p.health?.hediffSet != null)
                {
                    // 筛选出所有对玩家可见的健康状况
                    var visibleHediffs = p.health.hediffSet.hediffs.Where(h => h.Visible).ToList();

                    if (visibleHediffs.Any())
                    {
                        sb.AppendLine("\n--- Health ---");
                        foreach (var hediff in visibleHediffs)
                        {
                            // 安全地获取身体部位名称
                            string partName = hediff.Part != null ? hediff.Part.LabelCap : "Whole Body";

                            // 标签总是显示
                            sb.Append($"- {partName}: {hediff.LabelCap}");

                            // 根据开关添加描述
                            if (ctx.Inc_Health_Desc && !string.IsNullOrEmpty(hediff.def.description))
                            {
                                // 清理描述中的格式代码，并替换换行符，确保数据干净
                                string cleanDesc = hediff.def.description.StripTags().Replace('\n', ' ');
                                sb.AppendLine($" ({cleanDesc})");
                            }
                            else
                            {
                                sb.AppendLine();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (PersonasMod.Settings.EnableDebugLog) RimAiLog.Warning(RimAiLogCategory.Personas, $"[Director] Error fetching health data for {p.LabelShort}: {ex.Message}");
            }

            try
            {
                if (ctx.Inc_Equipment)
                {
                    bool hasItems = false;
                    StringBuilder equipSb = new StringBuilder();
                    if (p.equipment != null)
                    {
                        foreach (var eq in p.equipment.AllEquipmentListForReading)
                        {
                            equipSb.AppendLine($"- [Weapon]: {eq.LabelCap}"); hasItems = true;
                        }
                    }
                    if (p.apparel != null)
                    {
                        foreach (var app in p.apparel.WornApparel)
                        {
                            equipSb.AppendLine($"- [Apparel]: {app.LabelCap}"); hasItems = true;
                        }
                    }
                    if (hasItems) { sb.AppendLine("\n--- Equipment ---"); sb.Append(equipSb); }
                }
            }
            catch { }

            try
            {
                if (ctx.Inc_Inventory && p.inventory != null && p.inventory.innerContainer.Any)
                {
                    sb.AppendLine("\n--- Inventory ---");
                    foreach (var item in p.inventory.innerContainer)
                    {
                        sb.AppendLine($"- {item.LabelCap}");
                    }
                }
            }
            catch { }

            try
            {
                if (ctx.Inc_RimPsyche)
                {
                    string psyData = DirectorModCompat.GetMauxRimPsycheData(p);
                    if (!string.IsNullOrEmpty(psyData))
                    {
                        sb.AppendLine("\n--- RimPsyche ---");
                        sb.Append(psyData);
                    }
                }
            }
            catch { }

            if (!isSnapshot)
            {
                try
                {
                    if (ctx.Inc_DirectorNotes && !string.IsNullOrEmpty(PersonasMod.Settings.directorNotes))
                    {
                        sb.AppendLine("\n--- Director's Notes (Custom Context) ---");
                        sb.AppendLine(PersonasMod.Settings.directorNotes);
                    }
                }
                catch { }

                if (ctx.Inc_Memories)
                {
                    // 传入 -1，表示不根据时间过滤，直接读取最新的几条
                    string mems = DirectorMemoryContext.GetExternalMemories(p, -1);

                    if (!string.IsNullOrEmpty(mems))
                    {
                        sb.AppendLine("\n--- Memories ---");
                        sb.AppendLine(mems);
                    }
                }

                // 新增：常识注入 (最后执行)
                if (ctx.Inc_CommonKnowledge)
                {
                    // ★ 传入 p ★
                    string ck = DirectorMemoryContext.GetCommonKnowledge(sb.ToString(), p);
                    if (!string.IsNullOrEmpty(ck))
                    {
                        sb.AppendLine("\n--- Common Knowledge ---");
                        sb.AppendLine(ck);
                    }
                }
            }
            return sb.ToString();
        }
}
