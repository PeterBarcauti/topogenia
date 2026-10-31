// RaciAndTranslationRoutesTests.cs
// The RACI of a step and the manual translations of a step through the whole host on both databases (annex A 1.4, A 2.13, A 2.14).

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Steps;

public sealed class RaciAndTranslationRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static IReadOnlyList<(Guid Step, Guid Lane, string? Role)> Raci(JsonElement map) =>
        [.. map.GetProperty("raci").EnumerateArray().Select(role => (role.GetProperty("step_id").GetGuid(), role.GetProperty("lane_id").GetGuid(), role.GetProperty("role").GetString()))];

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_role_is_set_with_the_version_of_the_step_and_a_null_role_removes_it(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var lane = await project.AddLaneAsync("Achats");
        var step = await project.AddStepAsync(0, "Commander");
        var url = $"/api/steps/{step.Id}/raci";
        var version = await project.StepVersionAsync(step.Id);
        var set = (await facilitator.PutAsync(url, new { lane_id = lane, role = "C" }, version)).Ok();
        Assert.Equal(step.Id, set.GetProperty("id").GetGuid());
        Assert.NotEqual(JsonValueKind.Null, set.GetProperty("operation_id").ValueKind);
        Assert.Equal([(step.Id, lane, "C")], Raci(set.GetProperty("map")));
        Assert.Equal([(step.Id, lane, "C")], Raci(await project.MapAsync()));

        (await facilitator.PutAsync(url, new { lane_id = lane, role = "I" }, version)).Refused(409, "CONFLIT_VERSION");
        var removed = (await facilitator.PutAsync(url, new { lane_id = lane, role = (string?)null }, await project.StepVersionAsync(step.Id))).Ok();
        Assert.Empty(Raci(removed.GetProperty("map")));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_role_needs_a_lane_other_than_the_lane_of_the_step_a_known_letter_and_a_step_with_content(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var lane = await project.AddLaneAsync("Achats");
        var step = await project.AddStepAsync(0, "Commander");
        var start = await project.AddStepAsync(1, "Début", type: "start");
        var url = $"/api/steps/{step.Id}/raci";
        var version = await project.StepVersionAsync(step.Id);
        Assert.Equal("lane_id", (await facilitator.PutAsync(url, new { role = "C" }, version)).Refused(400, "CHAMP_OBLIGATOIRE").Field());
        Assert.Equal("role", (await facilitator.PutAsync(url, new { lane_id = lane, role = "Z" }, version)).Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Equal("lane_id", (await facilitator.PutAsync(url, new { lane_id = project.FirstLaneId, role = "C" }, version)).Refused(400, "VALEUR_INVALIDE").Field());
        (await facilitator.PutAsync($"/api/steps/{start.Id}/raci", new { lane_id = lane, role = "C" }, await project.StepVersionAsync(start.Id)))
            .Refused(400, "ETAPE_SANS_CONTENU");
        Assert.Empty(Raci(await project.MapAsync()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_contributor_sets_no_role_and_writes_no_translation_and_a_stranger_is_told_not_found(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var contributor = project.Contributor;
        using var stranger = project.Stranger;
        var lane = await project.AddLaneAsync("Achats");
        var step = await project.AddStepAsync(0, "Commander");
        (await contributor.PutAsync($"/api/steps/{step.Id}/raci", new { lane_id = lane, role = "C" })).Refused(403, "ACCES_REFUSE");
        (await stranger.PutAsync($"/api/steps/{step.Id}/raci", new { lane_id = lane, role = "C" })).Refused(404, "INTROUVABLE");
        (await contributor.PutAsync($"/api/steps/{step.Id}/translations", new { translations = new { en = "Order" } })).Refused(403, "ACCES_REFUSE");
        (await stranger.PutAsync($"/api/steps/{step.Id}/translations", new { translations = new { en = "Order" } })).Refused(404, "INTROUVABLE");
        var read = StructureCalls.StepOf(await project.MapAsync(), step.Id);
        Assert.Empty(read.GetProperty("translations").EnumerateObject());
        Assert.Empty(Raci(await project.MapAsync()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_manual_translations_of_a_step_are_written_and_deleted_by_language_without_entering_the_history(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var step = await project.AddStepAsync(0, "Commander");
        var url = $"/api/steps/{step.Id}/translations";
        var version = await project.StepVersionAsync(step.Id);
        var written = (await facilitator.PutAsync(url, new { translations = new { en = "Order" } }, version)).Ok();
        Assert.Equal(step.Id, written.GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, written.GetProperty("operation_id").ValueKind);
        Assert.Equal("Order", StructureCalls.StepOf(written.GetProperty("map"), step.Id).GetProperty("translations").GetProperty("en").GetString());
        Assert.Equal("Order", StructureCalls.StepOf(await project.MapAsync(), step.Id).GetProperty("translations").GetProperty("en").GetString());

        Assert.Equal("translations", (await facilitator.PutAsync(url, new { translations = new { eo = "Mendi" } }, version)).Refused(400, "LANGUE_INVALIDE").Field());
        var deleted = (await facilitator.PutAsync(url, new { translations = new Dictionary<string, string?> { ["en"] = null } }, version)).Ok();
        Assert.False(StructureCalls.StepOf(deleted.GetProperty("map"), step.Id).GetProperty("translations").TryGetProperty("en", out _));
        var history = (await facilitator.GetAsync($"/api/maps/{project.RootMapId}/history")).Ok().GetProperty("undo");
        Assert.Equal(["creer_etape"], history.EnumerateArray().Select(item => item.GetProperty("kind").GetString()));
    }
}
