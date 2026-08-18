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

namespace Ustas.RimAI.Communication.Personas;

internal static class DirectorPawnInfoFormatter
{

		internal static string GetBackstoryInfo(Pawn p, bool includeDesc)
		{
            if (p == null || p.story == null) return "";
            StringBuilder stringBuilder = new StringBuilder();
			if (p.story.Childhood != null)
			{
				stringBuilder.Append("Childhood: " + p.story.Childhood.TitleCapFor(p.gender));
				if (includeDesc)
				{
					string text = p.story.Childhood.FullDescriptionFor(p).Resolve();
					string text2 = text.Replace(p.story.Childhood.title, "").Trim();
					if (!string.IsNullOrWhiteSpace(text2))
					{
						stringBuilder.AppendLine(":\n" + text2.StripTags().Trim());
					}
					else
					{
						stringBuilder.AppendLine();
					}
				}
				else
				{
					stringBuilder.AppendLine();
				}
			}
			if (p.story.Adulthood != null)
			{
				stringBuilder.Append("Adulthood: " + p.story.Adulthood.TitleCapFor(p.gender));
				if (includeDesc)
				{
					string text3 = p.story.Adulthood.FullDescriptionFor(p).Resolve();
					string text4 = text3.Replace(p.story.Adulthood.title, "").Trim();
					if (!string.IsNullOrWhiteSpace(text4))
					{
						stringBuilder.AppendLine(":\n" + text4.StripTags().Trim());
					}
					else
					{
						stringBuilder.AppendLine();
					}
				}
				else
				{
					stringBuilder.AppendLine();
				}
			}
			return stringBuilder.ToString();
		}

		internal static string GetBasicInfo(Pawn p)
		{
            if (p == null) return "";
            StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("--- Basic Info ---");
			stringBuilder.AppendLine("Name: " + p.LabelShortCap);
			stringBuilder.AppendLine($"Gender: {p.gender}");
			stringBuilder.AppendLine($"Age: {p.ageTracker.AgeBiologicalYears}");
			string text = DirectorUtils.GetPawnSocialStatus(p);
			stringBuilder.AppendLine("Status: " + text);
			if (p.Faction != null)
			{
				if (p.Faction.IsPlayer)
				{
					stringBuilder.Append("Faction: " + p.Faction.Name + " (Player Colony)");
					if (p.IsSlave && p.guest?.SlaveFaction != null)
					{
						stringBuilder.AppendLine();
						stringBuilder.AppendLine("Origin Faction (Enslaved from): " + p.guest.SlaveFaction.Name);
						string description = p.guest.SlaveFaction.def.description;
						if (!string.IsNullOrEmpty(description))
						{
							stringBuilder.AppendLine("Origin Description: " + description.StripTags().Replace('\n', ' '));
						}
					}
					else
					{
						stringBuilder.AppendLine();
					}
				}
				else
				{
					string text2 = (p.Faction.HostileTo(Faction.OfPlayer) ? "Hostile" : "Neutral/Ally");
					stringBuilder.AppendLine("Faction: " + p.Faction.Name + " (" + text2 + ")");
					string description2 = p.Faction.def.description;
					if (!string.IsNullOrEmpty(description2))
					{
						stringBuilder.AppendLine("Faction Description: " + description2.StripTags().Replace('\n', ' '));
					}
				}
			}
			else
			{
				stringBuilder.AppendLine("Faction: None (Independent)");
			}
			return stringBuilder.ToString();
		}

		internal static string GetSkillsInfo(Pawn p, bool includeDesc)
		{
            if (p == null || p.skills == null) return "";
            StringBuilder stringBuilder = new StringBuilder();
			foreach (SkillRecord skill in p.skills.skills)
			{
				stringBuilder.Append(skill.def.label + ": ");
				bool flag = p.WorkTagIsDisabled(WorkTags.AllWork);
				if (!flag)
				{
					foreach (WorkTypeDef item in DefDatabase<WorkTypeDef>.AllDefsListForReading)
					{
						if (item.relevantSkills.Contains(skill.def) && p.WorkTypeIsDisabled(item))
						{
							flag = true;
							break;
						}
					}
				}
				if (flag)
				{
					stringBuilder.Append("[INCAPABLE]");
				}
				else
				{
					stringBuilder.Append(skill.Level);
					if (includeDesc && skill.passion != Passion.None)
					{
						Def def = DirectorUtils.GetVSEPassionDef(skill);
						if (def != null)
						{
							stringBuilder.Append(" [" + def.label.CapitalizeFirst() + "]: " + def.description);
						}
						else
						{
							string text = DirectorUtils.GetPassionInfoHardcoded(skill.passion);
							stringBuilder.Append(" " + text);
						}
					}
				}
				stringBuilder.AppendLine();
			}
			return stringBuilder.ToString();
		}

