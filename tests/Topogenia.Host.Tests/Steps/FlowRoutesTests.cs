// FlowRoutesTests.cs
// The routes of the flows through the whole host on both databases: create, relabel with If-Match, reverse, delete, manual translations (annex A 1.3, A 1.5, A 2.6, AR-155).

using System.Text.Json;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Steps;

public sealed class FlowRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_flow_is_created_between_two_steps_of_the_map_with_its_label(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (source, target) = (await project.AddStepAsync(0, "Commander"), await project.AddStepAsync(1, "Payer"));
        var created = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/flows", new { source_step_id = source.Id, target_step_id = target.Id, label = "oui" })).Ok();
        var id = created.GetProperty("id").GetGuid();
        Assert.NotEqual(JsonValueKind.Null, created.GetProperty("operation_id").ValueKind);
        var flow = StructureCalls.FlowOf(await project.MapAsync(), id);
        Assert.Equal((source.Id, target.Id, "oui"), (flow.GetProperty("source_step_id").GetGuid(), flow.GetProperty("target_step_id").GetGuid(), flow.GetProperty("label").GetString()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_flow_to_itself_a_duplicate_flow_and_a_flow_to_another_map_are_refused(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (source, target) = (await project.AddStepAsync(0, "Commander"), await project.AddStepAsync(1, "Payer"));
        var (subMap, subLane) = await project.DetailAsync(source.Id);
        var inner = await project.AddStepAsync(0, "Saisir", subLane, subMap);
        var url = $"/api/maps/{project.RootMapId}/flows";
        (await facilitator.PostAsync(url, new { source_step_id = source.Id, target_step_id = source.Id })).Refused(400, "LIEN_REFLEXIF");
        (await facilitator.PostAsync(url, new { source_step_id = source.Id, target_step_id = inner.Id })).Refused(400, "LIEN_HORS_CARTE");
        (await facilitator.PostAsync(url, new { source_step_id = source.Id, target_step_id = Guid.NewGuid() })).Refused(404, "INTROUVABLE");
        Assert.Equal("target_step_id", (await facilitator.PostAsync(url, new { source_step_id = source.Id })).Refused(400, "CHAMP_OBLIGATOIRE").Field());
        (await facilitator.PostAsync(url, new { source_step_id = source.Id, target_step_id = target.Id, label = new string('x', 61) })).Refused(400, "TEXTE_TROP_LONG");
        Assert.Empty(StructureCalls.FlowPairs(await project.MapAsync()));
        await project.AddFlowAsync(source.Id, target.Id);
        (await facilitator.PostAsync(url, new { source_step_id = source.Id, target_step_id = target.Id })).Refused(409, "LIEN_DOUBLON");
        Assert.Single(StructureCalls.FlowPairs(await project.MapAsync()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_flow_is_relabelled_with_its_version_and_a_stale_version_is_a_conflict(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (source, target) = (await project.AddStepAsync(0, "Commander"), await project.AddStepAsync(1, "Payer"));
        var flow = await project.AddFlowAsync(source.Id, target.Id, "oui");
        var version = StructureCalls.FlowOf(await project.MapAsync(), flow).GetProperty("version").GetInt32();
        var relabelled = (await facilitator.PatchAsync($"/api/flows/{flow}", new { label = "non" }, version)).Ok();
        Assert.Equal(flow, relabelled.GetProperty("id").GetGuid());
        Assert.Equal("non", StructureCalls.FlowOf(relabelled.GetProperty("map"), flow).GetProperty("label").GetString());
        Assert.Equal("modifier_lien", relabelled.GetProperty("history").GetProperty("undo")[0].GetProperty("kind").GetString());

        Assert.Equal("anne", (await facilitator.PatchAsync($"/api/flows/{flow}", new { label = "peut-être" }, version)).Refused(409, "CONFLIT_VERSION")
            .GetProperty("by").GetString());
        var current = StructureCalls.FlowOf(relabelled.GetProperty("map"), flow).GetProperty("version").GetInt32();
        (await facilitator.PatchAsync($"/api/flows/{flow}", new { label = new string('x', 61) }, current)).Refused(400, "TEXTE_TROP_LONG");
        Assert.Equal("non", StructureCalls.FlowOf(await project.MapAsync(), flow).GetProperty("label").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_flow_is_reversed_with_its_label_undone_and_refused_when_its_reverse_is_drawn(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (source, target) = (await project.AddStepAsync(0, "Commander"), await project.AddStepAsync(1, "Payer"));
        var flow = await project.AddFlowAsync(source.Id, target.Id, "oui");
        var reversed = (await facilitator.PostAsync($"/api/flows/{flow}/reverse", null, await project.FlowVersionAsync(flow))).Ok();
        Assert.Equal(flow, reversed.GetProperty("id").GetGuid());
        var shown = StructureCalls.FlowOf(reversed.GetProperty("map"), flow);
        Assert.Equal((target.Id, source.Id, "oui"), (shown.GetProperty("source_step_id").GetGuid(), shown.GetProperty("target_step_id").GetGuid(), shown.GetProperty("label").GetString()));
        Assert.Equal("inverser_lien", reversed.GetProperty("history").GetProperty("undo")[0].GetProperty("kind").GetString());
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo", null)).Ok();
        Assert.Equal(source.Id, StructureCalls.FlowOf(await project.MapAsync(), flow).GetProperty("source_step_id").GetGuid());

        await project.AddFlowAsync(target.Id, source.Id, "retour");
        (await facilitator.PostAsync($"/api/flows/{flow}/reverse", null, await project.FlowVersionAsync(flow))).Refused(409, "LIEN_DOUBLON");
        (await facilitator.PostAsync($"/api/flows/{Guid.NewGuid()}/reverse", null, 1)).Refused(404, "INTROUVABLE");
        Assert.Equal(2, StructureCalls.FlowPairs(await project.MapAsync()).Count);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_deleted_flow_leaves_the_map_and_comes_back_on_undo(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (source, target) = (await project.AddStepAsync(0, "Commander"), await project.AddStepAsync(1, "Payer"));
        var flow = await project.AddFlowAsync(source.Id, target.Id);
        var deleted = (await facilitator.DeleteAsync($"/api/flows/{flow}", await project.FlowVersionAsync(flow))).Ok();
        Assert.Equal(flow, deleted.GetProperty("id").GetGuid());
        Assert.Empty(StructureCalls.FlowPairs(deleted.GetProperty("map")));
        Assert.Equal("supprimer", deleted.GetProperty("history").GetProperty("undo")[0].GetProperty("kind").GetString());
        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal([(source.Id, target.Id)], StructureCalls.FlowPairs(undone.GetProperty("map")));
        (await facilitator.DeleteAsync($"/api/flows/{Guid.NewGuid()}")).Refused(404, "INTROUVABLE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_contributor_neither_creates_relabels_deletes_nor_translates_a_flow_and_a_stranger_is_told_not_found(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var contributor = project.Contributor;
        using var stranger = project.Stranger;
        var (source, target) = (await project.AddStepAsync(0, "Commander"), await project.AddStepAsync(1, "Payer"));
        var flow = await project.AddFlowAsync(source.Id, target.Id, "oui");
        (await contributor.PostAsync($"/api/maps/{project.RootMapId}/flows", new { source_step_id = target.Id, target_step_id = source.Id })).Refused(403, "ACCES_REFUSE");
        (await stranger.PostAsync($"/api/maps/{project.RootMapId}/flows", new { source_step_id = target.Id, target_step_id = source.Id })).Refused(404, "INTROUVABLE");
        (await contributor.PatchAsync($"/api/flows/{flow}", new { label = "non" })).Refused(403, "ACCES_REFUSE");
        (await stranger.PatchAsync($"/api/flows/{flow}", new { label = "non" })).Refused(404, "INTROUVABLE");
        (await contributor.DeleteAsync($"/api/flows/{flow}")).Refused(403, "ACCES_REFUSE");
        (await stranger.DeleteAsync($"/api/flows/{flow}")).Refused(404, "INTROUVABLE");
        (await contributor.PutAsync($"/api/flows/{flow}/translations", new { translations = new { en = "yes" } })).Refused(403, "ACCES_REFUSE");
        (await stranger.PutAsync($"/api/flows/{flow}/translations", new { translations = new { en = "yes" } })).Refused(404, "INTROUVABLE");
        var read = StructureCalls.FlowOf(await project.MapAsync(), flow);
        Assert.Equal(("oui", 0), (read.GetProperty("label").GetString(), read.GetProperty("translations").EnumerateObject().Count()));
        Assert.Single(StructureCalls.FlowPairs(await project.MapAsync()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_manual_translations_of_a_flow_are_written_and_deleted_by_language(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (source, target) = (await project.AddStepAsync(0, "Commander"), await project.AddStepAsync(1, "Payer"));
        var flow = await project.AddFlowAsync(source.Id, target.Id, "oui");
        var url = $"/api/flows/{flow}/translations";
        var version = await project.FlowVersionAsync(flow);
        var written = (await facilitator.PutAsync(url, new { translations = new { en = "yes" } }, version)).Ok();
        Assert.Equal((flow, JsonValueKind.Null), (written.GetProperty("id").GetGuid(), written.GetProperty("operation_id").ValueKind));
        Assert.Equal("yes", StructureCalls.FlowOf(await project.MapAsync(), flow).GetProperty("translations").GetProperty("en").GetString());

        Assert.Equal("translations", (await facilitator.PutAsync(url, new { translations = new { eo = "jes" } }, version)).Refused(400, "LANGUE_INVALIDE").Field());
        var deleted = (await facilitator.PutAsync(url, new { translations = new Dictionary<string, string?> { ["en"] = null } }, version)).Ok();
        Assert.False(StructureCalls.FlowOf(deleted.GetProperty("map"), flow).GetProperty("translations").TryGetProperty("en", out _));
    }
}
