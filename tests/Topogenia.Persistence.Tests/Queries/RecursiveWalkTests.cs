// RecursiveWalkTests.cs
// The walk up the sub-maps, bounded at 50 levels, gives the same depth and breadcrumb on both engines (annex A 2.2, A 5).

using Topogenia.Application.Ports;
using Topogenia.Domain.SubMaps;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Queries;

/// <summary>
/// The 1.x walked up the sub-maps with a recursive query bounded at 50 levels (`ServiceCartes.cs:196`), proper to the SQL
/// of SQL Server. In 2.0 the walk is the single one of the domain (`ProjectWorkspace.ParentSteps`), on the records loaded by
/// the joins on the project: no recursive query exists, so nothing of it can differ from one engine to the other. A chain
/// of 60 maps written and read back gives, on each engine, the depth bounded at 50 and the breadcrumb of 51 maps that the
/// domain computes in memory on the workspace before the writing.
/// </summary>
public sealed class RecursiveWalkTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private const int Levels = 60;

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_chain_of_sixty_maps_read_back_stops_its_depth_and_breadcrumb_at_fifty_levels(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        var maps = new List<Guid>();
        var built = await scenario.RunAsync((workspace, context) => maps.AddRange(Chains.Build(workspace, context, Levels)));

        var stored = await scenario.LoadAsync(WorkspaceContent.None);

        var deepest = maps[^1];
        Assert.Equal(Levels + 1, maps.Count);
        Assert.Equal(ProjectWorkspace.MaxDepth, stored.Depth(deepest));
        Assert.Equal(built.Workspace.Depth(deepest), stored.Depth(deepest));
        var trail = SubMapRules.Breadcrumb(stored, deepest).Select(map => map.Id).ToList();
        Assert.Equal(maps.Skip(Levels - ProjectWorkspace.MaxDepth), trail);
        Assert.Equal(SubMapRules.Breadcrumb(built.Workspace, deepest).Select(map => map.Id), trail);
        Assert.Equal(ProjectWorkspace.MaxDepth - 1, stored.Depth(maps[ProjectWorkspace.MaxDepth - 2]));
    }
}