		internal static string GetGenesInfo(Pawn p, bool includeDesc)
		{
            if (p == null || p.genes == null) return "";
            StringBuilder stringBuilder = new StringBuilder();
			if (p.genes.Endogenes.Any())
			{
				stringBuilder.Append("[Endogenes (Natural)]: ");
				foreach (Gene endogene in p.genes.Endogenes)
				{
					if (endogene.def.displayCategory != GeneCategoryDefOf.Miscellaneous && !endogene.Overridden)
					{
						stringBuilder.Append(endogene.LabelCap);
						if (includeDesc)
						{
							stringBuilder.Append("(" + endogene.def.description + ")");
						}
						stringBuilder.Append(", ");
					}
				}
				stringBuilder.AppendLine();
			}
			if (p.genes.Xenogenes.Any())
			{
				stringBuilder.Append("[Xenogenes (Artificial)]: ");
				foreach (Gene xenogene in p.genes.Xenogenes)
				{
					if (xenogene.def.displayCategory != GeneCategoryDefOf.Miscellaneous && !xenogene.Overridden)
					{
						stringBuilder.Append(xenogene.LabelCap);
						if (includeDesc)
						{
							stringBuilder.Append("(" + xenogene.def.description + ")");
						}
						stringBuilder.Append(", ");
					}
				}
				stringBuilder.AppendLine();
			}
			return stringBuilder.ToString();
		}

		internal static string GetRelationsInfo(Pawn p)
		{
            if (p == null || p.relations == null) return "";
            StringBuilder stringBuilder = new StringBuilder();
			StringBuilder stringBuilder2 = new StringBuilder();
			foreach (Pawn relatedPawn in p.relations.RelatedPawns)
			{
				PawnRelationDef mostImportantRelation = p.GetMostImportantRelation(relatedPawn);
				if (mostImportantRelation != null && (mostImportantRelation == PawnRelationDefOf.Parent || mostImportantRelation == PawnRelationDefOf.Child || mostImportantRelation == PawnRelationDefOf.Sibling || mostImportantRelation == PawnRelationDefOf.Spouse || mostImportantRelation == PawnRelationDefOf.Lover || mostImportantRelation == PawnRelationDefOf.Fiance || mostImportantRelation == PawnRelationDefOf.Bond || mostImportantRelation.defName == "Overseer"))
				{
					string text = mostImportantRelation.GetGenderSpecificLabelCap(relatedPawn);
					if (mostImportantRelation == PawnRelationDefOf.Child && text == "Child")
					{
						text = ((relatedPawn.gender == Gender.Female) ? "Daughter" : "Son");
					}
					else if (mostImportantRelation == PawnRelationDefOf.Sibling && text == "Sibling")
					{
						text = ((relatedPawn.gender == Gender.Female) ? "Sister" : "Brother");
					}
					if (string.IsNullOrEmpty(text))
					{
						text = mostImportantRelation.label;
					}
					text = text.CapitalizeFirst();
					string text2 = DirectorUtils.GetPawnShortStatus(relatedPawn) ?? "";
					stringBuilder2.AppendLine(("- " + text + ": " + relatedPawn.Name.ToStringShort + " " + text2).Trim());
				}
			}
			if (stringBuilder2.Length > 0)
			{
				stringBuilder.AppendLine("\n--- Key Relationships ---");
				stringBuilder.Append(stringBuilder2);
			}
			return stringBuilder.ToString();
		}

		internal static string GetEquipmentInfo(Pawn p, bool simpleMode = false)
		{
            if (p == null) return "";
            var sb = new StringBuilder();
			bool hasContent = false;

			// 武器
			if (p.equipment != null)
			{
				foreach (var eq in p.equipment.AllEquipmentListForReading)
				{
					if (!hasContent) { sb.AppendLine("--- Equipment ---"); hasContent = true; }

					// 根据模式选择标签生成方式
					string label = simpleMode ? GetStableThingLabel(eq) : eq.LabelCap;
					sb.AppendLine($"- [Weapon]: {label}");
				}
			}

			// 服装
			if (p.apparel != null)
			{
				foreach (var app in p.apparel.WornApparel)
				{
					if (!hasContent) { sb.AppendLine("--- Equipment ---"); hasContent = true; }

					string label = simpleMode ? GetStableThingLabel(app) : app.LabelCap;
					sb.AppendLine($"- [Apparel]: {label}");
				}
			}

			return sb.ToString();
		}

