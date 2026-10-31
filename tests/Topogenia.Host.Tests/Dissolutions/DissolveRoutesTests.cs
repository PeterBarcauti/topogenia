// DissolveRoutesTests.cs
// The dissolution of a detail through the whole host on both databases: its reply, its If-Match, its undo by the map (annex A 1.5-01, 02, 14).

using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Host.Tests.Selection.MapDrawing;

namespace Topogenia.Host.Tests.Dissolutions;

public sealed class DissolveRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<DetailedRow> RowAsync(EngineKind kind) => await DetailedRow.CreateAsync(await ApiProject.CreateAsync(await hosts.OfAsync(kind)));

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_dissolution_answers_its_frame_the_dissolved_code_and_the_frame_steps_and_the_undo_of_the_map_takes_it_back(EngineKind kind)
    {
        var row = await RowAsync(kind);
        var root = row.Project.RootMapId;
        var code = Step(await row.Project.MapAsync(), row.Macro).GetProperty("code").GetString();
        using var facilitator = row.Project.Facilitator;
        var dissolved = (await facilitator.PostAsync($"/api/steps/{row.Macro}/dissolve", null, await row.MacroVersionAsync())).Ok();
        var frame = dissolved.GetProperty("id").GetGuid();
        Assert.Equal(code, dissolved.GetProperty("code").GetString());
        Assert.Equal([row.First, row.Second], Ids(dissolved));
        var map = dissolved.GetProperty("map");
        Assert.Equal((frame, row.Macro), (map.GetProperty("dissolution").GetProperty("id").GetGuid(), map.GetProperty("dissolution").GetProperty("step_id").GetGuid()));
        Assert.Equal(new HashSet<(Guid, Guid)> { (row.Before, row.First), (row.First, row.Second), (row.Second, row.After) }, Flows(map));
        Assert.DoesNotContain(row.Macro, StepIds(map));

        var undone = (await facilitator.PostAsync($"/api/maps/{root}/undo")).Ok();
        Assert.Equal((dissolved.GetProperty("operation_id").GetGuid(), "remonter"), (undone.GetProperty("id").GetGuid(), undone.GetProperty("code").GetString()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, undone.GetProperty("map").GetProperty("dissolution").ValueKind);
        Assert.Equal(new[] { row.Before, row.Macro, row.After }.Order(), StepIds(undone.GetProperty("map")).Order());
        Assert.Equal([row.First, row.Second], StepIds(await row.Project.MapAsync(row.SubMap)).Order());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_dissolution_checks_the_version_read_of_the_step_and_needs_a_detail(EngineKind kind)
    {
        var row = await RowAsync(kind);
        var read = await row.Project.MapAsync();
        var version = Step(read, row.Macro).GetProperty("version").GetInt32();
        using var facilitator = row.Project.Facilitator;
        var url = $"/api/steps/{row.Macro}/dissolve";
        (await facilitator.PostAsync(url, null, version + 1)).Refused(409, "CONFLIT_VERSION");
        (await facilitator.SendAsync(HttpMethod.Post, url, null, null, ("If-Match", "\"v1\""))).Refused(400, "VERSION_INVALIDE");
        (await facilitator.PostAsync($"/api/steps/{row.Before}/dissolve", null, Step(read, row.Before).GetProperty("version").GetInt32()))
            .Refused(400, "DETAIL_ABSENT");
        Assert.Contains(row.Macro, StepIds(await row.Project.MapAsync()));
        // Without If-Match: no check in desktop mode, the version is required in server mode (A-2.16-04).
        if (kind == EngineKind.SqlServer)
        {
            (await facilitator.PostAsync(url)).Refused(428, "VERSION_REQUISE");
            Assert.Contains(row.Macro, StepIds(await row.Project.MapAsync()));
            (await facilitator.PostAsync(url, null, version)).Ok();
        }
        else
            (await facilitator.PostAsync(url)).Ok();
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_dissolution_is_hidden_from_a_stranger_refused_to_a_contributor_and_an_open_frame_is_not_grouped(EngineKind kind)
    {
        var row = await RowAsync(kind);
        using var stranger = row.Project.Stranger;
        using var contributor = row.Project.Contributor;
        using var facilitator = row.Project.Facilitator;
        var url = $"/api/steps/{row.Macro}/dissolve";
        (await stranger.PostAsync(url)).Refused(404, "INTROUVABLE");
        (await contributor.PostAsync(url)).Refused(403, "ACCES_REFUSE");
        Assert.Contains(row.Macro, StepIds(await row.Project.MapAsync()));

        var version = await row.MacroVersionAsync();
        (await facilitator.PostAsync(url, null, version)).Ok();
        Assert.DoesNotContain(row.Macro, StepIds(await row.Project.MapAsync()));
        // A second dissolution: not found in desktop mode (no If-Match, no check). In server mode the version is required
        // (A-2.16-04) and the version read is checked before the domain: the dissolved step moved on since, and no route
        // shows it any more, so the client can only send the version it read.
        if (kind == EngineKind.SqlServer)
        {
            (await facilitator.PostAsync(url)).Refused(428, "VERSION_REQUISE");
            (await facilitator.PostAsync(url, null, version)).Refused(409, "CONFLIT_VERSION");
        }
        else
            (await facilitator.PostAsync(url)).Refused(404, "INTROUVABLE");
        var group = new { steps = new[] { row.First }, label = "Achats" };
        (await facilitator.PostAsync($"/api/maps/{row.Project.RootMapId}/group", group)).Refused(409, "REMONTEE_EN_COURS");
    }
}
