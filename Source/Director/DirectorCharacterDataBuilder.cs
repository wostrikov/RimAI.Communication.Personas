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
                    sb.AppendLine(PersonaProfileExtractPolicy.IdentityMarker);
                    sb.AppendLine($"Name: {p.LabelShortCap}");
                    sb.AppendLine($"Gender: {p.gender}");
                    sb.AppendLine($"Age: {p.ageTracker.AgeBiologicalYears}");
                    sb.AppendLine($"Current Status: {DirectorPawnStatus.GetPawnSocialStatus(p)}");

                    if (p.Faction != null)
                    {
                        if (p.Faction.IsPlayer)
                        {
                            sb.Append($"Faction: {p.Faction.Name} (Player Colony)");

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
                                sb.AppendLine();
                            }
                        }
                        else
                        {
                            string relStr = p.Faction.HostileTo(Faction.OfPlayer) ? "Hostile" : "Neutral/Ally";
                            sb.AppendLine($"Faction: {p.Faction.Name} ({relStr})");

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
                    sb.AppendLine("\n" + PersonaProfileExtractPolicy.XenotypeMarker);
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
                    sb.AppendLine("\n" + PersonaProfileExtractPolicy.GenesMarker);
                    if (p.genes.Endogenes.Any())
                    {
                        sb.Append(PersonaProfileExtractPolicy.EndogeneMarker + " ");
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
                        sb.Append(PersonaProfileExtractPolicy.XenogeneMarker + " ");
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
                        sb.Append($"Childhood: {p.story.Childhood.TitleCapFor(p.gender)}");

                        if (ctx.Inc_Backstory_Desc)
                        {
                            string desc = p.story.Childhood.FullDescriptionFor(p).Resolve();

                            string cleanDesc = desc.Replace(p.story.Childhood.title, "").Trim();
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
                if (ctx.Inc_Relations && p.relations != null && p.relations.RelatedPawns.Any())
                {
                    StringBuilder relationSb = new StringBuilder();

                    foreach (Pawn otherPawn in p.relations.RelatedPawns)
                    {
                        PawnRelationDef relation = p.GetMostImportantRelation(otherPawn);
                        if (relation == null) continue;

                        bool isRelevant =
                            relation == PawnRelationDefOf.Parent ||
                            relation == PawnRelationDefOf.Child ||
                            relation == PawnRelationDefOf.Sibling ||
                            relation == PawnRelationDefOf.Spouse ||
                            relation == PawnRelationDefOf.Lover ||
                            relation == PawnRelationDefOf.Fiance ||
                            relation == PawnRelationDefOf.Bond ||
                            relation.defName == "Overseer";

                        if (!isRelevant) continue;

                        string relationLabel = relation.GetGenderSpecificLabelCap(otherPawn);

                        if (relation == PawnRelationDefOf.Child && relationLabel == "Child")
                            relationLabel = otherPawn.gender == Gender.Female ? "Daughter" : "Son";
                        else if (relation == PawnRelationDefOf.Sibling && relationLabel == "Sibling")
                            relationLabel = otherPawn.gender == Gender.Female ? "Sister" : "Brother";

                        if (string.IsNullOrEmpty(relationLabel)) relationLabel = relation.label;
                        relationLabel = relationLabel.CapitalizeFirst();

                        string status = DirectorPawnStatus.GetPawnShortStatus(otherPawn);

                        relationSb.AppendLine($"- {relationLabel}: {otherPawn.Name.ToStringShort} {status}".Trim());
                    }

                    if (relationSb.Length > 0)
                    {
                        sb.AppendLine("\n" + PersonaProfileExtractPolicy.RelationsMarker);
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
                    sb.AppendLine("\n" + PersonaProfileExtractPolicy.IdeologyMarker);
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
                    sb.AppendLine("\n" + PersonaProfileExtractPolicy.SkillsMarker);
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
                                Def vsePassionDef = DirectorModCompat.GetVSEPassionDef(skill);

                                if (vsePassionDef != null)
                                {
                                    sb.Append($" [{vsePassionDef.label.CapitalizeFirst()}]: {vsePassionDef.description}");
                                }
                                else
                                {
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
                    var visibleHediffs = p.health.hediffSet.hediffs.Where(h => h.Visible).ToList();

                    if (visibleHediffs.Any())
                    {
                        sb.AppendLine("\n--- Health ---");
                        foreach (var hediff in visibleHediffs)
                        {
                            string partName = hediff.Part != null ? hediff.Part.LabelCap : "Whole Body";

                            sb.Append($"- {partName}: {hediff.LabelCap}");

                            if (ctx.Inc_Health_Desc && !string.IsNullOrEmpty(hediff.def.description))
                            {
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
                        string withNotes = PersonaDirectorNotesPolicy.AppendNotes(
                            sb.ToString(),
                            PersonasMod.Settings.directorNotes,
                            true);
                        sb.Clear();
                        sb.Append(withNotes);
                    }
                }
                catch { }

                if (ctx.Inc_Memories)
                {
                    string mems = DirectorMemoryContext.GetExternalMemories(p, -1);

                    if (!string.IsNullOrEmpty(mems))
                    {
                        sb.AppendLine("\n--- Memories ---");
                        sb.AppendLine(mems);
                    }
                }

                if (ctx.Inc_CommonKnowledge)
                {
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
