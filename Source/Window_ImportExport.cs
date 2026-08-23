using RimWorld;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;
using System.Linq;
using Ustas.RimAI.Core.Storage;
using Ustas.RimAI.Core.Diagnostics;
using Ustas.RimAI.Core.Personas;
using Ustas.RimAI.Communication.Personas.Policy;

namespace Ustas.RimAI.Communication.Personas
{
    public class Window_ImportExport : Window
    {
        private string _text = "";

        private const string CommentedTemplate =
@"<!-- 
  Paste your library data here.
  The data must be valid XML and include the <Data> and <UserPresets> tags.
  Use Ctrl+A and DEL to delete this comment before importing.
  Example Format:
-->
<!--
<Data>
  <UserPresets>
    <li>
      <label>The Optimist</label>
      <personaText>An optimistic dreamer, full of imagination and hope.</personaText>
      <chattiness>0.5</chattiness>
      <category>Custom</category>
    </li>
    <li>
      <label>The Cynic</label>
      <personaText>A cynical realist who points out harsh truths.</personaText>
      <chattiness>0.8</chattiness>
      <category>Custom</category>
    </li>
  </UserPresets>
</Data>
-->
<!--
  Click 'Export...' to get your current library in this format.
-->";

        public Window_ImportExport()
        {
            doCloseX = true;
            draggable = true;
            resizeable = true;
            _text = CommentedTemplate;
        }

        public override Vector2 InitialSize => new Vector2(600f, 400f);

        public override void DoWindowContents(Rect inRect)
        {
            float buttonRowHeight = 35f;
            float gap = 5f;

            Rect buttonRowRect = new Rect(inRect.x, inRect.y, inRect.width, buttonRowHeight);
            Rect textRect = new Rect(inRect.x, buttonRowRect.yMax + gap, inRect.width, inRect.height - buttonRowHeight - gap);

            float btnWidth = (buttonRowRect.width - 30f) / 4f;

            Rect exportBtnRect = new Rect(buttonRowRect.x, buttonRowRect.y, btnWidth, 30f);

            if (Event.current.type == EventType.Repaint)
            {
                GUI.Button(exportBtnRect, "RPD_IO_Export".Translate());
            }

            if (Event.current.type == EventType.MouseUp && exportBtnRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.button == 0)
                {
                    ExportAndCopyToClipboard();
                }
                else if (Event.current.button == 1)
                {
                    ExportToFile();
                }
                Event.current.Use();
            }
            TooltipHandler.TipRegion(exportBtnRect, "RPD_IO_TipExport".Translate());

            if (Widgets.ButtonText(new Rect(buttonRowRect.x + btnWidth + 10f, buttonRowRect.y, btnWidth, 30f), "RPD_IO_ImportAppend".Translate()))
            {
                ImportFromText(false);
            }

            if (Widgets.ButtonText(new Rect(buttonRowRect.x + (btnWidth + 10f) * 2, buttonRowRect.y, btnWidth, 30f), "RPD_IO_ImportOverwrite".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("RPD_IO_ConfirmOverwrite".Translate(), () =>
                {
                    ImportFromText(true);
                }, destructive: true));
            }

            Rect importFileRect = new Rect(buttonRowRect.x + (btnWidth + 10f) * 3, buttonRowRect.y, btnWidth, 30f);
            if (Widgets.ButtonText(importFileRect, "RPD_IO_ImportFile".Translate()))
            {
                string dirPath = Path.Combine(GenFilePaths.SaveDataFolderPath, PersonaScribeLabels.LibraryExport.FolderName);
                if (!LocalStorage.Current.DirectoryExists(dirPath))
                {
                    Messages.Message("RPD_IO_MsgNoFolder".Translate(), MessageTypeDefOf.RejectInput, false);
                }
                else
                {
                    var files = LocalStorage.Current.GetFiles(dirPath, "*.xml").OrderByDescending(f => LocalStorage.Current.GetCreationTime(f)).ToList();
                    List<FloatMenuOption> opts = new List<FloatMenuOption>();
                    foreach (var f in files)
                    {
                        var fname = Path.GetFileName(f);
                        var path = f; // capture

                        opts.Add(new FloatMenuOption($"{fname} (Append)", () =>
                        {
                            _text = LocalStorage.Current.ReadAllText(path);
                            ImportFromText(false);
                        }));
                        opts.Add(new FloatMenuOption($"{fname} (Overwrite)", () =>
                        {
                            _text = LocalStorage.Current.ReadAllText(path);
                            ImportFromText(true);
                        }));
                    }
                    if (opts.Count == 0) opts.Add(new FloatMenuOption("RPD_IO_MsgNoFiles".Translate(), null));
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
            }

            Widgets.DrawLineHorizontal(inRect.x, buttonRowRect.yMax, inRect.width);

            _text = Widgets.TextArea(textRect, _text);
        }

