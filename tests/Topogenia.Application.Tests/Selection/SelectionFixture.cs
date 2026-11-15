// SelectionFixture.cs
// Helpers of the selection, sub-map, dissolution and RACI handler tests: flows, lanes, live records, undo and redo.

using Topogenia.Application.Commands;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Selection;

internal static class SelectionFixture
{
    public static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Adds a live flow between two steps of a map (the root map by default) and returns its identifier.</summary>
    public static Guid Flow(TestProject project, Guid source, Guid target, Guid? mapId = null)
    {
        var id = project.NextId();
        project.Database.Add(new Flow(id, mapId ?? project.RootMapId, source, target, null, TestProject.Now, project.Facilitator.Id,
            null, null, null, null, 1));
        return id;
    }

    /// <summary>Adds a live lane under the others on a map (the root map by default) and returns its identifier.</summary>
    public static Guid AddLane(TestProject project, string name, int sortOrder, Guid? mapId = null)
    {
        var id = project.NextId();
        project.Database.Add(new Lane(id, mapId ?? project.RootMapId, $"PRJ-C{sortOrder + 1:D2}", name, "fr", LaneType.Person, null, "#E8F5E9",
            sortOrder, null, TestProject.Now, project.Facilitator.Id, null, null, null, null, 1));
        return id;
    }

    /// <summary>Changes the type of a step of the database (start, end... for the steps without content).</summary>
    public static void Retype(TestProject project, Guid stepId, StepType type) =>
        project.Database.Add(StepOf(project, stepId) with { StepType = type });

    public static Step StepOf(TestProject project, Guid stepId) => project.Database.Get<Step>(Step.KeyOf(stepId));

    public static ProcessMap MapOf(TestProject project, Guid mapId) => project.Database.Get<ProcessMap>(ProcessMap.KeyOf(mapId));

    public static bool IsLiveStep(TestProject project, Guid stepId) =>
        project.Database.All<Step>().Any(step => step.Id == stepId && step.DeletedAt is null);

    public static bool IsLiveMap(TestProject project, Guid mapId) =>
        project.Database.All<ProcessMap>().Any(map => map.Id == mapId && map.DeletedAt is null);

    /// <summary>The live flows of a map as (source, target) pairs, sorted.</summary>
    public static List<(Guid Source, Guid Target)> LiveFlows(TestProject project, Guid? mapId = null) =>
        project.Database.All<Flow>().Where(flow => flow.DeletedAt is null && flow.MapId == (mapId ?? project.RootMapId))
            .Select(flow => (flow.SourceStepId, flow.TargetStepId)).Order().ToList();

    /// <summary>The live steps of a map, in column order.</summary>
    public static List<Guid> LiveSteps(TestProject project, Guid? mapId = null) =>
        project.Database.All<Step>().Where(step => step.DeletedAt is null && step.MapId == (mapId ?? project.RootMapId))
            .OrderBy(step => step.Column).ThenBy(step => step.Row).Select(step => step.Id).ToList();

    /// <summary>The kinds logged on a map, oldest first.</summary>
    public static List<string> Kinds(TestProject project, Guid? mapId = null) =>
        project.Database.Entries.Where(entry => entry.MapId == (mapId ?? project.RootMapId)).OrderBy(entry => entry.Sequence)
            .Select(entry => entry.Kind).ToList();

    public static Task<CommandOutcome<JournalResult>> UndoAsync(TestProject project, Guid? mapId = null) =>
        project.Runner.RunAsync(new UndoCommand(mapId ?? project.RootMapId), Token);

    public static Task<CommandOutcome<JournalResult>> RedoAsync(TestProject project, Guid? mapId = null) =>
        project.Runner.RunAsync(new RedoCommand(mapId ?? project.RootMapId), Token);
}
