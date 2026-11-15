// StepsFixture.cs
// Helpers of the step, flow and layout command tests: flows and lanes added to the test project, live records, undo and redo.

using Topogenia.Application.OperationLog;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Steps;

internal static class StepsFixture
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Adds a live flow between two steps of the root map and returns its identifier.</summary>
    public static Guid Flow(this TestProject project, Guid source, Guid target, string? label = null)
    {
        var id = project.NextId();
        project.Database.Add(new Flow(id, project.RootMapId, source, target, label, TestProject.Now, project.Facilitator.Id,
            null, null, null, null, 1));
        return id;
    }

    /// <summary>Adds a second lane under the first one of the root map and returns its identifier.</summary>
    public static Guid SecondLane(this TestProject project)
    {
        var id = project.NextId();
        project.Database.Add(new Lane(id, project.RootMapId, "PRJ-C02", "Acteur 2", "fr", LaneType.Person, null, "#E8F5E9", 1, null,
            TestProject.Now, project.Facilitator.Id, null, null, null, null, 1));
        return id;
    }

    public static Step StepOf(this TestProject project, Guid id) => project.Database.Get<Step>(Step.KeyOf(id));

    public static Flow FlowOf(this TestProject project, Guid id) => project.Database.Get<Flow>(Domain.Model.Flow.KeyOf(id));

    public static bool IsLiveStep(this TestProject project, Guid id) =>
        project.Database.All<Step>().SingleOrDefault(step => step.Id == id) is { DeletedAt: null };

    public static bool IsLiveFlow(this TestProject project, Guid id) =>
        project.Database.All<Flow>().SingleOrDefault(flow => flow.Id == id) is { DeletedAt: null };

    /// <summary>The live flows of the root map as source and target pairs, with their labels.</summary>
    public static List<(Guid Source, Guid Target, string? Label)> LiveFlows(this TestProject project) =>
        project.Database.All<Flow>().Where(flow => flow.DeletedAt is null && flow.MapId == project.RootMapId)
            .Select(flow => (flow.SourceStepId, flow.TargetStepId, flow.Label)).OrderBy(pair => pair.SourceStepId).ThenBy(pair => pair.TargetStepId)
            .ToList();

    /// <summary>The cell of a step as lane, column and row.</summary>
    public static (Guid Lane, int Column, int Row) CellOf(this TestProject project, Guid id) =>
        project.StepOf(id) is var step ? (step.LaneId, step.Column, step.Row) : default;

    public static string LastKind(this TestProject project) => project.Database.Entries[^1].Kind;

    public static async Task UndoAsync(this TestProject project, Guid? mapId = null) =>
        await project.Runner.RunAsync(new UndoCommand(mapId ?? project.RootMapId), Token);

    public static async Task RedoAsync(this TestProject project, Guid? mapId = null) =>
        await project.Runner.RunAsync(new RedoCommand(mapId ?? project.RootMapId), Token);
}