        private void ExportAndCopyToClipboard()
        {
            try
            {
                string xml = GenerateExportXml();
                GUIUtility.systemCopyBuffer = xml;
                Messages.Message("RPD_IO_MsgExportClipboard".Translate(), MessageTypeDefOf.PositiveEvent, false);
                _text = xml;
            }
            catch (System.Exception ex)
            {
                RimAiLog.Error(RimAiLogCategory.Personas, $"Export failed: {ex}");
            }
        }

        private void ExportToFile()
        {
            try
            {
                string xml = GenerateExportXml();
                string dirPath = Path.Combine(GenFilePaths.SaveDataFolderPath, PersonaScribeLabels.LibraryExport.FolderName);
                LocalStorage.Current.CreateDirectory(dirPath);

                string filename = $"{PersonaScribeLabels.LibraryExport.FilenamePrefix}{System.DateTime.Now:yyyyMMdd_HHmmss}{PersonaScribeLabels.LibraryExport.FilenameExtension}";
                string fullPath = Path.Combine(dirPath, filename);

                LocalStorage.Current.WriteAllText(fullPath, xml);

                Messages.Message("RPD_IO_MsgExportFile".Translate(fullPath), MessageTypeDefOf.PositiveEvent, false);
                RimAiLog.Info(RimAiLogCategory.Personas, $"[RimAI.Personas] Library exported to: {fullPath}");

                _text = xml;
            }
            catch (System.Exception ex)
            {
                RimAiLog.Error(RimAiLogCategory.Personas, $"Export to file failed: {ex}");
            }
        }

        private string GenerateExportXml()
        {
            return PersonaPresetLibraryIoPolicy.Format(CaptureCurrent());
        }

        private void ImportFromText(bool overwrite)
        {
            if (string.IsNullOrWhiteSpace(_text)) return;

            try
            {
                var parsed = PersonaPresetLibraryIoPolicy.Parse(_text);
                if (parsed.Accepted && parsed.Snapshot != null)
                {
                    ApplySnapshot(PersonaPresetLibraryIoPolicy.Apply(CaptureCurrent(), parsed.Snapshot, overwrite));
                    PresetSynchronizer.SyncToRimTalk();
                    Messages.Message("RPD_IO_MsgImportSuccess".Translate(), MessageTypeDefOf.PositiveEvent, false);
                    Close();
                    return;
                }

                if (!TryImportLegacyScribe(overwrite))
                {
                    Messages.Message("RPD_IO_MsgImportFail".Translate(parsed.Reason ?? "parse"), MessageTypeDefOf.RejectInput, false);
                    return;
                }

                PresetSynchronizer.SyncToRimTalk();
                Messages.Message("RPD_IO_MsgImportSuccess".Translate(), MessageTypeDefOf.PositiveEvent, false);
                Close();
            }
            catch (System.Exception ex)
            {
                RimAiLog.Error(RimAiLogCategory.Personas, $"Import failed: {ex}");
                Messages.Message("RPD_IO_MsgImportFail".Translate(ex.Message), MessageTypeDefOf.RejectInput, false);
            }
        }

