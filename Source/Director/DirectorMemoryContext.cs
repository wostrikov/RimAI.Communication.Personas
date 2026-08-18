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

public static class DirectorMemoryContext
{
        private static void AppendMemories(StringBuilder sb, IEnumerable<MemoryContextEntry> list, string header, int limit, int lastTick)
        {
            if (list == null) return;
            var newMemoryLines = new List<string>();

            foreach (var entry in list)
            {
                if (entry == null || newMemoryLines.Count >= limit) break;
                int memTick = entry.TimestampTicks;
                if (lastTick > 0 && memTick <= lastTick) break;

                string content = entry.Text;
                if (string.IsNullOrEmpty(content)) continue;

                string typeName = string.IsNullOrEmpty(entry.Category) ? "Memory" : entry.Category;
                string timeAgo = "";
                if (memTick > 0)
                {
                    int ticksElapsed = GenTicks.TicksGame - memTick;
                    int daysElapsed = ticksElapsed / GenDate.TicksPerDay;

                    if (daysElapsed < 1) timeAgo = "Today";
                    else if (daysElapsed == 1) timeAgo = "Yesterday";
                    else if (daysElapsed < 15) timeAgo = $"{daysElapsed} days ago";
                    else if (daysElapsed < 30) timeAgo = $"About a season ago ({daysElapsed} days)";
                    else if (daysElapsed < 60) timeAgo = $"Several seasons ago({daysElapsed} days)";
                    else if (daysElapsed < 120) timeAgo = $"About a year ago({daysElapsed} days)";
                    else timeAgo = $"A long time ago({daysElapsed} days)";
                }

                string line = $"[{typeName.CapitalizeFirst()}] {content}";
                if (!string.IsNullOrEmpty(timeAgo))
                    line += $" ({timeAgo})";
                newMemoryLines.Add($"- {line}");
            }

            if (newMemoryLines.Count > 0)
            {
                sb.AppendLine($"\n[{header}]:");
                newMemoryLines.Reverse();
                foreach (var line in newMemoryLines)
                    sb.AppendLine(line);
            }
        }

        public static string GetExternalMemories(Pawn p, int lastTick)
        {
            if (!ModsConfig.IsActive("ustas.rimai.communication.memory")) return null;
            var provider = MemoryContextAccess.Current;
            if (provider == null) return null;

            try
            {
                var result = provider.GetContext(new MemoryContextRequest
                {
                    Pawn = p,
                    PawnId = p?.ThingID,
                    SinceTick = lastTick,
                    PerLayerLimit = 5,
                    LayeredPawnMemories = true
                });
                var memories = result?.Memories;
                if (memories == null || memories.Count == 0) return null;

                StringBuilder sb = new StringBuilder();
                AppendMemories(sb, memories.Where(m => m.Kind == "Archive"), "Long-Term (Archive)", 5, lastTick);
                AppendMemories(sb, memories.Where(m => m.Kind == "EventLog"), "Mid-Term (Recent Events)", 5, lastTick);
                AppendMemories(sb, memories.Where(m => m.Kind == "Situational"), "Short-Term (Immediate)", 5, lastTick);
                return sb.Length > 0 ? sb.ToString().Trim() : null;
            }
            catch (Exception ex)
            {
                if (PersonasMod.Settings.EnableDebugLog)
                    RimAiLog.Warning(RimAiLogCategory.Personas, $"[Director] Critical error reading memories: {ex}");
                return null;
            }
        }

        public static string GetCommonKnowledge(string context, Pawn p)
        {
            if (!ModsConfig.IsActive("ustas.rimai.communication.memory")) return null;
            var provider = MemoryContextAccess.Knowledge;
            if (provider == null) return null;

            try
            {
                var result = provider.GetKnowledge(new MemoryContextRequest
                {
                    Query = context,
                    Pawn = p,
                    PawnId = p?.ThingID,
                    MaxEntries = 5
                });
                return string.IsNullOrEmpty(result?.Projection) ? null : result.Projection;
            }
            catch (Exception ex)
            {
                if (PersonasMod.Settings.EnableDebugLog)
                    RimAiLog.Warning(RimAiLogCategory.Personas, $"[Director] CK injection failed: {ex.Message}");
            }
            return null;
        }
}
