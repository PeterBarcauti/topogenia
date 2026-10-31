// TrashPurgeTests.cs
// The purge of a trashed project and of the expired batches of a live one, in short transactions, on both engines (annex A 2.15, spec 4.6).

using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Trash;
using Topogenia.Persistence.Purge;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Purge;

/// <summary>
/// The plan is the domain's (`PurgePlanner`, proved by the tests of the domain); these tests prove that the persistence
/// deletes it whole, in its order, in transactions of the given size, with the entries of the operation log that refer
/// to it, without touching another project, the users or the audit, and that it stops when the project or a batch changed
/// since the plan. Each test writes its own complete project (<see cref="ProjectGraph"/>) in the database of the class.
/// </summary>
public sealed class TrashPurgeTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private const int Size = 25;

    private static async Task AssertNothingLeftAsync(TestDatabase database, ProjectGraph graph)
    {
        Assert.Null(await PurgeBench.LoadAsync(database, graph.Project.Id));
        Assert.Equal(0, await PurgeBench.LogEntriesAsync(database, graph.Project.Id));
        foreach (var record in graph.Records)
            Assert.Equal(PurgeBench.Survivors.Contains(record.GetType()), await PurgeBench.ExistsAsync(database, record));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_trashed_project_past_the_retention_is_purged_whole_in_short_transactions_and_nothing_else(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var (purged, other) = (await PurgeBench.GraphAsync(database), await PurgeBench.GraphAsync(database));
        var trashedAt = purged.At(100);
        await PurgeBench.TrashAsync(database, purged, trashedAt);
        var untouched = (await PurgeBench.LoadAsync(database, other.Project.Id))!;
        var plan = await PurgeBench.PlanAsync(database, purged.Project.Id, PurgeBench.AfterRetention(trashedAt));

        var outcome = await PurgeBench.PurgeAsync(database, purged.Project.Id, plan, Size);

        Assert.True(plan.PurgesProject);
        Assert.Equal(new PurgeOutcome(plan.Records.Count, 2, 1 + ((plan.Records.Count + Size - 1) / Size)), outcome);
        await AssertNothingLeftAsync(database, purged);
        WorkspaceRecords.AssertSame(untouched, (await PurgeBench.LoadAsync(database, other.Project.Id))!);
        Assert.Equal(2, await PurgeBench.LogEntriesAsync(database, other.Project.Id));
        // The code was reserved until the purge (spec 4.5): a new project takes it now.
        await Refusals.SaveAsync(database, purged.Project with { Id = Guid.CreateVersion7(), DeletedAt = null, DeletedBy = null });
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_purge_stopped_midway_leaves_a_consistent_project_that_a_new_plan_finishes(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = await PurgeBench.GraphAsync(database);
        var trashedAt = graph.At(100);
        await PurgeBench.TrashAsync(database, graph, trashedAt);
        var now = PurgeBench.AfterRetention(trashedAt);
        var plan = await PurgeBench.PlanAsync(database, graph.Project.Id, now);

        await Assert.ThrowsAsync<SimulatedEngineFailure>(() => PurgeBench.PurgeAsync(database, graph.Project.Id, plan, 10, new FailingCommit(3)));

        var partial = await PurgeBench.LoadAsync(database, graph.Project.Id);
        Assert.NotNull(partial);
        Assert.Equal(0, await PurgeBench.LogEntriesAsync(database, graph.Project.Id));
        var rest = PurgePlanner.Expired(partial, now, AppSettings.Default);
        Assert.Equal(plan.Records.Count - 10, rest.Records.Count);
        await PurgeBench.PurgeAsync(database, graph.Project.Id, rest, Size);
        await AssertNothingLeftAsync(database, graph);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_project_restored_from_the_trash_after_its_plan_is_not_purged(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = await PurgeBench.GraphAsync(database);
        var trashedAt = graph.At(100);
        await PurgeBench.TrashAsync(database, graph, trashedAt);
        var plan = await PurgeBench.PlanAsync(database, graph.Project.Id, PurgeBench.AfterRetention(trashedAt));
        await PurgeBench.RunAsync(database, graph.Project.Id, PurgeBench.At(graph.Owner.Id, graph.At(200)), workspace => TrashRules.Restore(workspace));
        var restored = (await PurgeBench.LoadAsync(database, graph.Project.Id))!;

        var refusal = await Assert.ThrowsAsync<DomainException>(() => PurgeBench.PurgeAsync(database, graph.Project.Id, plan, Size));

        Assert.Equal(ErrorCodes.VersionConflict, refusal.Code);
        WorkspaceRecords.AssertSame(restored, (await PurgeBench.LoadAsync(database, graph.Project.Id))!);
        Assert.Equal(2, await PurgeBench.LogEntriesAsync(database, graph.Project.Id));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_projects_to_purge_are_those_trashed_or_holding_a_batch_before_the_limit(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graphs = new List<ProjectGraph>();
        for (var i = 0; i < 3; i++)
            graphs.Add(await PurgeBench.GraphAsync(database));
        // Each graph holds two batches deleted at its minutes 5 and 6, and the graphs follow each other by a minute at least:
        // the limit, just after the first batch of the first graph, leaves every other batch after it.
        var limit = graphs[0].Batch.DeletedAt.AddTicks(1);
        await PurgeBench.TrashAsync(database, graphs[1], graphs[0].At(1));
        await PurgeBench.TrashAsync(database, graphs[2], graphs[2].At(100));
        var expected = graphs.Take(2).Select(graph => graph.Project.Id).Order().ToList();

        await using var session = database.Session();
        var candidates = await new TrashPurge(session).ProjectsToPurgeAsync(limit, TestContext.Current.CancellationToken);

        Assert.Equal(expected, candidates.Where(id => graphs.Any(graph => graph.Project.Id == id)));
        Assert.Equal(candidates.Distinct().Order(), candidates);
        await Assert.ThrowsAsync<ArgumentException>(() => new TrashPurge(session).ProjectsToPurgeAsync(DateTime.SpecifyKind(limit, DateTimeKind.Unspecified), TestContext.Current.CancellationToken));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_project_trashed_or_a_batch_deleted_exactly_at_the_limit_is_kept_and_one_tick_before_it_is_listed(EngineKind kind)
    {
        // The 1.x compares strictly (`deleted_at < @limite`, `006_corbeille.sql`): the limit itself is still kept.
        var database = await databases.OfAsync(kind);
        var (trashed, live) = (await PurgeBench.GraphAsync(database), await PurgeBench.GraphAsync(database));
        // Trashed at its minute 1, before its own batches (minutes 5 and 6): the trash alone decides at that limit.
        var trashedAt = trashed.At(1);
        await PurgeBench.TrashAsync(database, trashed, trashedAt);
        await using var session = database.Session();
        var purge = new TrashPurge(session);

        async Task<bool> ListedAsync(ProjectGraph graph, DateTime limit) =>
            (await purge.ProjectsToPurgeAsync(limit, TestContext.Current.CancellationToken)).Contains(graph.Project.Id);

        Assert.False(await ListedAsync(trashed, trashedAt));
        Assert.True(await ListedAsync(trashed, trashedAt.AddTicks(1)));
        Assert.False(await ListedAsync(live, live.Batch.DeletedAt));
        Assert.True(await ListedAsync(live, live.Batch.DeletedAt.AddTicks(1)));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_plan_of_another_project_or_a_whole_purge_of_a_live_project_is_rejected_before_any_command(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = await PurgeBench.GraphAsync(database);
        var other = await PurgeBench.GraphAsync(database);
        var plan = await PurgeBench.PlanAsync(database, graph.Project.Id, PurgeBench.AfterRetention(graph.At(10)));
        var forged = new PurgePlan(true, plan.PurgedBatchIds, [.. plan.Records, graph.Project]);
        var log = new CommandLog();

        await Assert.ThrowsAsync<ArgumentException>(() => PurgeBench.PurgeAsync(database, other.Project.Id, plan, Size, log));
        await Assert.ThrowsAsync<ArgumentException>(() => PurgeBench.PurgeAsync(database, graph.Project.Id, forged, Size, log));

        Assert.False(plan.PurgesProject);
        Assert.NotEmpty(plan.PurgedBatchIds);
        Assert.Empty(log.Commands);
    }
}

