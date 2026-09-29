using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Personas.Policy;

internal static class PersonaAutomationPolicyTests
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

        const int day = PersonaAutomationPolicy.TicksPerDay;
        T(!PersonaAutomationPolicy.IsEvolveDue(-1, 100 * day, 5), "due-needs-anchor");
        T(!PersonaAutomationPolicy.IsEvolveDue(0, 100 * day, 0), "due-zero-interval-off");
        T(!PersonaAutomationPolicy.IsEvolveDue(10 * day, 14 * day, 5), "due-not-yet");
        T(PersonaAutomationPolicy.IsEvolveDue(10 * day, 15 * day, 5), "due-exact");
        T(PersonaAutomationPolicy.ClampIntervalDays(-3) == 0, "interval-clamp-low");
        T(PersonaAutomationPolicy.ClampIntervalDays(99999) == PersonaAutomationPolicy.MaxIntervalDays, "interval-clamp-high");

        var keys = new List<string>();
        var contexts = new List<string>();
        T(PersonaAutomationPolicy.TryAddTrigger(keys, contexts, "marriage:1:2", "Married Ada."), "trigger-first");
        T(!PersonaAutomationPolicy.TryAddTrigger(keys, contexts, "marriage:1:2", "Married Ada."), "trigger-duplicate-key");
        T(PersonaAutomationPolicy.TryAddTrigger(keys, contexts, "schedule:1:0", null), "trigger-no-context");
        T(contexts.Count == 1, "trigger-empty-context-not-added");
        for (int i = 0; i < 20; i++)
            PersonaAutomationPolicy.TryAddTrigger(keys, contexts, "trait:" + i, "Trait " + i);
        T(contexts.Count == PersonaAutomationPolicy.MaxTriggerContexts, "trigger-context-cap");

        T(PersonaAutomationPolicy.ComposeEvolved("old", "new", true) == "new", "evolve-overwrite");
        string appended = PersonaAutomationPolicy.ComposeEvolved("old", "new", false);
        T(appended.StartsWith("old") && appended.EndsWith(PersonaAutomationPolicy.DevelopmentTag + " new"), "evolve-append");
        T(PersonaAutomationPolicy.ComposeEvolved("old", "  ", false) == "old", "evolve-empty-keeps-original");

        var dedupe = new TriggerDeduplicator(120, day);
        T(!dedupe.IsDuplicate("birth:7", 1000), "dedupe-first");
        dedupe.MarkAccepted("birth:7", 1000);
        T(dedupe.IsDuplicate("birth:7", 1100), "dedupe-inside-window");
        T(!dedupe.IsDuplicate("birth:7", 1121), "dedupe-after-window");
        for (int i = 0; i < 300; i++)
            dedupe.MarkAccepted("k" + i, 1000 + 2 * day);
        T(!dedupe.IsDuplicate("birth:7", 1000 + 2 * day) && dedupe.Count <= 301, "dedupe-prunes-expired");

        T(!PersonaHistoryPolicy.ShouldRecord("", "new"), "history-empty-before");
        T(!PersonaHistoryPolicy.ShouldRecord("same ", " same"), "history-unchanged");
        T(PersonaHistoryPolicy.ShouldRecord("old", "new"), "history-changed");
        T(!PersonaHistoryPolicy.ShouldRecord(new string('x', PersonaHistoryPolicy.MaxRecordedChars + 1), "new"), "history-oversized");
        T(PersonaHistoryPolicy.ClampMaxRecords(0) == PersonaHistoryPolicy.MinMaxRecords, "history-clamp-low");
        T(PersonaHistoryPolicy.ClampMaxRecords(500) == PersonaHistoryPolicy.MaxMaxRecords, "history-clamp-high");
        T(PersonaHistoryPolicy.ClampContext(new string('c', 5000)).Length == PersonaHistoryPolicy.MaxContextChars, "history-context-clamp");

        var records = new List<int> { 9, 8, 7, 6, 5, 4 };
        PersonaHistoryPolicy.TrimNewestFirst(records, 3);
        T(records.Count == 3 && records[0] == 9 && records[2] == 7, "history-trim-keeps-newest");

        string evolve = PersonaEvolutionPolicy.Compose("voice", "3 days", null, null, false, null, "Married Ada.");
        T(evolve.Contains(PersonaEvolutionPolicy.TriggerEventsMarker) && evolve.Contains("Married Ada."), "evolve-trigger-section");
        T(!PersonaEvolutionPolicy.Compose("voice", "3 days", null, null, false, null).Contains(PersonaEvolutionPolicy.TriggerEventsMarker), "evolve-no-trigger-section");
        return n;
    }
}
