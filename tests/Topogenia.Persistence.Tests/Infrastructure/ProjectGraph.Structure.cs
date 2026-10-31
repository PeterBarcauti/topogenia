// ProjectGraph.Structure.cs
// Structure of the fictitious project: maps, lanes, steps, flows, RACI roles and a dissolution with its frame.

using Topogenia.Domain.Dissolutions;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;

namespace Topogenia.Persistence.Tests.Infrastructure;

public sealed partial class ProjectGraph
{
    public ProcessMap RootMap { get; private set; } = null!;

    public ProcessMap SubMap { get; private set; } = null!;

    /// <summary>The three lanes of the root map (person, department, system).</summary>
    public IReadOnlyList<Lane> Lanes { get; private set; } = [];

    public Lane SubLane { get; private set; } = null!;

    /// <summary>The seven live steps of the root map, one per type, in column order.</summary>
    public IReadOnlyList<Step> Steps { get; private set; } = [];

    public Step SubStep { get; private set; } = null!;

    /// <summary>A step deleted logically in <see cref="Batch"/> (the detailed step of the dissolution).</summary>
    public Step DeletedStep { get; private set; } = null!;

    public Flow LiveFlow { get; private set; } = null!;

    public Flow DeletedFlow { get; private set; } = null!;

    public Dissolution Dissolution { get; private set; } = null!;

    private void Structure()
    {
        RootMap = Add(new ProcessMap(Guid.CreateVersion7(), Project.Id, null, Project.Name, "fr", At(2), Owner.Id, At(3), Owner.Id, null, null, 2));
        var types = Enum.GetValues<LaneType>();
        Lanes = types.Select((type, i) => Add(new Lane(Guid.CreateVersion7(), RootMap.Id, ReadableCode('C', i + 1), $"Lane {i + 1}", "fr", type,
            type == LaneType.Person ? Contributor.Id : null, LaneColors.ForIndex(i), i, i == 2 ? Guid.CreateVersion7() : null,
            At(3), Owner.Id, null, null, null, null, 1) { Headcount = valueStream && type == LaneType.Department ? 3 : null })).ToList();
        var stepTypes = Enum.GetValues<StepType>();
        var statuses = Enum.GetValues<AutomationStatus>();
        Steps = stepTypes.Select((type, i) => Add(new Step(Guid.CreateVersion7(), RootMap.Id, Lanes[i % Lanes.Count].Id, ReadableCode('E', i + 1),
            $"Step {i + 1}", "fr", type, i, i % 2, i == 0 ? Contributor.Id : null, statuses[i % statuses.Length],
            i == 1 ? Guid.CreateVersion7() : null, At(4), Owner.Id, i == 0 ? At(5) : null, i == 0 ? Contributor.Id : null, null, null, i + 1)
        {
            VolumeQuantity = valueStream && type == StepType.Start ? 120.5m : null,
            VolumePeriod = valueStream && type == StepType.Start ? VolumePeriod.Month : null,
        })).ToList();
        SubMap = Add(new ProcessMap(Guid.CreateVersion7(), Project.Id, Steps[0].Id, Steps[0].Label, "en", At(4), Owner.Id, null, null, null, null, 1));
        SubLane = Add(new Lane(Guid.CreateVersion7(), SubMap.Id, ReadableCode('C', 9), "Detail lane", "en", LaneType.Department, null,
            LaneColors.ForIndex(3), 0, Lanes[1].Id, At(4), Owner.Id, null, null, null, null, 1));
        SubStep = Add(new Step(Guid.CreateVersion7(), SubMap.Id, SubLane.Id, ReadableCode('E', 9), "Detail step", "en", StepType.Task, 0, 0,
            null, AutomationStatus.Manual, null, At(4), Owner.Id, null, null, null, null, 1));
        DeletedStep = Add(new Step(Guid.CreateVersion7(), RootMap.Id, Lanes[0].Id, ReadableCode('E', 8), "Deleted step", "fr", StepType.Task, 0, 0,
            null, AutomationStatus.Manual, null, At(4), Owner.Id, At(5), Owner.Id, At(5), Batch.Id, 2));
        LiveFlow = Add(new Flow(Guid.CreateVersion7(), RootMap.Id, Steps[0].Id, Steps[1].Id, "yes", At(5), Owner.Id, At(6), Contributor.Id, null, null, 2));
        Add(new Flow(Guid.CreateVersion7(), RootMap.Id, Steps[1].Id, Steps[2].Id, null, At(5), Owner.Id, null, null, null, null, 1) { SharePercent = valueStream ? 40.25m : null });
        DeletedFlow = Add(new Flow(Guid.CreateVersion7(), RootMap.Id, Steps[0].Id, Steps[1].Id, "no", At(5), Owner.Id, null, null, At(6), Batch.Id, 1));
        Raci();
    }

    private void Raci()
    {
        Add(new StepRaci(Steps[0].Id, Lanes[1].Id, RaciRole.A, At(6), Owner.Id));
        Add(new StepRaci(Steps[0].Id, Lanes[2].Id, RaciRole.C, At(6), Owner.Id) { Version = 3, UpdatedAt = At(7), UpdatedBy = Contributor.Id });
        Add(new StepRaci(Steps[1].Id, Lanes[2].Id, RaciRole.I, At(6), Owner.Id));
        Add(new StepRaci(Steps[2].Id, Lanes[0].Id, RaciRole.S, At(6), Owner.Id));
    }

    private void Dissolutions()
    {
        Dissolution = Add(new Dissolution(Guid.CreateVersion7(), RootMap.Id, DeletedStep.Id, Batch.Id, At(7), Owner.Id, null, null)
        {
            Version = 2,
            UpdatedAt = At(8),
            UpdatedBy = Owner.Id,
        });
        Add(new Dissolution(Guid.CreateVersion7(), SubMap.Id, DeletedStep.Id, RestoredBatch.Id, At(7), Owner.Id, At(8), Contributor.Id));
        Add(new DissolutionStep(Dissolution.Id, SubStep.Id, SubLane.Id, 4, 1));
        Add(new DissolutionObject(Dissolution.Id, DissolutionObjectTypes.Flow, LiveFlow.Id));
        Add(new DissolutionObject(Dissolution.Id, DissolutionObjectTypes.Lane, Lanes[2].Id));
    }
}
