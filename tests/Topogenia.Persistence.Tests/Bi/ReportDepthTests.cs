// ReportDepthTests.cs
// The report view of the maps gives the 101 levels that the 1.x showed (AR-87), on a chain of sub-maps deeper than the recursion of SQL Server allows.

using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Persistence.SqlServer;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Persistence.SqlServer.SqlServerAccessProbe;

namespace Topogenia.Persistence.Tests.Bi;

/// <summary>
/// A-2.2-28, AR-67, AR-87: the domain walks at most 50 levels and only warns beyond the recommended depth, so a database may
/// hold a deeper chain. The view of the 1.x had no bound and failed beyond 101 levels (100 recursions of SQL Server, which
/// a view cannot raise); the view of the 2.0 gives what the 1.x showed, the 101 first levels, and fails on none. Below the
/// root map and the detailed map of <see cref="ProjectGraph"/>, 105 more levels (107 in all): `bi.process_map` gives the
/// levels 1 to 101, nor do the views read through it fail (migration `ReportMapDepthOf101`).
/// </summary>
public sealed class ReportDepthTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private const int ExtraLevels = 105;

    /// <summary>The levels of the report view (AR-87): the 1.x showed them all, it failed below.</summary>
    private const int ReportDepth = 101;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The graph, then one map per level under the detailed step, each with one lane and one step detailed by the next.</summary>
    private static (ProjectGraph Graph, List<object> Records) DeepProject()
    {
        var graph = ProjectGraph.New();
        var records = graph.Records.ToList();
        var parent = graph.SubStep;
        for (var level = 1; level <= ExtraLevels; level++)
        {
            var map = new ProcessMap(Guid.CreateVersion7(), graph.Project.Id, parent.Id, $"Level {level + 2}", "en", graph.At(4), graph.Owner.Id,
                null, null, null, null, 1);
            var lane = new Lane(Guid.CreateVersion7(), map.Id, graph.ReadableCode('C', 100 + level), $"Lane {level}", "en", LaneType.Department, null,
                LaneColors.ForIndex(1), 0, null, graph.At(4), graph.Owner.Id, null, null, null, null, 1);
            parent = new Step(Guid.CreateVersion7(), map.Id, lane.Id, graph.ReadableCode('E', 100 + level), $"Step {level}", "en", StepType.Task, 0, 0,
                null, AutomationStatus.Manual, null, graph.At(4), graph.Owner.Id, null, null, null, null, 1);
            records.AddRange([map, lane, parent]);
        }
        return (graph, records);
    }

    private static async Task<ReportViewRows> ReadAsReportsAsync(TestDatabase database, string view)
    {
        await using var session = await RoleSession.OpenAsync(database, SqlServerAccess.ReportRole);
        return await SqlServerAccessProbe.ReadViewAsync(session.Connection, view, Token);
    }

    [Fact]
    public async Task The_report_view_of_the_maps_gives_the_hundred_and_one_levels_of_the_1x_of_a_deeper_chain_without_failing()
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        var (graph, records) = DeepProject();
        await using (var context = database.Context())
        {
            context.AddRange(records);
            await context.SaveChangesAsync(Token);
        }

        var maps = (await ReadAsReportsAsync(database, "process_map")).Rows.Where(row => graph.Project.Id.Equals(row[1])).ToList();
        Assert.Equal(Enumerable.Range(1, ReportDepth), maps.Select(row => (int)row[4]!).Order());
        var deepest = Assert.Single(maps, row => (int)row[4]! == ReportDepth);
        Assert.Equal($"Level {ReportDepth}", deepest[3]);

        // The views read through bi.process_map stop with it: the steps of the 101 levels, none below.
        var mapIds = maps.Select(row => (Guid)row[0]!).ToHashSet();
        var steps = (await ReadAsReportsAsync(database, "step")).Rows.Where(row => graph.Project.Id.Equals(row[2])).ToList();
        Assert.All(steps, row => Assert.Contains((Guid)row[1]!, mapIds));
        Assert.Contains(steps, row => $"Step {ReportDepth - 2}".Equals(row[5]));
        Assert.DoesNotContain(steps, row => $"Step {ReportDepth - 1}".Equals(row[5]));
    }
}
