using System;
using System.Collections.Generic;
using RimWorld;
using Ustas.RimAI.Core.TestDriver;
using Verse;

namespace Ustas.RimAI.Communication.Personas.Integration;

/// <summary>
/// Live batch-console observation. Opens Window_BatchDirector, applies a
/// character-type filter, and does not call a paid provider.
/// </summary>
public static class PersonasPipelineProbe
{
    public static void Register()
    {
        TestDriverModuleOperations.Register(
            TestDriverCommandNames.ProbePersonas,
            (request, _) => new TestDriverDelegateOperation(() => Run(request)));
    }

    static TestDriverProgress Run(TestDriverRequest request)
    {
        if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null)
            return TestDriverProgress.Failed("probe_personas requires a loaded game");

        var mode = request.Arguments.GetString("mode", "batch_console");
        var correlationId = request.Arguments.GetString("correlationId", request.RequestId);
        if (!string.Equals(mode, "batch_console", StringComparison.OrdinalIgnoreCase))
            return TestDriverProgress.Failed("mode must be batch_console");

        return BatchConsole(correlationId);
    }

    static TestDriverProgress BatchConsole(string correlationId)
    {
        string firstError = null;
        bool windowOpened = false;
        int visibleBefore = 0;
        int visibleAfterFilter = 0;
        bool filterApplied = false;
        bool filterChanged = false;
        var filterKeys = new List<string>();
        Window_BatchDirector window = null;

        try
        {
            if (PersonasMod.Settings.BatchFilters == null)
                PersonasMod.Settings.InitFilters();

            window = new Window_BatchDirector();
            Find.WindowStack.Add(window);
            windowOpened = Find.WindowStack?.WindowOfType<Window_BatchDirector>() != null;
            if (!windowOpened)
                return FailedBatch(correlationId, false, 0, 0, false, false, filterKeys, "batch console did not open");

            foreach (var key in window.CharacterTypeFilterKeys)
                filterKeys.Add(key);

            visibleBefore = window.VisiblePawnCount;
            filterApplied = window.TrySetCharacterTypeFilter("Colonists", false);
            visibleAfterFilter = window.VisiblePawnCount;
            filterChanged = visibleAfterFilter != visibleBefore || filterApplied;
        }
        catch (NullReferenceException ex)
        {
            firstError = ex.GetType().Name + ": " + ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            firstError = ex.GetType().Name + ": " + ex.Message;
        }
        catch (ArgumentException ex)
        {
            firstError = ex.GetType().Name + ": " + ex.Message;
        }
        finally
        {
            window?.Close(doCloseSound: false);
            if (PersonasMod.Settings.BatchFilters != null)
                PersonasMod.Settings.BatchFilters["Colonists"] = true;
        }

        return TestDriverProgress.Completed(new TestDriverJsonWriter()
            .Text("mode", "batch_console")
            .Text("correlationId", correlationId)
            .Flag("windowOpened", windowOpened)
            .Text("windowType", nameof(Window_BatchDirector))
            .Integer("visibleBefore", visibleBefore)
            .Integer("visibleAfterFilter", visibleAfterFilter)
            .Flag("filterApplied", filterApplied)
            .Flag("filterChanged", filterChanged)
            .Flag("hasColonistsFilter", filterKeys.Contains("Colonists"))
            .Flag("hasPrisonersFilter", filterKeys.Contains("Prisoners"))
            .Flag("hasSlavesFilter", filterKeys.Contains("Slaves"))
            .Flag("hasVisitorsFilter", filterKeys.Contains("Visitors"))
            .Flag("characterTypeFilters", filterKeys.Count >= 4)
            .TextArray("filterKeys", filterKeys)
            .Flag("paidCall", false)
            .Flag("EXCEPTION_PRESENT", firstError != null)
            .Text("firstError", firstError)
            .Flag("paused", Find.TickManager?.Paused ?? true)
            .Integer("ticksGame", Find.TickManager?.TicksGame ?? 0));
    }

    static TestDriverProgress FailedBatch(
        string correlationId,
        bool windowOpened,
        int visibleBefore,
        int visibleAfterFilter,
        bool filterApplied,
        bool filterChanged,
        List<string> filterKeys,
        string error)
    {
        return TestDriverProgress.Completed(new TestDriverJsonWriter()
            .Text("mode", "batch_console")
            .Text("correlationId", correlationId)
            .Flag("windowOpened", windowOpened)
            .Integer("visibleBefore", visibleBefore)
            .Integer("visibleAfterFilter", visibleAfterFilter)
            .Flag("filterApplied", filterApplied)
            .Flag("filterChanged", filterChanged)
            .TextArray("filterKeys", filterKeys)
            .Flag("EXCEPTION_PRESENT", true)
            .Text("firstError", error));
    }
}
