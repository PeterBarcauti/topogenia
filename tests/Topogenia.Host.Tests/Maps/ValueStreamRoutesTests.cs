// ValueStreamRoutesTests.cs
// The value stream through the whole host on both databases: the four entries and the work time by their routes, the figures in the view of the map (ADR 085).

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Maps;

public sealed class ValueStreamRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static int VersionOf(JsonElement map, string list, Guid id) =>
        map.GetProperty(list).EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == id).GetProperty("version").GetInt32();

    /// <summary>A start of 120 cases a month, a step of 30 minutes and 2 days, a decision of 10 minutes that sends 25 % back.</summary>
    private static async Task<(Guid Start, Guid Work, Guid Check, Guid Back)> ClaimsAsync(ApiProject project)
    {
        using var facilitator = project.Facilitator;
        var start = await project.AddStepAsync(0, "Demande reçue", type: "start");
        var work = await project.AddStepAsync(1, "Traiter la demande");
        var check = await project.AddStepAsync(2, "Conforme ?", type: "decision");
        var end = await project.AddStepAsync(3, "Demande close", type: "end");
        await project.AddFlowAsync(start.Id, work.Id);
        await project.AddFlowAsync(work.Id, check.Id);
        var back = await project.AddFlowAsync(check.Id, work.Id, "non");
        await project.AddFlowAsync(check.Id, end.Id, "oui");
        var map = await project.MapAsync();
        (await facilitator.PatchAsync($"/api/steps/{start.Id}", new { volume = new { quantity = 120, period = "month" } }, VersionOf(map, "steps", start.Id))).Ok();
        map = await project.MapAsync();
        (await facilitator.PutAsync($"/api/flows/{back}/share", new { share = 25 }, VersionOf(map, "flows", back))).Ok();
        (await facilitator.PutAsync($"/api/steps/{work.Id}/sheet",
            new { fields = new { duration_minutes = new { number = 30 }, frequency = new { value = "per_case" }, lead_time = new { value = "day", number = 2 } } },
            await StepSheets.ReadVersionAsync(facilitator, work.Id))).Ok();
        (await facilitator.PutAsync($"/api/steps/{check.Id}/sheet",
            new { fields = new { duration_minutes = new { number = 10 }, frequency = new { value = "per_case" }, lead_time = new { value = "hour", number = 1 } } },
            await StepSheets.ReadVersionAsync(facilitator, check.Id))).Ok();
        return (start.Id, work.Id, check.Id, back);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_entries_written_by_their_routes_give_the_figures_of_the_view_of_the_map(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var (start, work, _, back) = await ClaimsAsync(project);
        using var facilitator = project.Facilitator;
        var detail = (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok();
        (await facilitator.PutAsync($"/api/projects/{project.Id}/work-time", new { workday_hours = 8 }, detail.GetProperty("version").GetInt32())).Ok();
        var map = await project.MapAsync();
        (await facilitator.PatchAsync($"/api/lanes/{project.FirstLaneId}", new { headcount = 4 }, VersionOf(map, "lanes", project.FirstLaneId))).Ok();

        map = await project.MapAsync();
        var stream = map.GetProperty("value_stream");
        Assert.Equal(160d, stream.GetProperty("steps").GetProperty(work.ToString()).GetProperty("throughput").GetDouble(), 9);
        Assert.Equal(160d * 40 / 60, stream.GetProperty("totals").GetProperty("charge_hours").GetDouble(), 9);
        Assert.Equal((8d, 151.67d), (stream.GetProperty("work_time").GetProperty("workday_hours").GetDouble(),
            stream.GetProperty("work_time").GetProperty("fte_hours_per_month").GetDouble()));
        Assert.Equal(17d, stream.GetProperty("main_path").GetProperty("lead_hours").GetDouble(), 9);
        Assert.Equal(0.25, stream.GetProperty("flows").GetProperty(back.ToString()).GetProperty("share").GetDouble(), 9);
        Assert.Equal((120m, "month"), (StructureCalls.StepOf(map, start).GetProperty("volume").GetProperty("quantity").GetDecimal(),
            StructureCalls.StepOf(map, start).GetProperty("volume").GetProperty("period").GetString()));
        Assert.Equal(25m, StructureCalls.FlowOf(map, back).GetProperty("share_percent").GetDecimal());
        Assert.Equal(4, map.GetProperty("lanes")[0].GetProperty("headcount").GetInt32());
        Assert.DoesNotContain(stream.GetProperty("issues").EnumerateArray(), issue => issue.GetProperty("severity").GetString() == "warning"
            && issue.GetProperty("code").GetString() != "VALEUR_BROUILLON");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_entry_out_of_its_place_is_refused_and_a_contributor_writes_none_of_the_structure(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var (start, work, _, back) = await ClaimsAsync(project);
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;
        var map = await project.MapAsync();
        Assert.Equal("volume", (await facilitator.PatchAsync($"/api/steps/{work}", new { volume = new { quantity = 3, period = "day" } },
            VersionOf(map, "steps", work))).Refused(400, "VALEUR_INVALIDE").Field());
        var forward = map.GetProperty("flows").EnumerateArray().Single(flow => flow.GetProperty("source_step_id").GetGuid() == start
            && flow.GetProperty("target_step_id").GetGuid() == work).GetProperty("id").GetGuid();
        Assert.Equal("share", (await facilitator.PutAsync($"/api/flows/{forward}/share", new { share = 50 }, VersionOf(map, "flows", forward)))
            .Refused(400, "VALEUR_INVALIDE").Field());
        (await contributor.PutAsync($"/api/flows/{back}/share", new { share = 10 }, VersionOf(map, "flows", back))).Refused(403, "ACCES_REFUSE");
        var detail = (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok();
        Assert.Equal("workday_hours", (await facilitator.PutAsync($"/api/projects/{project.Id}/work-time", new { workday_hours = 25 },
            detail.GetProperty("version").GetInt32())).Refused(400, "VALEUR_INVALIDE").Field());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_share_is_one_operation_undone_by_one_undo(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var (_, _, _, back) = await ClaimsAsync(project);
        using var facilitator = project.Facilitator;
        var map = await project.MapAsync();
        (await facilitator.PutAsync($"/api/flows/{back}/share", new { share = 40 }, VersionOf(map, "flows", back))).Ok();
        Assert.Equal(40m, StructureCalls.FlowOf(await project.MapAsync(), back).GetProperty("share_percent").GetDecimal());
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal(25m, StructureCalls.FlowOf(await project.MapAsync(), back).GetProperty("share_percent").GetDecimal());
    }
}
