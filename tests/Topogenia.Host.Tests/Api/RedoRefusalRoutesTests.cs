// RedoRefusalRoutesTests.cs
// A refused redo through the whole host on both databases: 409 OPERATION_DEPASSEE naming its author, or 409 CARTE_MODIFIEE when a sheet or a document changed out of the undo stack, then nothing left to undo or to redo (annex B 2.6-48, AR-05, AR-43).

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Api;

/// <summary>
/// The 1.x cleared the history of the map when `retablir` failed (`magasin.ts`, `vider()`). Here the refusal of
/// `POST /api/maps/{id}/redo` clears, in a unit of its own, the undo and the redo stacks of the user on the map: the reply
/// is the refusal, the history read next is empty, and the change of the other member stays.
/// </summary>
public sealed class RedoRefusalRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private static IEnumerable<string?> LaneNames(JsonElement map) => map.GetProperty("lanes").EnumerateArray().Select(lane => lane.GetProperty("name").GetString());

    private static (int Undo, int Redo) Stacks(JsonElement history) =>
        (history.GetProperty("undo").GetArrayLength(), history.GetProperty("redo").GetArrayLength());

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_redo_outdated_by_another_member_is_refused_409_with_its_author_and_clears_the_undo_and_the_redo_stacks_of_the_user(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        var map = $"/api/maps/{project.RootMapId}";
        var lane = $"/api/lanes/{project.FirstLaneId}";
        (await facilitator.PostAsync($"{map}/lanes", new { name = "Visites" })).Ok();
        (await facilitator.PatchAsync(lane, new { name = "Achats" }, await project.LaneVersionAsync(project.FirstLaneId))).Ok();
        Assert.Equal((1, 1), Stacks((await facilitator.PostAsync($"{map}/undo")).Ok().GetProperty("history")));
        (await admin.PatchAsync(lane, new { name = "Comptabilité" }, await project.LaneVersionAsync(project.FirstLaneId))).Ok();
        var adminId = (await admin.GetAsync("/api/me")).Ok().GetProperty("id").GetGuid();

        var refusal = (await facilitator.PostAsync($"{map}/redo")).Refused(409, "OPERATION_DEPASSEE");

        Assert.Equal(adminId, refusal.GetProperty("updated_by").GetGuid());
        Assert.Equal((0, 0), Stacks((await facilitator.GetAsync($"{map}/history")).Ok()));
        (await facilitator.PostAsync($"{map}/redo")).Refused(404, "INTROUVABLE");
        (await facilitator.PostAsync($"{map}/undo")).Refused(404, "INTROUVABLE");
        Assert.Equal(["Comptabilité", "Visites"], LaneNames(await project.MapAsync()));
        Assert.Equal((1, 0), Stacks((await admin.GetAsync($"{map}/history")).Ok()));
    }

    /// <summary>A .pmap of the root map with the steps given, in the first lane, as the AI sends it back.</summary>
    private static async Task<string> PmapAsync(ApiProject project, params object[] steps)
    {
        var lane = (await project.MapAsync()).GetProperty("lanes")[0];
        return JsonSerializer.Serialize(new
        {
            format = "topogenia", version = "1.2", project = new { name = "Projet", code = project.Code },
            map = new { lanes = new object[] { new { id = lane.GetProperty("code").GetString(), name = lane.GetProperty("name").GetString() } }, steps, flows = Array.Empty<object>() },
        });
    }

    private static async Task<string> CodeOfAsync(ApiProject project, Guid step) =>
        (await project.MapAsync()).GetProperty("steps").EnumerateArray().Single(candidate => candidate.GetProperty("id").GetGuid() == step).GetProperty("code").GetString()!;

    /// <summary>Previews then applies a proposal on the root map, as the panel of the assistant does.</summary>
    private static async Task ApplyAsync(ApiClient facilitator, ApiProject project, string pmap)
    {
        var map = $"/api/maps/{project.RootMapId}";
        var preview = (await facilitator.PostAsync($"{map}/assistant/preview", new { pmap })).Ok();
        (await facilitator.PostAsync($"{map}/assistant/apply", new { pmap }, preview.GetProperty("structure_version").GetInt32())).Ok();
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_redo_of_a_proposal_whose_step_got_a_sheet_meanwhile_is_refused_409_map_modified_and_clears_the_stacks(EngineKind kind)
    {
        // Audit 3.1 V3, C2: the sheet saved meanwhile is out of the undo stack; the database refuses a second sheet on a step.
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        using var facilitator = project.Facilitator;
        var map = $"/api/maps/{project.RootMapId}";
        var step = await StepSheets.StepAsync(project, 0, "Saisir");
        var code = await CodeOfAsync(project, step);
        var lane = (await project.MapAsync()).GetProperty("lanes")[0].GetProperty("code").GetString();
        await ApplyAsync(facilitator, project, await PmapAsync(project, new { id = code, lane, label = "Saisir", column = 0, sheet = new { purpose = "Saisir la demande" } }));
        (await facilitator.PostAsync($"{map}/undo")).Ok();
        (await facilitator.PutAsync($"/api/steps/{step}/sheet", new { fields = new { purpose = new { value = "Saisir la commande" } } }, 0)).Ok();

        (await facilitator.PostAsync($"{map}/redo")).Refused(409, "CARTE_MODIFIEE");

        Assert.Equal((0, 0), Stacks((await facilitator.GetAsync($"{map}/history")).Ok()));
        Assert.Equal("Saisir la commande", (await facilitator.GetAsync($"/api/steps/{step}/sheet")).Ok().GetProperty("content").GetProperty("purpose").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_redo_of_a_proposal_whose_used_document_was_deleted_meanwhile_is_refused_409_map_modified_and_clears_the_stacks(EngineKind kind)
    {
        // Audit 3.1 V3, C3: the document deleted meanwhile is out of the undo stack; the database refuses a use of a missing document.
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        using var facilitator = project.Facilitator;
        var map = $"/api/maps/{project.RootMapId}";
        var producer = await StepSheets.StepAsync(project, 0, "Saisir");
        var user = await StepSheets.StepAsync(project, 1, "Valider");
        var document = (await facilitator.PostAsync($"/api/steps/{producer}/documents", new { direction = "output", name = "Bon de commande" })).Ok()
            .GetProperty("outputs")[0].GetProperty("document");
        var (producerCode, userCode) = (await CodeOfAsync(project, producer), await CodeOfAsync(project, user));
        var lane = (await project.MapAsync()).GetProperty("lanes")[0].GetProperty("code").GetString();
        await ApplyAsync(facilitator, project, await PmapAsync(project,
            new { id = producerCode, lane, label = "Saisir", column = 0, sheet = new { outputs = new object[] { new { id = $"{producerCode}-S1", name = "Bon de commande" } } } },
            new { id = userCode, lane, label = "Valider", column = 1, sheet = new { uses = new object[] { new { document = $"{producerCode}-S1", wait_hours = 2 } } } }));
        Assert.Equal(1, (await facilitator.GetAsync($"/api/steps/{user}/sheet")).Ok().GetProperty("inputs").GetArrayLength());
        (await facilitator.PostAsync($"{map}/undo")).Ok();
        (await facilitator.DeleteAsync($"/api/steps/{producer}/documents/{document.GetProperty("id").GetGuid()}", document.GetProperty("version").GetInt32())).Ok();

        (await facilitator.PostAsync($"{map}/redo")).Refused(409, "CARTE_MODIFIEE");

        Assert.Equal((0, 0), Stacks((await facilitator.GetAsync($"{map}/history")).Ok()));
        Assert.Equal(0, (await facilitator.GetAsync($"/api/steps/{user}/sheet")).Ok().GetProperty("inputs").GetArrayLength());
    }
}
