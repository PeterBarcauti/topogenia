// ChunkTests.cs
// Lists of identifiers cut by 500: a change set of more than 2 100 records read back and written on both engines (spec 4.3-2, A-5-06).

using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Writing;

/// <summary>
/// SQL Server takes at most 2 100 parameters in a statement (spec 4.3-2): the rows of the records without version that a
/// change set modifies are read back by packets of 500 identifiers. A change set that restores 2 201 deletion batches
/// is checked and written on both engines.
/// </summary>
public sealed class ChunkTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private const int Batches = 2_201;

    [Fact]
    public void A_list_is_cut_into_packets_of_500_distinct_values_in_their_order()
    {
        var values = Enumerable.Range(0, 1_201).Concat([0, 1]).ToList();

        var chunks = Chunks.Of(values).ToList();

        Assert.Equal(new[] { 500, 500, 201 }, chunks.Select(chunk => chunk.Count));
        Assert.Equal(Enumerable.Range(0, 1_201), chunks.SelectMany(chunk => chunk));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_change_set_of_more_than_2100_records_without_version_is_checked_and_written(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        var deletedAt = FixedClock.Time.AddDays(-1);
        await scenario.WriteAsync(new ChangeSet(Enumerable.Range(0, Batches)
            .Select(_ => StoreScenario.Created(new DeletionBatch(Guid.CreateVersion7(), scenario.ProjectId, null, deletedAt, scenario.Actor.Id, null, null)))
            .ToList()));

        var outcome = await scenario.RunAsync((workspace, context) =>
        {
            foreach (var batch in workspace.All<DeletionBatch>().ToList())
                workspace.Put(batch with { RestoredAt = context.Now, RestoredBy = context.ActorId });
        });

        Assert.Equal(Batches, outcome.Changes.Modified<DeletionBatch>().Count());
        var stored = await scenario.AssertStoredAsync(outcome);
        Assert.All(stored.All<DeletionBatch>(), batch => Assert.NotNull(batch.RestoredAt));
    }
}
