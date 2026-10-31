// ContractCodesTests.cs
// Codes of the contract that no other route test produced through HTTP: code of a project in the trash, lane deleted, layout warnings as data (annex A 3).

using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Api;

public sealed class ContractCodesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_code_of_a_project_in_the_trash_stays_taken_with_its_own_code(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        (await facilitator.DeleteAsync($"/api/projects/{project.Id}?confirmation={project.Code}", await project.ProjectVersionAsync())).Ok(204);
        (await admin.PostAsync("/api/projects", new { code = project.Code, name = "Ventes" })).Refused(409, "PROJET_CODE_CORBEILLE");
        (await facilitator.PostAsync($"/api/trash/{project.Id}/restore")).Ok(204);
        (await admin.PostAsync("/api/projects", new { code = project.Code, name = "Ventes" })).Refused(409, "PROJET_CODE_EXISTANT");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_placed_in_a_lane_deleted_since_is_refused_as_a_lane_deleted_and_nothing_is_written(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var lane = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Achats" })).Ok().GetProperty("id").GetGuid();
        (await facilitator.DeleteAsync($"/api/lanes/{lane}", await project.LaneVersionAsync(lane))).Ok();
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/steps", new { lane_id = lane, column = 0, label = "Commander" }))
            .Refused(409, "COULOIR_SUPPRIME");
        Assert.Empty((await project.MapAsync()).GetProperty("steps").EnumerateArray());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_read_of_a_map_gives_its_layout_warnings_as_data_by_step_and_code(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        async Task<Guid> StepAsync(int column, string label, string type) =>
            (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/steps", new { lane_id = project.FirstLaneId, column, label, type })).Ok()
                .GetProperty("id").GetGuid();
        var start = await StepAsync(0, "Début", "start");
        var lonely = await StepAsync(1, "Isolée", "task");
        var end = await StepAsync(2, "Fin", "end");
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/flows", new { source_step_id = end, target_step_id = start })).Ok();
        var warnings = (await project.MapAsync()).GetProperty("warnings").EnumerateArray()
            .Select(warning => (warning.GetProperty("step_id").GetGuid(), warning.GetProperty("code").GetString()))
            .ToHashSet();
        Assert.Equal([(start, "DEBUT_LIEN_ENTRANT"), (lonely, "ETAPE_ISOLEE"), (end, "FIN_LIEN_SORTANT")], warnings);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_rows_of_a_workbook_left_aside_give_their_reason_as_data_and_nothing_is_written(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var step = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/steps", new { lane_id = project.FirstLaneId, column = 0, label = "Commander" }))
            .Ok().GetProperty("id").GetGuid();
        (await facilitator.PutAsync($"/api/steps/{step}/translations", new { translations = new { en = "Order" } }, await project.StepVersionAsync(step))).Ok();
        var imported = (await facilitator.PutAsync($"/api/projects/{project.Id}/translations", new
        {
            scope = "missing",
            rows = new object[]
            {
                new { line = 2, type = "step", id = step, original = "Commander", translations = new Dictionary<string, string> { ["en"] = "Purchase" } },
                new { line = 3, type = "lane", id = project.FirstLaneId, original = "Autre texte", translations = new Dictionary<string, string> { ["en"] = "Actor" } },
                new { line = 4, type = "lane", id = project.FirstLaneId, original = "Acteur 1", translations = new Dictionary<string, string>() },
            },
        })).Ok();
        Assert.Equal(0, imported.GetProperty("written").GetInt32());
        Assert.Equal([(2, "TRADUCTION_MANUELLE"), (3, "TEXTE_MODIFIE"), (4, "SANS_TRADUCTION")],
            imported.GetProperty("ignored").EnumerateArray().Select(row => (row.GetProperty("line").GetInt32(), row.GetProperty("reason").GetString())).OrderBy(row => row.Item1));
        var all = (await facilitator.GetAsync($"/api/projects/{project.Id}/translations?scope=all")).Ok().GetProperty("texts").EnumerateArray()
            .Single(text => text.GetProperty("id").GetGuid() == step);
        Assert.Equal(("Order", "manual"), (all.GetProperty("translations").GetProperty("en").GetString(), all.GetProperty("sources").GetProperty("en").GetString()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_command_bar_takes_the_code_of_a_step_of_another_project_for_an_unknown_one(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var other = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var otherFacilitator = other.Facilitator;
        async Task<string> CodeAsync(ApiClient client, ApiProject owner, string label) =>
            (await client.PostAsync($"/api/maps/{owner.RootMapId}/steps", new { lane_id = owner.FirstLaneId, column = 0, label })).Ok()
                .GetProperty("code").GetString()!;
        var own = await CodeAsync(facilitator, project, "Commander");
        var foreign = await CodeAsync(otherFacilitator, other, "Payer");
        var errors = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/command", new { text = $"{own} > {foreign}" }))
            .Refused(400, "COMMANDE_ERREURS").GetProperty("errors").EnumerateArray().ToList();
        var error = Assert.Single(errors);
        Assert.Equal(("ETAPE_INCONNUE", foreign), (error.GetProperty("code").GetString(), error.GetProperty("detail").GetString()));
        Assert.Empty((await project.MapAsync()).GetProperty("flows").EnumerateArray());
    }
}
