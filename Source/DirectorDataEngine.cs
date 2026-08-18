using Ustas.RimAI.Communication.Personas;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.UI;
using Ustas.RimAI.Communication.Util;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace Ustas.RimAI.Communication.Personas
{
    public class HistoryLine
    {
        public string name { get; set; }
        public string target { get; set; }
        public string text { get; set; }
    }
    public static class DirectorDataEngine
	{
		public static string TempCurrentPersona = "";

		public static string BuildCompleteData(Pawn p, bool simpleEquipment = false)
		{
			StringBuilder stringBuilder = new StringBuilder();
			ContextSettings context = PersonasMod.Settings.Context;
			if (context.Inc_Basic)
			{
				stringBuilder.AppendLine(GetBasicInfo(p));
			}
			if (context.Inc_Race)
			{
				stringBuilder.AppendLine(GetRaceInfo(p, context.Inc_Race_Desc));
			}
			if (context.Inc_Genes)
			{
				stringBuilder.AppendLine(GetGenesInfo(p, context.Inc_Genes_Desc));
			}
			if (context.Inc_Backstory)
			{
				stringBuilder.AppendLine(GetBackstoryInfo(p, context.Inc_Backstory_Desc));
			}
			if (context.Inc_Relations)
			{
				stringBuilder.AppendLine(GetRelationsInfo(p));
			}
			if (context.Inc_Traits)
			{
				stringBuilder.AppendLine(GetTraitsInfo(p, context.Inc_Traits_Desc));
			}
			if (context.Inc_Ideology)
			{
				stringBuilder.AppendLine(GetIdeologyInfo(p, context.Inc_Ideology_Desc));
			}
			if (context.Inc_Skills)
			{
				stringBuilder.AppendLine(GetSkillsInfo(p, context.Inc_Skills_Desc));
			}
			if (context.Inc_Health)
			{
				stringBuilder.AppendLine(GetHealthInfo(p, context.Inc_Health_Desc));
			}
			if (context.Inc_Equipment) stringBuilder.AppendLine(GetEquipmentInfo(p, simpleEquipment));
			if (context.Inc_Equipment)
			{
				stringBuilder.AppendLine(GetEquipmentInfo(p));
			}
			if (context.Inc_Inventory)
			{
				stringBuilder.AppendLine(GetInventoryInfo(p));
			}
			if (context.Inc_RimPsyche)
			{
				stringBuilder.AppendLine(GetRimPsycheInfo(p));
			}
			if (context.Inc_Memories)
			{
				stringBuilder.AppendLine(GetMemoryInfo(p));
			}
			if (context.Inc_CommonKnowledge)
			{
				stringBuilder.AppendLine(GetCommonKnowledgeInfo(p, stringBuilder.ToString()));
			}
			return stringBuilder.ToString();
		}

		public static string GetBasicInfo(Pawn p)
		{
		    return DirectorPawnInfoFormatter.GetBasicInfo(p);
		}


		public static string GetRaceInfo(Pawn p, bool includeDesc)
		{
		    return DirectorPawnInfoFormatter.GetRaceInfo(p, includeDesc);
		}


		public static string GetGenesInfo(Pawn p, bool includeDesc)
		{
		    return DirectorPawnInfoFormatter.GetGenesInfo(p, includeDesc);
		}


		public static string GetBackstoryInfo(Pawn p, bool includeDesc)
		{
		    return DirectorPawnInfoFormatter.GetBackstoryInfo(p, includeDesc);
		}


		public static string GetRelationsInfo(Pawn p)
		{
		    return DirectorPawnInfoFormatter.GetRelationsInfo(p);
		}


		public static string GetTraitsInfo(Pawn p, bool includeDesc)
		{
		    return DirectorPawnInfoFormatter.GetTraitsInfo(p, includeDesc);
		}


		public static string GetIdeologyInfo(Pawn p, bool includeDesc)
		{
		    return DirectorPawnInfoFormatter.GetIdeologyInfo(p, includeDesc);
		}


		public static string GetSkillsInfo(Pawn p, bool includeDesc)
		{
		    return DirectorPawnInfoFormatter.GetSkillsInfo(p, includeDesc);
		}


		public static string GetHealthInfo(Pawn p, bool includeDesc)
		{
		    return DirectorPawnInfoFormatter.GetHealthInfo(p, includeDesc);
		}


		public static string GetEquipmentInfo(Pawn p, bool simpleMode = false)
		{
		    return DirectorPawnInfoFormatter.GetEquipmentInfo(p, simpleMode);
		}


		// ★★★ 核心：构建不受耐久度/磨损影响的稳定标签 ★★★
		// 只包含：材质 + 物品名 + 品质 (例如：传奇级 合成纤维T恤衫)
		private static string GetStableThingLabel(Thing t)
		{
		    return DirectorPawnInfoFormatter.GetStableThingLabel(t);
		}


		public static string GetInventoryInfo(Pawn p)
		{
		    return DirectorPawnInfoFormatter.GetInventoryInfo(p);
		}


		public static string GetRimPsycheInfo(Pawn p)
		{
            if (p == null) return "";
            return DirectorUtils.GetMauxRimPsycheData(p);
		}

		public static string GetMemoryInfo(Pawn p)
		{
            if (p == null) return "";
            return DirectorUtils.GetExternalMemories(p, -1) ?? "";
		}

		public static string GetCommonKnowledgeInfo(Pawn p, string context)
		{
            if (p == null) return "";
            return DirectorUtils.GetCommonKnowledge(context, p) ?? "";
		}

		public static string GetEvolveStatusDiff(Pawn p)
		{
            if (p == null) return "";
            var worldComp = Find.World.GetComponent<DirectorWorldComponent>();
			if (worldComp == null) return "";

			string oldSnapshot = worldComp.GetSnapshot(p); // 这是一个 Detailed 快照
			if (string.IsNullOrEmpty(oldSnapshot)) return "No previous snapshot.";

			// ★ 生成当前的 Detailed 快照进行对比 ★
			string currentSnapshot = DirectorUtils.BuildCustomCharacterData(p, isSnapshot: true, simpleEquipment: false);

			return DirectorUtils.GenerateDiffReport(oldSnapshot, currentSnapshot);
		}

		// GetDailyStatusDiff 逻辑已经在 WorldComponent 里封装好了，直接调用即可
		public static string GetDailyStatusDiff(Pawn p)
		{
			var worldComp = Find.World.GetComponent<DirectorWorldComponent>();
			return worldComp?.GetOrUpdateDailyDiff(p) ?? "";
		}

		public static string GetEvolveMemories(Pawn p)
		{
            if (p == null) return "";
            var worldComp = Find.World.GetComponent<DirectorWorldComponent>();
			int lastTick = worldComp?.GetLastEvolveTick(p) ?? -1;

			// 传入 lastTick，DirectorUtils.GetExternalMemories 会自动过滤掉旧记忆
			// 如果 lastTick 是 -1，它会返回最近的几条（作为保底）
			return DirectorUtils.GetExternalMemories(p, lastTick) ?? "No new memories.";
		}

		public static string GetTimeInfo(Pawn p)
		{
		    return DirectorPawnInfoFormatter.GetTimeInfo(p);
		}


        public static string GetSmartHistory(Pawn currentPawn, List<Pawn> allPawns, bool isMonologue)
        {
            if (currentPawn == null) return "";

            // 1. 获取预算
            int totalBudget = 5;
            try { totalBudget = Ustas.RimAI.Communication.Settings.Get().Context.ConversationHistoryCount; } catch { }
            if (isMonologue) totalBudget = Math.Min(totalBudget, 3);

            // 2. 确定人员
            var participants = allPawns ?? new List<Pawn>();
            if (!participants.Contains(currentPawn)) participants.Insert(0, currentPawn);

            // 3. 配额分配
            Dictionary<Pawn, int> pawnQuotas = new Dictionary<Pawn, int>();
            int pawnCount = participants.Count;

            if (totalBudget >= pawnCount)
            {
                foreach (var p in participants) pawnQuotas[p] = 1;
                pawnQuotas[currentPawn] += (totalBudget - pawnCount);
            }
            else
            {
                pawnQuotas[currentPawn] = 1;
                int remaining = totalBudget - 1;
                foreach (var p in participants)
                {
                    if (p == currentPawn) continue;
                    if (remaining > 0) { pawnQuotas[p] = 1; remaining--; }
                    else { pawnQuotas[p] = 0; }
                }
            }

            // 4. 执行提取
            StringBuilder finalSb = new StringBuilder();
            bool hasContent = false;

            // ★★★ 新增：全局去重池 (存储已处理过的原始 JSON 字符串) ★★★
            // 这样就能跨 Pawn 去重了
            HashSet<string> processedDialogues = new HashSet<string>();

            foreach (var p in participants)
            {
                if (!pawnQuotas.TryGetValue(p, out int quota) || quota <= 0) continue;

                // ★★★ 传入去重池 ★★★
                List<string> historyLines = ExtractHistoryForPawn(p, participants, quota, processedDialogues);

                if (historyLines != null && historyLines.Count > 0)
                {
                    if (hasContent) finalSb.AppendLine();
                    finalSb.AppendLine($"[History: {p.LabelShort}]");
                    for (int i = 0; i < historyLines.Count; i++)
                    {
                        finalSb.AppendLine($"[{i + 1}] {historyLines[i]}");
                    }
                    hasContent = true;
                }
            }

            if (hasContent)
            {
                if (isMonologue) return finalSb.ToString().TrimEnd();
                return "[Relevant Conversation History]:\n" + finalSb.ToString().TrimEnd();
            }
            return "";
        }

        // ★★★ 修改：返回 List<string> 而不是 string ★★★
        private static List<string> ExtractHistoryForPawn(Pawn p, List<Pawn> contextPawns, int limit, HashSet<string> processedDialogues)
        {
            var activeNames = new HashSet<string>();
            foreach (var cp in contextPawns) activeNames.Add(cp.LabelShort);

            var history = TalkHistory.GetMessageHistory(p, simplified: false);
            if (history == null || history.Count == 0) return null;

            var resultLines = new List<string>();

            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (resultLines.Count >= limit) break;

                var entry = history[i];
                if (entry.role != Role.AI)
                    continue;

                string rawContent = entry.message;
                if (processedDialogues.Contains(rawContent))
                    continue;

                try
                {
                    var lines = JsonUtil.DeserializeFromJson<List<HistoryLine>>(rawContent);
                    if (lines != null && lines.Count > 0)
                    {
                        StringBuilder sessionSb = new StringBuilder();
                        bool isRelevant = false;

                        foreach (var line in lines)
                        {
                            bool nameInContext = activeNames.Contains(line.name);
                            bool targetInContext = !string.IsNullOrEmpty(line.target) && activeNames.Contains(line.target);

                            if (nameInContext || targetInContext)
                            {
                                isRelevant = true;
                            }

                            string targetInfo = "";
                            if (!string.IsNullOrEmpty(line.target) && line.target != "None" && line.target != "自己" && line.target != line.name)
                            {
                                targetInfo = $" (to {line.target})";
                            }
                            sessionSb.AppendLine($"{line.name}{targetInfo}: {line.text}");
                        }

                        if (isRelevant)
                        {
                            resultLines.Insert(0, sessionSb.ToString().TrimEnd());
                            processedDialogues.Add(rawContent);
                        }
                    }
                }
                catch { }
            }

            return resultLines;
        }


        public static string GetActiveUIText(Pawn p)
        {
            // 1. 优先读取我们手动设置的缓存 (给 Evolve 功能用)
            if (!string.IsNullOrEmpty(TempCurrentPersona)) return TempCurrentPersona;

            // 2. 如果缓存为空，尝试从当前打开的窗口里抓取
            try
            {
                // 获取当前最顶层的窗口
                var window = Find.WindowStack.WindowOfType<PersonaEditorWindow>();

                if (window != null)
                {
                    if (window.EditedPawn == p)
                    {
                        return window.EditingPersonality ?? "";
                    }
                }
            }
            catch
            {
                // 静默失败，不要崩
            }

            // 3. 如果 UI 没打开，或者读不到，回退到读取 Hediff (已保存的数据)
            var hediff = Hediff_Persona.GetOrAddNew(p);
            return hediff?.Personality ?? "";
        }

        public static string GetDataByKey(Pawn p, string key)
		{
			if (p == null) return "";

			switch (key)
			{
				case "full_profile": return DirectorDataEngine.BuildCompleteData(p);
				case "basic.name": return p.LabelShortCap;
				case "basic.fullname": return p.Name?.ToStringFull ?? p.LabelShortCap;
				case "basic.gender": return p.gender.ToString();
				case "basic.age": return p.ageTracker.AgeBiologicalYears.ToString();
				case "basic.status": return DirectorUtils.GetPawnSocialStatus(p);
				case "basic.faction.label": return p.Faction?.Name ?? "None";
				case "basic.faction.desc": return p.Faction?.def?.description?.StripTags() ?? "";
				case "race.label": return p.def.label;
				case "race.desc": return p.def.description.StripTags();
				case "race.xenotype.label": return p.genes?.Xenotype?.label ?? "Baseliner";
				case "race.xenotype.desc": return p.genes?.Xenotype?.description.StripTags() ?? "";
				case "genes.list": return DirectorDataEngine.GetGenesInfo(p, includeDesc: false);
				case "genes.list_with_desc": return DirectorDataEngine.GetGenesInfo(p, includeDesc: true);
				case "backstory.childhood.title": return p.story?.Childhood?.TitleCapFor(p.gender) ?? "";
				case "backstory.childhood.desc": return p.story?.Childhood?.FullDescriptionFor(p).Resolve().StripTags() ?? "";
				case "backstory.adulthood.title": return p.story?.Adulthood?.TitleCapFor(p.gender) ?? "";
				case "backstory.adulthood.desc": return p.story?.Adulthood?.FullDescriptionFor(p).Resolve().StripTags() ?? "";
				case "traits.list": return DirectorDataEngine.GetTraitsInfo(p, includeDesc: false);
				case "traits.list_with_desc": return DirectorDataEngine.GetTraitsInfo(p, includeDesc: true);
				case "ideology.list": return DirectorDataEngine.GetIdeologyInfo(p, includeDesc: false);
				case "ideology.list_with_desc": return DirectorDataEngine.GetIdeologyInfo(p, includeDesc: true);
				case "skills.list": return DirectorDataEngine.GetSkillsInfo(p, includeDesc: false);
				case "skills.list_with_desc": return DirectorDataEngine.GetSkillsInfo(p, includeDesc: true);
				case "health.list": return DirectorDataEngine.GetHealthInfo(p, includeDesc: false);
				case "health.list_with_desc": return DirectorDataEngine.GetHealthInfo(p, includeDesc: true);
				case "relations": return DirectorDataEngine.GetRelationsInfo(p);
				case "equipment": return DirectorDataEngine.GetEquipmentInfo(p);
				case "inventory": return DirectorDataEngine.GetInventoryInfo(p);
				case "rimpsyche": return DirectorDataEngine.GetRimPsycheInfo(p);
				case "memories": return DirectorDataEngine.GetMemoryInfo(p);
				case "common_knowledge": return DirectorDataEngine.GetCommonKnowledgeInfo(p, DirectorDataEngine.BuildCompleteData(p));
				default:
					return ""; // 或者返回 $"{{Unknown: {key}}}"
			}
		}
	}
}
