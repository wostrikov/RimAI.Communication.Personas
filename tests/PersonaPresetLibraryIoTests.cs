using System;
using System.IO;
using Ustas.RimAI.Communication.Personas.Policy;

internal static class PersonaPresetLibraryIoTests
{
    public static int Run()
    {
        int n = 0;
        void T(bool x, string s)
        {
            if (!x)
                throw new Exception("FAILED " + s);
            n++;
        }

        var original = new PersonaLibrarySnapshot();
        original.Presets.Add(new PersonaLibraryPresetRecord
        {
            Id = "p1",
            Label = "The Optimist",
            PersonaText = "Hopeful.\nSecond line.",
            Chattiness = 0.5f,
            Category = "Custom",
            Enabled = true
        });
        original.Rules.Add(new PersonaLibraryRuleRecord
        {
            Enabled = true,
            Type = "FactionDef",
            Priority = 3,
            TargetDefName = "OutlanderCivil",
            AllowedPresetIds = { "p1" }
        });

        string xml = PersonaPresetLibraryIoPolicy.Format(original);
        var parsed = PersonaPresetLibraryIoPolicy.Parse(xml);
        T(parsed.Accepted, "parse-ok");
        T(parsed.Snapshot.Presets.Count == 1, "preset-count");
        T(parsed.Snapshot.Presets[0].Id == "p1", "preset-id");
        T(parsed.Snapshot.Presets[0].Label == "The Optimist", "preset-label");
        T(parsed.Snapshot.Presets[0].PersonaText.Contains("Second line"), "preset-multiline");
        T(Math.Abs(parsed.Snapshot.Presets[0].Chattiness - 0.5f) < 0.0001f, "preset-chattiness");
        T(parsed.Snapshot.Rules.Count == 1, "rule-count");
        T(parsed.Snapshot.Rules[0].TargetDefName == "OutlanderCivil", "rule-target");
        T(parsed.Snapshot.Rules[0].AllowedPresetIds.Contains("p1"), "rule-ids");

        var current = new PersonaLibrarySnapshot();
        current.Presets.Add(new PersonaLibraryPresetRecord { Id = "keep", Label = "Keep" });
        var appended = PersonaPresetLibraryIoPolicy.Apply(current, parsed.Snapshot, overwrite: false);
        T(appended.Presets.Count == 2, "append-keeps-existing");
        T(appended.Presets[0].Id == "keep", "append-first");
        T(appended.Presets[1].Id == "p1", "append-incoming");

        var overwritten = PersonaPresetLibraryIoPolicy.Apply(current, parsed.Snapshot, overwrite: true);
        T(overwritten.Presets.Count == 1 && overwritten.Presets[0].Id == "p1", "overwrite-replaces");
        T(current.Presets.Count == 1 && current.Presets[0].Id == "keep", "apply-does-not-mutate-current");

        T(!PersonaPresetLibraryIoPolicy.Parse("").Accepted, "fail-empty");
        T(!PersonaPresetLibraryIoPolicy.Parse("not xml").Accepted, "fail-garbage");
        T(!PersonaPresetLibraryIoPolicy.Parse("<Data><UserPresets /></Data>").Accepted, "fail-legacy-root");
        var rejected = PersonaPresetLibraryIoPolicy.Apply(current, null, true);
        T(rejected.Presets.Count == 1 && rejected.Presets[0].Id == "keep", "null-incoming-fail-closed");

        string window = Read("Window_ImportExport.cs.src");
        T(window.Contains("PersonaPresetLibraryIoPolicy.Format"), "host-export");
        T(window.Contains("PersonaPresetLibraryIoPolicy.Parse"), "host-parse");
        T(window.Contains("PersonaPresetLibraryIoPolicy.Apply"), "host-apply");
        return n;
    }

    static string Read(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, name);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
