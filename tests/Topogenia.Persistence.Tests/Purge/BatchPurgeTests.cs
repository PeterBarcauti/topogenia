// BatchPurgeTests.cs
// The expired batches of a live project purged with what they deleted and the log entries that refer to them; the open frame waits (spec 4.5, 4.6).

using Topogenia.Domain.Dissolutions;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Flows;
using Topogenia.Domain.Model;
using Topogenia.Domain.Selection;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Structure;
using Topogenia.Domain.SubMaps;
using Topogenia.Domain.Trash;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Purge;

/// <summary>
/// A live project, built by the operations of the domain: a step deleted in a batch, and a detail dissolved into the map,
/// whose open frame holds the batch of the dissolution. Past the retention, the daily plan purges the first batch only;
/// the persistence deletes it with the step and the flow it holds and with the log entries of its operation and of the
/// redo of it (spec 4.6), and leaves the frame, its batch, the other entries and another project as they were.
/// </summary>
public sealed class BatchPurgeTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static OperationLogEntry Entry(StoreScenario scenario, Guid mapId, Guid id, int sequence, Guid? redoneFrom = null) =>
        new(id, scenario.ProjectId, mapId, scenario.Actor.Id, "step.delete", sequence, """{"forward":[]}""", """{"inverse":[]}""", """{"touched":[]}""",
            FixedClock.Time.AddSeconds(sequence), null, redoneFrom);

    /// <summary>Four steps linked in a chain; the first deleted with its flow; the third detailed then dissolved (open frame).</summary>
    private static async Task<(DeletionBatch Deleted, DeletionBatch Frame, Guid[] Steps)> BuildAsync(StoreScenario scenario)
    {
        var steps = new Guid[4];
        await scenario.RunAsync((workspace, context) =>
        {
            var map = workspace.RootMap.Id;
            var lane = workspace.LanesOf(map)[0].Id;
            for (var i = 0; i < steps.Length; i++)
                steps[i] = StepOperations.Create(workspace, context, new CreateStepRequest(map, lane, i, $"Step {i}")).Id;
            for (var i = 0; i + 1 < steps.Length; i++)
                FlowRules.Create(workspace, context, new CreateFlowRequest(map, steps[i], steps[i + 1], null));
        });
        var deletion = await scenario.RunAsync((workspace, context) =>
            GroupDeletion.Delete(workspace, context, workspace.RootMap.Id, new GroupDeletionRequest([steps[0]], null)));
        await scenario.RunAsync((workspace, context) =>
        {
            var detail = SubMapRules.Detail(workspace, context, steps[2]);
            StepOperations.Create(workspace, context, new CreateStepRequest(detail.MapId, detail.FirstLaneId!.Value, 0, "Prepare"));
        });
        var dissolution = await scenario.RunAsync((workspace, context) => Dissolve.Run(workspace, context, steps[2]));
        return (deletion.Changes.Created<DeletionBatch>().Single(), dissolution.Changes.Created<DeletionBatch>().Single(), steps);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_expired_batch_goes_with_its_objects_and_its_log_entries_while_the_open_frame_and_another_project_stay(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var scenario = await StoreScenario.CreateAsync(database);
        var other = await StoreScenario.CreateAsync(database);
        var (deleted, frame, steps) = await BuildAsync(scenario);
        await BuildAsync(other);
        var map = (await scenario.LoadAsync()).RootMap.Id;
        var operation = deleted.OperationId!.Value;
        var kept = new[] { Entry(scenario, map, Guid.CreateVersion7(), 3), Entry(scenario, map, frame.OperationId!.Value, 4) };
        await scenario.WriteAsync(new ChangeSet([.. new[] { Entry(scenario, map, operation, 1), Entry(scenario, map, Guid.CreateVersion7(), 2, operation) }
            .Concat(kept).Select(entry => StoreScenario.Created(entry))]));
        var before = await scenario.LoadAsync();
        var otherBefore = await other.LoadAsync();
        var plan = PurgePlanner.Expired(before, PurgeBench.AfterRetention(FixedClock.Time.AddDays(1)), AppSettings.Default);

        var outcome = await PurgeBench.PurgeAsync(database, scenario.ProjectId, plan, 4);

        Assert.Equal([deleted.Id], plan.PurgedBatchIds);
        Assert.Equal(2, outcome.LogEntries);
        var after = await scenario.LoadAsync();
        var purged = plan.Records.Select(record => record.Key).ToHashSet();
        var expected = WorkspaceRecords.Of(before).Where(pair => !purged.Contains(pair.Key) && pair.Value is not Project).ToDictionary();
        var present = WorkspaceRecords.Of(after).Where(pair => pair.Value is not Project).ToDictionary();
        Assert.Equal(WorkspaceRecords.Sorted(expected.Keys), WorkspaceRecords.Sorted(present.Keys));
        Assert.All(expected, pair => Assert.Equal(pair.Value, present[pair.Key]));
        Assert.Null(after.Step(steps[0]));
        Assert.Contains(plan.Records, record => record is Flow { DeletionBatchId: { } batch } && batch == deleted.Id);
        Assert.NotNull(after.Step(steps[2])!.DeletedAt);
        Assert.True(Assert.Single(after.All<Dissolution>()).IsOpen);
        Assert.Equal(frame, Assert.Single(after.All<DeletionBatch>()));
        foreach (var entry in kept)
            Assert.True(await PurgeBench.ExistsAsync(database, entry));
        Assert.Equal(kept.Length, await PurgeBench.LogEntriesAsync(database, scenario.ProjectId));
        WorkspaceRecords.AssertSame(otherBefore, await other.LoadAsync());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_batch_restored_by_an_undo_after_its_plan_is_not_purged(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var scenario = await StoreScenario.CreateAsync(database);
        var steps = new Guid[2];
        await scenario.RunAsync((workspace, context) =>
        {
            var map = workspace.RootMap.Id;
            for (var i = 0; i < steps.Length; i++)
                steps[i] = StepOperations.Create(workspace, context, new CreateStepRequest(map, workspace.LanesOf(map)[0].Id, i, $"Step {i}")).Id;
        });
        var deletion = await scenario.RunAsync((workspace, context) =>
            GroupDeletion.Delete(workspace, context, workspace.RootMap.Id, new GroupDeletionRequest(steps, null)));
        var plan = PurgePlanner.Expired(await scenario.LoadAsync(), PurgeBench.AfterRetention(FixedClock.Time.AddDays(1)), AppSettings.Default);
        await scenario.RunAsync((workspace, context) => UndoReplay.Undo(workspace, context, deletion.Changes));
        var restored = await scenario.LoadAsync();

        var refusal = await Assert.ThrowsAsync<DomainException>(() => PurgeBench.PurgeAsync(database, scenario.ProjectId, plan, 100));

        Assert.Equal(ErrorCodes.VersionConflict, refusal.Code);
        Assert.Equal(DeletionBatch.KeyOf(deletion.Changes.Created<DeletionBatch>().Single().Id).Type, refusal.Details["object"]);
        WorkspaceRecords.AssertSame(restored, await scenario.LoadAsync());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_batch_of_a_frame_is_purged_once_the_frame_is_finished_and_what_was_arranged_stays(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var scenario = await StoreScenario.CreateAsync(database);
        var (deleted, frame, steps) = await BuildAsync(scenario);
        var dissolution = (await scenario.LoadAsync()).All<Dissolution>().Single().Id;
        await scenario.RunAsync((workspace, context) => Remainder.Finish(workspace, context, dissolution));
        var before = await scenario.LoadAsync();
        var plan = PurgePlanner.Expired(before, PurgeBench.AfterRetention(FixedClock.Time.AddDays(1)), AppSettings.Default);

        await PurgeBench.PurgeAsync(database, scenario.ProjectId, plan, 100);

        Assert.Equal(new[] { deleted.Id, frame.Id }.Order(), plan.PurgedBatchIds.Order());
        var after = await scenario.LoadAsync();
        Assert.Empty(after.All<DeletionBatch>());
        Assert.Empty(after.All<Dissolution>());
        Assert.Null(after.Step(steps[2]));
        var map = before.RootMap.Id;
        Assert.Equal(before.StepsOf(map).OrderBy(step => step.Id), after.StepsOf(map).OrderBy(step => step.Id));
        Assert.Equal(before.FlowsOf(map).OrderBy(flow => flow.Id), after.FlowsOf(map).OrderBy(flow => flow.Id));
    }
}
