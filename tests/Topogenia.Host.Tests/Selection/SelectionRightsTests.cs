// SelectionRightsTests.cs
// The selection routes are kept from a stranger and from the members below the facilitator, on both databases (annex A 1.3-14 to 26, A-1.3-21).

using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Host.Tests.Selection.MapDrawing;

namespace Topogenia.Host.Tests.Selection;

public sealed class SelectionRightsTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_selection_routes_hide_the_map_from_a_stranger_and_refuse_a_contributor_and_a_reader(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        var row = await RowAsync(project, "Commander");
        var map = project.RootMapId;
        var requests = new (string Url, object Body)[]
        {
            ($"/api/maps/{map}/deletion", new { steps = row }),
            ($"/api/maps/{map}/paste", new { steps = new[] { new { key = "x", label = "Valider", lane_id = project.FirstLaneId, column = 1 } } }),
            ($"/api/maps/{map}/transfer", new { steps = new[] { new { id = row[0], lane_id = project.FirstLaneId, column = 2 } } }),
            ($"/api/maps/{map}/group", new { steps = row, label = "Achats" }),
        };
        using var stranger = project.Stranger;
        using var contributor = project.Contributor;
        using var reader = project.Reader;
        foreach (var (url, body) in requests)
        {
            (await stranger.PostAsync(url, body)).Refused(404, "INTROUVABLE");
            (await contributor.PostAsync(url, body)).Refused(403, "ACCES_REFUSE");
            (await reader.PostAsync(url, body)).Refused(403, "ACCES_REFUSE");
        }
        var after = await project.MapAsync();
        Assert.Equal((row[0], 0), (StepIds(after).Single(), Step(after, row[0]).GetProperty("column").GetInt32()));
    }
}
