// LaneOrderTests.cs
// The renumbering of the order of the lanes is written alike on both engines, without a query per lane (annex A 5, D02).

using Topogenia.Domain.Lanes;
using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Queries;

/// <summary>
/// The 1.x renumbered the lanes of a map with an updatable common table expression of SQL Server (`StructureSql.cs`,
/// `WITH numerotes ... UPDATE numerotes`). In 2.0 the domain renumbers in memory (`LaneOperations.ApplyOrder`, and the
/// renumbering after a deletion) and the change set writes the lanes whose order changed: no SQL of an engine, the same
/// rows on both. The order has no unique index, so no parking is needed (`docs/modele-donnees.md` § 6.7).
/// </summary>
public sealed class LaneOrderTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private async Task<(StoreScenario Scenario, List<Guid> Lanes)> LanesAsync(EngineKind kind, int count)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        var lanes = new List<Guid>();
        await scenario.RunAsync((workspace, context) =>
        {
            var map = workspace.RootMap.Id;
            lanes.Add(workspace.LanesOf(map)[0].Id);
            for (var i = 1; i < count; i++)
                lanes.Add(LaneOperations.Add(workspace, context, new AddLaneRequest(map, $"Lane {i}")).Id);
        });
        return (scenario, lanes);
    }

    private static List<(Guid Lane, int Order)> Stored(ProjectWorkspace workspace) =>
        workspace.LanesOf(workspace.RootMap.Id).Select(lane => (lane.Id, lane.SortOrder)).ToList();

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Lanes_reordered_then_one_deleted_are_numbered_from_zero_in_their_new_order(EngineKind kind)
    {
        var (scenario, lanes) = await LanesAsync(kind, 5);

        var reordered = await scenario.RunAsync((workspace, _) => LaneOperations.Reorder(workspace, workspace.RootMap.Id, [.. Enumerable.Reverse(lanes)]));
        var afterOrder = await scenario.AssertStoredAsync(reordered);
        Assert.Equal(Enumerable.Reverse(lanes).Select((lane, i) => (lane, i)), Stored(afterOrder));

        var deleted = await scenario.RunAsync((workspace, context) => LaneOperations.Delete(workspace, context, new DeleteLaneRequest(lanes[2])));
        var afterDeletion = await scenario.AssertStoredAsync(deleted);
        var expected = Enumerable.Reverse(lanes).Where(lane => lane != lanes[2]).Select((lane, i) => (lane, i));
        Assert.Equal(expected, Stored(afterDeletion));
        Assert.NotNull(afterDeletion.Lane(lanes[2])!.DeletedAt);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Renumbering_two_or_thirty_lanes_reads_the_same_and_writes_each_lane_once(EngineKind kind)
    {
        var runs = new List<CountedRun>();
        foreach (var count in new[] { 2, 30 })
        {
            var (scenario, lanes) = await LanesAsync(kind, count);
            var run = await Counted.RunAsync(scenario, (workspace, _) => LaneOperations.Reorder(workspace, workspace.RootMap.Id, [.. Enumerable.Reverse(lanes)]));
            Assert.Equal(count, run.Outcome.Changes.Modified<Lane>().Count());
            runs.Add(run);
        }

        Counted.AssertIndependentReads(runs[0], runs[1]);
    }
}
