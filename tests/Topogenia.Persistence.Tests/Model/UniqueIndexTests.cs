// UniqueIndexTests.cs
// Partial unique indexes (two live rows refused, a live and a deleted one accepted) and unique codes, on both engines (spec 4.2).

using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Model;

public sealed class UniqueIndexTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    /// <summary>For each partial unique index of spec 4.2: rows that break it, and rows that look alike but are allowed.</summary>
    private static readonly Dictionary<string, (Func<ProjectGraph, object[]> Refused, Func<ProjectGraph, object[]> Accepted)> PartialCases = new()
    {
        [IndexNames.LiveStepOfCell] = (
            graph => [StepIn(graph, graph.Steps[0], 50)],
            graph => [StepIn(graph, graph.Steps[0], 50) with { DeletedAt = graph.At(20), DeletionBatchId = graph.Batch.Id }]),
        [IndexNames.LiveFlowOfPair] = (
            graph => [graph.LiveFlow with { Id = Guid.CreateVersion7() }],
            graph => [graph.LiveFlow with { Id = Guid.CreateVersion7(), DeletedAt = graph.At(20), DeletionBatchId = graph.Batch.Id }]),
        [IndexNames.LiveSubMapOfStep] = (
            graph => [graph.SubMap with { Id = Guid.CreateVersion7() }],
            graph => [graph.SubMap with { Id = Guid.CreateVersion7(), DeletedAt = graph.At(20), DeletionBatchId = graph.Batch.Id }]),
        [IndexNames.RootMapOfProject] = (
            graph => [graph.RootMap with { Id = Guid.CreateVersion7() }],
            graph => [graph.RootMap with { Id = Guid.CreateVersion7(), ParentStepId = graph.Steps[1].Id }]),
        [IndexNames.SingleApproverOfStep] = (
            graph => [new StepRaci(graph.Steps[0].Id, graph.Lanes[0].Id, RaciRole.A, graph.At(20), graph.Owner.Id)],
            graph => [new StepRaci(graph.Steps[0].Id, graph.Lanes[0].Id, RaciRole.I, graph.At(20), graph.Owner.Id)]),
        [IndexNames.OpenDissolutionOfMap] = (
            graph => [graph.Dissolution with { Id = Guid.CreateVersion7() }],
            graph => [graph.Dissolution with { Id = Guid.CreateVersion7(), ClosedAt = graph.At(20), ClosedBy = graph.Owner.Id }]),
        [IndexNames.PainPointOfUse] = (
            graph => [graph.Use with { StepId = graph.Steps[2].Id }],
            graph => [graph.Use with { StepId = graph.Steps[2].Id, PainPointId = null }, graph.Use with { StepId = graph.Steps[3].Id, PainPointId = null }]),
    };

    /// <summary>Unique over every row, deleted ones included: a code is never given twice, a login is one person (spec 4.1, 4.5).</summary>
    private static readonly Dictionary<string, Func<ProjectGraph, object[]>> UniqueCases = new()
    {
        ["UQ_project_code"] = graph => [graph.Project with { Id = Guid.CreateVersion7() }],
        ["UQ_lane_code"] = graph => [graph.Lanes[0] with { Id = Guid.CreateVersion7(), SortOrder = 9 }],
        ["UQ_step_code"] = graph => [StepIn(graph, graph.Steps[0], 50) with { Column = 60, Code = graph.DeletedStep.Code }],
        ["UQ_pain_point_code"] = graph => [graph.MapPainPoint with { Id = Guid.CreateVersion7() }],
        ["UQ_app_user_login_normalized"] = graph =>
            [graph.Reader with { Id = Guid.CreateVersion7(), Login = graph.Reader.Login.ToLowerInvariant() }],
        ["UQ_step_sheet_step_id"] = graph => [graph.Sheets[0] with { Id = Guid.CreateVersion7() }],
        ["UQ_step_sheet_version_sheet_id_version_number"] = graph =>
            [graph.Of<StepSheetVersion>()[0] with { Id = Guid.CreateVersion7() }],
        ["UQ_operation_log_map_id_sequence"] = graph => [graph.LogEntry with { Id = Guid.CreateVersion7() }],
    };

    public static TheoryData<EngineKind, string> PartialIndexes() => Cases(PartialCases.Keys);

    public static TheoryData<EngineKind, string> UniqueIndexes() => Cases(UniqueCases.Keys);

    private static TheoryData<EngineKind, string> Cases(IEnumerable<string> names)
    {
        var data = new TheoryData<EngineKind, string>();
        foreach (var kind in Enum.GetValues<EngineKind>())
        {
            foreach (var name in names.Order(StringComparer.Ordinal))
                data.Add(kind, name);
        }
        return data;
    }

    /// <summary>A new step in the cell of another step, with a new code.</summary>
    private static Step StepIn(ProjectGraph graph, Step other, int code) =>
        other with { Id = Guid.CreateVersion7(), Code = graph.ReadableCode('E', code), DeletedAt = null, DeletionBatchId = null };

    private async Task<(TestDatabase Database, ProjectGraph Graph)> WrittenAsync(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = ProjectGraph.New();
        await Refusals.SaveAsync(database, [.. graph.Records]);
        return (database, graph);
    }

    [Theory]
    [MemberData(nameof(PartialIndexes))]
    public async Task Two_live_rows_are_refused_by_the_partial_unique_index(EngineKind kind, string index)
    {
        var (database, graph) = await WrittenAsync(kind);
        await Refusals.AssertRefusedAsync(database, GuardKind.Unique, index, PartialCases[index].Refused(graph));
    }

    [Theory]
    [MemberData(nameof(PartialIndexes))]
    public async Task A_row_outside_the_filter_of_the_partial_unique_index_is_accepted(EngineKind kind, string index)
    {
        var (database, graph) = await WrittenAsync(kind);
        await Refusals.SaveAsync(database, PartialCases[index].Accepted(graph));
    }

    [Theory]
    [MemberData(nameof(UniqueIndexes))]
    public async Task A_value_unique_over_every_row_is_refused_twice(EngineKind kind, string index)
    {
        var (database, graph) = await WrittenAsync(kind);
        await Refusals.AssertRefusedAsync(database, GuardKind.Unique, index, UniqueCases[index](graph));
    }

    [Fact]
    public void Every_partial_unique_index_of_the_model_has_its_cases() =>
        Assert.Equal(ModelGuards.PartialIndexes.Select(guard => guard.IndexName).Order(StringComparer.Ordinal), PartialCases.Keys.Order(StringComparer.Ordinal));

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_login_keeps_its_case_and_its_normalised_form_is_the_unique_one(EngineKind kind)
    {
        var (database, graph) = await WrittenAsync(kind);
        var mixed = graph.Reader with { Id = Guid.CreateVersion7(), Login = @"Fictitious\MiXeD" + graph.Number, LoginNormalized = Login.Normalize(@"Fictitious\MiXeD" + graph.Number) };
        await Refusals.SaveAsync(database, mixed);
        await using var context = database.Context();
        var read = context.Set<AppUser>().Single(user => user.Id == mixed.Id);
        Assert.Equal(@"Fictitious\MiXeD" + graph.Number, read.Login);
        Assert.Equal(@"FICTITIOUS\MIXED" + graph.Number, read.LoginNormalized);
        await Refusals.AssertRefusedAsync(database, GuardKind.Unique, "UQ_app_user_login_normalized",
            mixed with { Id = Guid.CreateVersion7(), Login = @"FICTITIOUS\mixed" + graph.Number });
    }
}
