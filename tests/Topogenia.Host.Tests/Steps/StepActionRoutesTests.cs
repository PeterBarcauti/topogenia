// StepActionRoutesTests.cs
// The insertion next to a step and the detail of a step (1.x `detailler`) through the whole host on both databases (annex A 1.4, A 2.2, A 2.4).

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Steps;

public sealed class StepActionRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static IReadOnlyList<string> Warnings(JsonElement reply) => [.. reply.GetProperty("warnings").EnumerateArray().Select(warning => warning.GetString()!)];

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_inserted_after_another_takes_its_outgoing_flow_and_is_answered_with_its_code(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (first, last) = (await project.AddStepAsync(0, "Commander"), await project.AddStepAsync(1, "Payer"));
        await project.AddFlowAsync(first.Id, last.Id);
        var inserted = (await facilitator.PostAsync($"/api/steps/{first.Id}/insert", new { position = "after", label = "Contrôler", type = "decision" })).Ok();
        var id = inserted.GetProperty("id").GetGuid();
        Assert.StartsWith($"{project.Code}-", inserted.GetProperty("code").GetString(), StringComparison.Ordinal);
        Assert.NotEqual(JsonValueKind.Null, inserted.GetProperty("operation_id").ValueKind);
        var map = await project.MapAsync();
        var step = StructureCalls.StepOf(map, id);
        Assert.Equal(("Contrôler", "decision", 1), (step.GetProperty("label").GetString(), step.GetProperty("type").GetString(), step.GetProperty("column").GetInt32()));
        Assert.Equal(inserted.GetProperty("code").GetString(), step.GetProperty("code").GetString());
        Assert.Equal(2, StructureCalls.StepOf(map, last.Id).GetProperty("column").GetInt32());
        Assert.Equal([(first.Id, id), (id, last.Id)], StructureCalls.FlowPairs(map).OrderBy(pair => pair.Source == first.Id ? 0 : 1));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_insertion_needs_a_known_position_and_a_facilitator(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;
        using var stranger = project.Stranger;
        var step = await project.AddStepAsync(0, "Commander");
        var url = $"/api/steps/{step.Id}/insert";
        Assert.Equal("position", (await facilitator.PostAsync(url, new { position = "inside", label = "Contrôler" })).Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Equal("position", (await facilitator.PostAsync(url, new { label = "Contrôler" })).Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Equal("position", (await facilitator.PostAsync(url, new { position = "below", label = "Contrôler" })).Refused(400, "VALEUR_INVALIDE").Field());
        (await facilitator.PostAsync(url, new { position = "after", label = " " })).Refused(400, "CHAMP_OBLIGATOIRE");
        (await contributor.PostAsync(url, new { position = "after", label = "Contrôler" })).Refused(403, "ACCES_REFUSE");
        (await stranger.PostAsync(url, new { position = "after", label = "Contrôler" })).Refused(404, "INTROUVABLE");
        Assert.Equal(1, (await project.MapAsync()).GetProperty("steps").GetArrayLength());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Detailing_a_step_answers_its_sub_map_and_detailing_it_again_while_empty_warns_and_logs_nothing(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var step = await project.AddStepAsync(0, "Commander");
        var detailed = (await facilitator.PostAsync($"/api/steps/{step.Id}/detail", null, await project.StepVersionAsync(step.Id))).Ok();
        var subMap = detailed.GetProperty("id").GetGuid();
        Assert.Equal([subMap], detailed.Ids());
        Assert.Empty(Warnings(detailed));
        Assert.NotEqual(JsonValueKind.Null, detailed.GetProperty("operation_id").ValueKind);
        Assert.Equal(subMap, StructureCalls.StepOf(detailed.GetProperty("map"), step.Id).GetProperty("sub_map_id").GetGuid());
        Assert.Equal((step.Id, "Commander"), ((await project.MapAsync(subMap)).GetProperty("parent_step_id").GetGuid(), (await project.MapAsync(subMap)).GetProperty("name").GetString()));

        var versionAfterDetail = StructureCalls.StepOf(detailed.GetProperty("map"), step.Id).GetProperty("version").GetInt32();
        var reopened = (await facilitator.PostAsync($"/api/steps/{step.Id}/detail", null, versionAfterDetail)).Ok();
        Assert.Equal(subMap, reopened.GetProperty("id").GetGuid());
        Assert.Equal(["SOUS_CARTE_EXISTANTE"], Warnings(reopened));
        Assert.Equal(JsonValueKind.Null, reopened.GetProperty("operation_id").ValueKind);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Detailing_checks_the_version_of_the_step_and_refuses_a_start_a_filled_sub_map_and_a_contributor(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;
        var start = await project.AddStepAsync(0, "Début", type: "start");
        (await facilitator.PostAsync($"/api/steps/{start.Id}/detail", null, await project.StepVersionAsync(start.Id))).Refused(400, "ETAPE_NON_DETAILLABLE");
        var step = await project.AddStepAsync(1, "Commander");
        var version = await project.StepVersionAsync(step.Id);
        (await facilitator.PatchAsync($"/api/steps/{step.Id}", new { label = "Facturer" }, version)).Ok();
        (await facilitator.PostAsync($"/api/steps/{step.Id}/detail", null, version)).Refused(409, "CONFLIT_VERSION");
        (await contributor.PostAsync($"/api/steps/{step.Id}/detail")).Refused(403, "ACCES_REFUSE");
        Assert.Equal(JsonValueKind.Null, StructureCalls.StepOf(await project.MapAsync(), step.Id).GetProperty("sub_map_id").ValueKind);

        var subMap = (await facilitator.PostAsync($"/api/steps/{step.Id}/detail", null, await project.StepVersionAsync(step.Id))).Ok().GetProperty("id").GetGuid();
        var subLane = (await project.MapAsync(subMap)).GetProperty("lanes")[0].GetProperty("id").GetGuid();
        await project.AddStepAsync(0, "Saisir", subLane, subMap);
        (await facilitator.PostAsync($"/api/steps/{step.Id}/detail", null, await project.StepVersionAsync(step.Id))).Refused(409, "ETAPE_DEJA_DETAILLEE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_detail_deeper_than_the_recommended_depth_is_answered_with_its_warning(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (mapId, laneId) = (project.RootMapId, project.FirstLaneId);
        var warnings = new List<IReadOnlyList<string>>();
        for (var level = 0; level < 5; level++)
        {
            var step = await project.AddStepAsync(0, $"Niveau {level}", laneId, mapId);
            var reply = (await facilitator.PostAsync($"/api/steps/{step.Id}/detail", null, await project.StepVersionAsync(step.Id, mapId))).Ok();
            warnings.Add(Warnings(reply));
            mapId = reply.GetProperty("id").GetGuid();
            laneId = (await project.MapAsync(mapId)).GetProperty("lanes")[0].GetProperty("id").GetGuid();
        }
        Assert.All(warnings.Take(3), Assert.Empty);
        Assert.Contains("PROFONDEUR_DEPASSEE", warnings[^1]);
    }
}