		internal static string GetRaceInfo(Pawn p, bool includeDesc)
		{
            if (p == null) return "";
            StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("Race: " + p.def.label);
			if (includeDesc)
			{
				stringBuilder.AppendLine(": " + p.def.description.StripTags());
			}
			else
			{
				stringBuilder.AppendLine();
			}
			if (p.genes?.Xenotype != null)
			{
				stringBuilder.Append("Xenotype: " + p.genes.XenotypeLabel);
				if (includeDesc)
				{
					stringBuilder.AppendLine(": " + p.genes.Xenotype.description.StripTags());
				}
				else
				{
					stringBuilder.AppendLine();
				}
			}
			return stringBuilder.ToString();
		}

		internal static string GetHealthInfo(Pawn p, bool includeDesc)
		{
            if (p == null || p.health?.hediffSet == null) return "";
            StringBuilder stringBuilder = new StringBuilder();
			List<Hediff> list = p.health.hediffSet.hediffs.Where((Hediff h) => h.Visible).ToList();
			if (list.Any())
			{
				stringBuilder.AppendLine("\n--- Health ---");
				foreach (Hediff item in list)
				{
					string text = ((item.Part != null) ? item.Part.LabelCap : "Whole Body");
					stringBuilder.Append("- " + text + ": " + item.LabelCap);
					if (includeDesc && !string.IsNullOrEmpty(item.def.description))
					{
						string text2 = item.def.description.StripTags().Replace('\n', ' ');
						stringBuilder.AppendLine(" (" + text2 + ")");
					}
					else
					{
						stringBuilder.AppendLine();
					}
				}
			}
			return stringBuilder.ToString();
		}

		internal static string GetIdeologyInfo(Pawn p, bool includeDesc)
		{
            if (p == null || p.Ideo == null) return "";
            StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("Religion: " + p.Ideo.name);
			foreach (MemeDef meme in p.Ideo.memes)
			{
				stringBuilder.Append($"Meme [{meme.LabelCap}]");
				if (includeDesc)
				{
					stringBuilder.AppendLine(": " + meme.description);
				}
				else
				{
					stringBuilder.AppendLine();
				}
			}
			return stringBuilder.ToString();
		}

		internal static string GetTraitsInfo(Pawn p, bool includeDesc)
		{
            if (p == null || p.story?.traits == null) return "";
            StringBuilder stringBuilder = new StringBuilder();
			foreach (Trait allTrait in p.story.traits.allTraits)
			{
				stringBuilder.Append(allTrait.Label);
				if (includeDesc)
				{
					stringBuilder.AppendLine(": " + allTrait.CurrentData.description);
				}
				else
				{
					stringBuilder.AppendLine();
				}
			}
			return stringBuilder.ToString();
		}

		internal static string GetInventoryInfo(Pawn p)
		{
            if (p == null) return "";
            StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("\n--- Inventory ---");
			foreach (Thing item in p.inventory.innerContainer)
			{
				stringBuilder.AppendLine("- " + item.LabelCap);
			}
			return stringBuilder.ToString();
		}

		internal static string GetTimeInfo(Pawn p)
		{
			var worldComp = Find.World.GetComponent<DirectorWorldComponent>();
			if (worldComp == null) return "No records.";
			int lastTick = worldComp.GetLastEvolveTick(p);
			if (lastTick <= 0) return "No previous update record.";

			int days = (GenTicks.TicksGame - lastTick) / 60000;
			long ageThen = worldComp.GetLastEvolveBioAgeTicks(p) / 3600000;
			long ageNow = p.ageTracker.AgeBiologicalYears;

			string info = $"{days} days passed.";
			if (ageNow > ageThen) info += $" Aged {ageThen}->{ageNow}.";
			return info;
		}

		// ★★★ 核心：构建不受耐久度/磨损影响的稳定标签 ★★★
		// 只包含：材质 + 物品名 + 品质 (例如：传奇级 合成纤维T恤衫)
		internal static string GetStableThingLabel(Thing t)
		{
            if (t == null) return "";
            // GenLabel.ThingLabel 基础生成 (材质+名字)
            string baseLabel = GenLabel.ThingLabel(t.def, t.Stuff).CapitalizeFirst();

			// 手动拼接品质 (如果存在)
			if (t.TryGetComp<CompQuality>() is CompQuality qc)
			{
				baseLabel += $" ({qc.Quality.GetLabel()})";
			}

			// 忽略耐久度 (HitPoints) 和 磨损前缀 (Tattered/Worn out)
			return baseLabel;
		}
}
