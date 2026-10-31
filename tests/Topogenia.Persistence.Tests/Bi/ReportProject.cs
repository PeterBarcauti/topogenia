// ReportProject.cs
// The fictitious project of the report views: the complete graph plus the deleted and hidden objects that the views must leave out.

using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Bi;

/// <summary>
/// A <see cref="ProjectGraph"/> (which already holds a deleted step, a deleted flow, steps of every type and a detailed
/// map) with what a report must not show besides: a deleted lane holding a role, a role on the lane of its own step, a
/// role on a lane of another map, a document of the deleted step used by a live step, and a pain point of the deleted
/// step. The project can be put in the trash, which hides all of it.
/// </summary>
internal sealed class ReportProject
{
    private ReportProject(ProjectGraph graph, IReadOnlyList<object> records)
    {
        Graph = graph;
        Records = records;
    }

    public ProjectGraph Graph { get; }

    /// <summary>Every record to write, the graph and the additions.</summary>
    public IReadOnlyList<object> Records { get; }

    public Guid ProjectId => Graph.Project.Id;

    public static ReportProject New(bool inTrash = false)
    {
        var graph = ProjectGraph.New();
        var project = inTrash ? graph.Project with { DeletedAt = graph.At(30), DeletedBy = graph.Owner.Id } : graph.Project;
        var records = graph.Records.Select(record => ReferenceEquals(record, graph.Project) ? project : record).Concat(Hidden(graph)).ToList();
        return new ReportProject(graph, records);
    }

    /// <summary>Every identifier of the records of the project.</summary>
    public IReadOnlySet<Guid> Identifiers => Records.SelectMany(record => record.GetType().GetProperties()
            .Where(property => property.Name == "Id" && property.PropertyType == typeof(Guid))
            .Select(property => (Guid)property.GetValue(record)!))
        .ToHashSet();

    private static IEnumerable<object> Hidden(ProjectGraph graph)
    {
        var steps = graph.Steps;
        var deletedLane = new Lane(Guid.CreateVersion7(), graph.RootMap.Id, graph.ReadableCode('C', 7), "Deleted lane", "fr", LaneType.Department, null,
            LaneColors.ForIndex(4), 3, null, graph.At(3), graph.Owner.Id, null, null, graph.At(5), graph.Batch.Id, 1);
        var oldOutput = new Document(Guid.CreateVersion7(), graph.DeletedStep.Id, DocumentDirection.Output, "Output of a deleted step", null, null, null,
            false, "fr", graph.At(4), graph.Owner.Id, null, null, 1);
        return
        [
            deletedLane,
            new StepRaci(steps[0].Id, deletedLane.Id, RaciRole.I, graph.At(6), graph.Owner.Id),
            new StepRaci(steps[0].Id, graph.SubLane.Id, RaciRole.S, graph.At(6), graph.Owner.Id),
            new StepRaci(steps[1].Id, steps[1].LaneId, RaciRole.C, graph.At(6), graph.Owner.Id),
            oldOutput,
            new DocumentUse(oldOutput.Id, steps[0].Id, 4m, 50, null, graph.At(11), graph.Owner.Id, null, null),
            new PainPoint(Guid.CreateVersion7(), graph.Project.Id, graph.DeletedStep.Id, graph.ReadableCode('I', 3), "Raised on a deleted step", "fr", 1m,
                PainPointStatus.Reported, graph.At(10), graph.Owner.Id, null, null, 1),
        ];
    }
}
