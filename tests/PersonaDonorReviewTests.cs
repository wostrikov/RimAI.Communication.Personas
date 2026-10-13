using System;
using System.IO;
using Ustas.RimAI.Communication.Personas.Policy;

/// <summary>
/// What a review of the upstream mod's release brought in, and the defects
/// of our own that turned up while reading the module for it: the
/// per-pawn records nothing ever cleared, a character sheet built twice, the
/// equipment section the profile carried twice, and the pawns that lost their
/// history for being dead or away from home on the day of a load.
/// </summary>
internal static class PersonaDonorReviewTests
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

        // A role change says where the pawn came from, on one line.
        string hostile = PersonaTriggerContextPolicy.Faction("Red Hand", "hostile to the colony");
        string home = PersonaTriggerContextPolicy.Faction("New Hope", "the player's colony");
        T(hostile == "Red Hand (hostile to the colony)", "faction-with-standing");
        T(PersonaTriggerContextPolicy.Faction("Wanderers", null) == "Wanderers", "faction-without-standing");
        T(PersonaTriggerContextPolicy.Faction("  ", "hostile") == null, "no-name-no-faction");

        string captive = PersonaTriggerContextPolicy.RoleState(hostile, home, null);
        T(captive == "faction Red Hand (hostile to the colony), held by New Hope (the player's colony)", "prisoner-names-its-keeper");
        T(PersonaTriggerContextPolicy.RoleState(home, home, home) == "faction New Hope (the player's colony)", "own-faction-not-repeated");
        T(PersonaTriggerContextPolicy.RoleState(null, null, null) == "no faction", "nobody's-pawn");
        T(PersonaTriggerContextPolicy.RoleState(home, null, hostile).EndsWith(", taken from Red Hand (hostile to the colony)", StringComparison.Ordinal),
            "slave-names-where-it-was-taken-from");

        string recruited = PersonaTriggerContextPolicy.RoleChange("Prisoner", "Colonist", captive, PersonaTriggerContextPolicy.RoleState(home, null, null));
        T(recruited.StartsWith("Role changed from Prisoner to Colonist. Before: faction Red Hand", StringComparison.Ordinal), "role-change-leads-with-the-roles");
        T(recruited.EndsWith(" After: faction New Hope (the player's colony).", StringComparison.Ordinal), "role-change-ends-with-the-new-side");
        T(!recruited.Contains("\n"), "role-change-is-one-line");
        string same = PersonaTriggerContextPolicy.RoleState(home, null, null);
        T(PersonaTriggerContextPolicy.RoleChange("Slave", "Colonist", same, same) == "Role changed from Slave to Colonist.", "equal-sides-add-nothing");
        T(PersonaTriggerContextPolicy.RoleChange("Visitor", "Colonist", null, same) == "Role changed from Visitor to Colonist.", "unknown-side-adds-nothing");

        T(PersonaTriggerContextPolicy.TraitGained("Kind", null) == "Gained a new trait: Kind.", "trait-without-description");
        T(PersonaTriggerContextPolicy.TraitGained("Kind", "Ada is\nexceptionally  agreeable.") == "Gained a new trait: Kind. What it means: Ada is exceptionally agreeable.",
            "trait-description-on-one-line");
        string longTrait = PersonaTriggerContextPolicy.TraitGained("Kind", new string('x', 1000));
        T(longTrait.EndsWith("...", StringComparison.Ordinal)
            && longTrait.Length < PersonaTriggerContextPolicy.MaxTraitDescriptionChars + 60, "trait-description-capped");

        T(PersonaTriggerContextPolicy.Birth("Mila", "Boris") == "Became a parent: Mila was born. The other parent is Boris.", "birth-names-both");
        T(PersonaTriggerContextPolicy.Birth(null, null) == "Became a parent: a child was born.", "birth-with-nothing-known");
        T(PersonaTriggerContextPolicy.Birth("Mila", " ") == "Became a parent: Mila was born.", "birth-without-the-other-parent");

        // A rule that goes by age.
        T(PersonaAssignmentRulePolicy.AgeMatches(0, 0, 12) && PersonaAssignmentRulePolicy.AgeMatches(12, 0, 12), "age-ends-inclusive");
        T(!PersonaAssignmentRulePolicy.AgeMatches(13, 0, 12), "age-above-range");
        T(PersonaAssignmentRulePolicy.AgeMatches(40, 60, 18) && !PersonaAssignmentRulePolicy.AgeMatches(70, 60, 18), "age-range-typed-backwards");
        T(PersonaAssignmentRulePolicy.ClampAge(-5) == 0 && PersonaAssignmentRulePolicy.ClampAge(5000) == PersonaAssignmentRulePolicy.MaxAllowedAge, "age-clamped");

        var library = new PersonaLibrarySnapshot();
        library.Rules.Add(new PersonaLibraryRuleRecord { Type = "Age", MinAge = 3, MaxAge = 12, AllowedPresetIds = { "child" } });
        var read = PersonaPresetLibraryIoPolicy.Parse(PersonaPresetLibraryIoPolicy.Format(library));
        T(read.Accepted && read.Snapshot.Rules[0].Type == "Age"
            && read.Snapshot.Rules[0].MinAge == 3 && read.Snapshot.Rules[0].MaxAge == 12, "age-rule-survives-export");
        var olderFile = PersonaPresetLibraryIoPolicy.Parse(
            PersonaPresetLibraryIoPolicy.Format(library).Replace(" minAge=\"3\"", "").Replace(" maxAge=\"12\"", ""));
        T(olderFile.Accepted && olderFile.Snapshot.Rules[0].MinAge == 0 && olderFile.Snapshot.Rules[0].MaxAge == 99, "file-without-ages-reads-defaults");
        var merged = PersonaPresetLibraryIoPolicy.Apply(new PersonaLibrarySnapshot(), read.Snapshot, overwrite: false);
        T(merged.Rules[0].MinAge == 3 && merged.Rules[0].MaxAge == 12, "age-rule-survives-merge");

        string settings = Read("PersonasSettings.cs.src");
        T(settings.Contains("enum RuleType { FactionDef, RaceDef, XenotypeDef, Age }"), "age-is-the-last-rule-kind");
        string resolver = Read("PersonaResolver.cs.src");
        int ageCase = resolver.IndexOf("rule.type == RuleType.Age", StringComparison.Ordinal);
        int needsDef = resolver.IndexOf("string.IsNullOrEmpty(rule.targetDefName)", StringComparison.Ordinal);
        T(ageCase >= 0 && needsDef > ageCase, "age-rule-matched-before-the-def-is-asked-for");

        // Records kept per pawn are cleared for pawns the game no longer holds.
        string world = Read("DirectorWorldComponent.cs.src");
        foreach (string dictionary in new[] { "_lastEvolveTicks", "_lastEvolveBioAgeTicks", "_dataSnapshots", "_dailySnapshots", "_dailySnapshotDays" })
            T(world.Contains("RemoveMissingKeys(" + dictionary + ", known);"), "pruned-" + dictionary);
        T(world.Contains("_history.RemoveMissingPawns(known);") && world.Contains("_autoEvolvePawns.RemoveWhere(id => !known.Contains(id));"),
            "history-and-choices-pruned-from-the-same-census");
        T(world.Contains("_nextPruneTick = now + GenDate.TicksPerDay;"), "pruned-daily");
        T(world.Contains("_nextPruneTick = fromLoad ? 0 : GenDate.TicksPerDay;"), "first-prune-waits-for-the-first-tick");
        T(!world.Contains("AllMapsWorldAndTemporary_AliveOrDead"), "world-component-asks-the-census");
        T(world.Contains("string snapshot = snapshotData ?? DirectorUtils.BuildCustomCharacterData("), "set-timestamp-uses-the-sheet-it-is-given");
        T(!world.Contains("_processedPawnIds"), "unused-set-gone");

        // The census counts the dead on a map and the pawns away from one.
        string census = Read("PersonaPawnCensus.cs.src");
        T(census.Contains("PawnsFinder.All_AliveOrDead"), "census-includes-caravans-and-transporters");
        T(census.Contains("ThingRequest.ForGroup(ThingRequestGroup.Pawn)") && census.Contains("alsoGetSpawnedThings: false"), "census-includes-the-dead-on-a-map");
        T(!Read("PersonaHistoryStore.cs.src").Contains("PawnsFinder."), "history-store-takes-the-census");

        // One equipment section in the full profile.
        string engine = Read("DirectorDataEngine.cs.src");
        int profile = engine.IndexOf("public static string BuildCompleteData", StringComparison.Ordinal);
        int profileEnd = engine.IndexOf("public static string GetBasicInfo", StringComparison.Ordinal);
        T(profile >= 0 && profileEnd > profile && Count(engine.Substring(profile, profileEnd - profile), "GetEquipmentInfo(") == 1, "equipment-once");
        T(engine.Contains("DirectorUtils.GetExternalMemories(p, lastTick) ?? \"\";"), "no-memories-is-empty");

        // What a preset can reach.
        string adapter = Read("DirectorApiAdapter.cs.src");
        T(adapter.Contains("\"d_evolve_memories\"") && adapter.Contains("DirectorPresetRenderer.IsRendering ? DirectorDataEngine.GetEvolveMemories(p) : \"\""),
            "evolve-memories-only-in-a-director-request");
        T(adapter.Contains("\"evolve_director_notes\"") && adapter.Contains("\"autogen_director_notes\""), "notes-variables-registered");
        T(adapter.Contains("Reg(\"d_skills_list\", p => DirectorDataEngine.GetSkillsInfo(p, includeDesc: false));")
            && adapter.Contains("Reg(\"d_health_list\", p => DirectorDataEngine.GetHealthInfo(p, includeDesc: false));"),
            "plain-lists-carry-no-descriptions");
        string renderer = Read("DirectorPresetRenderer.cs.src");
        T(renderer.Contains("IsRendering && _automatic ? RenderNotes(notes, pawn) : string.Empty"), "notes-only-in-an-automatic-request");
        T(renderer.Contains("if (_renderingNotes) return string.Empty;"), "notes-quoting-themselves-end");
        T(Read("DirectorPersonaEvolve.cs.src").Contains("automatic: editorWindow == null"), "the-editors-evolve-is-not-automatic");

        string composer = Read("DirectorPromptComposer.cs.src");
        T(composer.Contains("GetPawns()?.FindAll(candidate => candidate != null)"), "missing-participants-filtered");
        T(composer.Contains("contextObj.Map = p?.Map ?? participants.Find(candidate => candidate.Map != null)?.Map;"), "map-from-the-pawn-then-anyone");
        return n;
    }

    private static int Count(string text, string what)
    {
        int count = 0;
        for (int at = text.IndexOf(what, StringComparison.Ordinal); at >= 0; at = text.IndexOf(what, at + what.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    private static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, name));
}
