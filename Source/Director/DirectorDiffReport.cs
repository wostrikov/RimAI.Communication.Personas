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

public static class DirectorDiffReport
{
        public static string GenerateDiffReport(string oldSnapshot, string newSnapshot)
        {
            if (oldSnapshot == newSnapshot) return "No significant changes.";

            var oldBlocks = ParseSnapshotToBlocks(oldSnapshot);
            var newBlocks = ParseSnapshotToBlocks(newSnapshot);

            StringBuilder diffSb = new StringBuilder();

            var allBlockTitles = oldBlocks.Keys.Union(newBlocks.Keys).Distinct();

            foreach (var title in allBlockTitles)
            {
                if (title.Contains("Backstory"))
                {
                    continue; // 直接跳过，不进行对比
                }

                oldBlocks.TryGetValue(title, out var oldContent);
                newBlocks.TryGetValue(title, out var newContent);

                if (oldContent == newContent) continue;

                // ★★★ 核心修复：默认列表，特判键值 ★★★
                // 只有明确知道是 Key-Value 格式的块，才用 KeyValue 对比
                // 其他所有（包括未来新增的）都按安全的 List 方式对比
                if (title.Contains("Basic Info") || title.Contains("Skills"))
                {
                    // 使用“键值对对比”模式
                    CompareKeyValueBlock(diffSb, title, oldContent, newContent);
                }
                else
                {
                    // 其他所有块都使用安全的“列表对比”模式
                    CompareListBlock(diffSb, title, oldContent, newContent);
                }
            }

            string report = diffSb.ToString().Trim();
            return string.IsNullOrEmpty(report) ? "No significant changes." : report;
        }

        private static Dictionary<string, string> ParseSnapshotToBlocks(string snapshot)
                {
                    var blocks = new Dictionary<string, string>();
                    if (string.IsNullOrEmpty(snapshot)) return blocks;
        
                    var lines = snapshot.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    string currentTitle = "General";
                    StringBuilder currentContent = new StringBuilder();
        
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("---") && line.EndsWith("---"))
                        {
                            // 保存上一个块
                            if (currentContent.Length > 0)
                                blocks[currentTitle] = currentContent.ToString().Trim();
        
                            // 开始新块
                            currentTitle = line.Trim('-', ' ');
                            currentContent.Clear();
                        }
                        else
                        {
                            currentContent.AppendLine(line);
                        }
                    }
                    // 保存最后一个块
                    if (currentContent.Length > 0)
                        blocks[currentTitle] = currentContent.ToString().Trim();
        
                    return blocks;
                }
        
                /// <summary>
                /// 对比列表型数据块 (如 Traits, Genes)
                /// </summary>

        private static void CompareListBlock(StringBuilder diffSb, string title, string oldContent, string newContent)
        {
            var oldItems = new HashSet<string>(string.IsNullOrEmpty(oldContent)
                ? new string[0]
                : oldContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()));

            var newItems = new HashSet<string>(string.IsNullOrEmpty(newContent)
                ? new string[0]
                : newContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()));

            var added = newItems.Except(oldItems).ToList();
            var removed = oldItems.Except(newItems).ToList();

            if (added.Any() || removed.Any())
            {
                diffSb.AppendLine($"Changes in {title}:");
                foreach (var item in added) diffSb.AppendLine($"- Added: {item}");
                foreach (var item in removed) diffSb.AppendLine($"- Removed: {item}");
            }
        }

        private static void CompareKeyValueBlock(StringBuilder diffSb, string title, string oldContent, string newContent)
        {
            var oldDict = (string.IsNullOrEmpty(oldContent) ? "" : oldContent).Split('\n')
                .Select(line => line.Trim().Split(new[] { ':' }, 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());

            var newDict = (string.IsNullOrEmpty(newContent) ? "" : newContent).Split('\n')
                .Select(line => line.Trim().Split(new[] { ':' }, 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());

            var changes = new List<string>();
            foreach (var kvp in newDict)
            {
                if (oldDict.TryGetValue(kvp.Key, out var oldValue) && oldValue != kvp.Value)
                {
                    changes.Add($"- {kvp.Key}: {oldValue} -> {kvp.Value}");
                }
            }

            if (changes.Any())
            {
                diffSb.AppendLine($"Changes in {title}:");
                foreach (var change in changes) diffSb.AppendLine(change);
            }
        }
}