        private PersonaLibrarySnapshot CaptureCurrent()
        {
            var snapshot = new PersonaLibrarySnapshot();
            var presets = PersonasMod.Settings.userPresets;
            if (presets != null)
            {
                foreach (var preset in presets)
                {
                    if (preset == null)
                        continue;
                    snapshot.Presets.Add(new PersonaLibraryPresetRecord
                    {
                        Id = preset.id,
                        Label = preset.label,
                        PersonaText = preset.personaText,
                        Chattiness = preset.chattiness,
                        Category = preset.category,
                        Enabled = preset.enabled
                    });
                }
            }

            var rules = PersonasMod.Settings.assignmentRules;
            if (rules != null)
            {
                foreach (var rule in rules)
                {
                    if (rule == null)
                        continue;
                    snapshot.Rules.Add(new PersonaLibraryRuleRecord
                    {
                        Enabled = rule.enabled,
                        TargetDefName = rule.targetDefName,
                        Type = rule.type.ToString(),
                        Priority = rule.priority,
                        AllowedPresetIds = new List<string>(rule.allowedPresetIds ?? new List<string>())
                    });
                }
            }

            return snapshot;
        }

        private void ApplySnapshot(PersonaLibrarySnapshot snapshot)
        {
            var presets = new List<CustomPreset>();
            foreach (var record in snapshot?.Presets ?? new List<PersonaLibraryPresetRecord>())
            {
                if (record == null)
                    continue;
                presets.Add(new CustomPreset
                {
                    id = record.Id,
                    label = record.Label,
                    personaText = record.PersonaText,
                    chattiness = record.Chattiness,
                    category = record.Category,
                    enabled = record.Enabled
                });
            }

            var rules = new List<AssignmentRule>();
            foreach (var record in snapshot?.Rules ?? new List<PersonaLibraryRuleRecord>())
            {
                if (record == null)
                    continue;
                RuleType type;
                if (!System.Enum.TryParse(record.Type, true, out type))
                    type = RuleType.FactionDef;
                rules.Add(new AssignmentRule
                {
                    enabled = record.Enabled,
                    targetDefName = record.TargetDefName,
                    type = type,
                    priority = record.Priority,
                    allowedPresetIds = new List<string>(record.AllowedPresetIds ?? new List<string>())
                });
            }

            PersonasMod.Settings.userPresets = presets;
            PersonasMod.Settings.assignmentRules = rules;
        }

        private bool TryImportLegacyScribe(bool overwrite)
        {
            var loadedData = new TempExportData();
            string tempPath = null;
            try
            {
                tempPath = Path.Combine(Path.GetTempPath(), $"RPD_Import_{System.DateTime.Now:yyyyMMdd_HHmmssfff}.xml");
                LocalStorage.Current.WriteAllText(tempPath, _text);
                Scribe.loader.InitLoading(tempPath);
                loadedData.ExposeData();
                Scribe.loader.FinalizeLoading();
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempPath) && LocalStorage.Current.FileExists(tempPath))
                {
                    try { LocalStorage.Current.DeleteFile(tempPath); } catch { }
                }
            }

            if ((loadedData.Presets == null || loadedData.Presets.Count == 0) &&
                (loadedData.Rules == null || loadedData.Rules.Count == 0))
                return false;

            if (overwrite)
            {
                PersonasMod.Settings.userPresets = loadedData.Presets ?? new List<CustomPreset>();
                PersonasMod.Settings.assignmentRules = loadedData.Rules ?? new List<AssignmentRule>();
            }
            else
            {
                if (loadedData.Presets != null)
                    PersonasMod.Settings.userPresets.AddRange(loadedData.Presets);
                if (loadedData.Rules != null)
                    PersonasMod.Settings.assignmentRules.AddRange(loadedData.Rules);
            }

            return true;
        }

        private class TempExportData : IExposable
        {
            public List<CustomPreset> Presets;
            public List<AssignmentRule> Rules;

            public void ExposeData()
            {
                Scribe_Collections.Look(ref Presets, PersonaScribeLabels.LibraryExport.UserPresets, LookMode.Deep);
                Scribe_Collections.Look(ref Rules, PersonaScribeLabels.LibraryExport.AssignmentRules, LookMode.Deep);
            }
        }
    }
}