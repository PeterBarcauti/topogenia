// ProjectsAndSecurityTrashPortedTests.cs
// The 1.x integration tests of the trash ported to the whole host on both databases: move to the trash, restore, purge at once (T-CORB-01 to 03).

using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Ported;

public sealed class ProjectsAndSecurityTrashPortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static async Task<ApiResponse> MoveToTrashAsync(ApiClient client, ApiProject project, string confirmation) =>
        await client.DeleteAsync($"/api/projects/{project.Id}?confirmation={Uri.EscapeDataString(confirmation)}", await project.ProjectVersionAsync());

    private static async Task<IReadOnlyList<Guid>> ProjectIdsAsync(ApiClient client) =>
        [.. (await client.GetAsync("/api/projects")).Ok().EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];

    private static async Task<IReadOnlyList<Guid>> TrashIdsAsync(ApiClient client) =>
        [.. (await client.GetAsync("/api/trash")).Ok().GetProperty("projects").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];

    /// <summary>
    /// A project with content: two steps linked by a flow, the first detailed, given a role on a second lane and a
    /// manual translation; returns the project.
    /// </summary>
    private async Task<ApiProject> FullProjectAsync(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var first = await project.AddStepAsync(0, "Extraction");
        var next = await project.AddStepAsync(1, "Tableau");
        await project.AddFlowAsync(first.Id, next.Id, "oui");
        var (subMap, subLane) = await project.DetailAsync(first.Id);
        await project.AddStepAsync(0, "Lancer la requête", subLane, subMap);
        var other = await project.AddLaneAsync("Logistique");
        (await facilitator.PutAsync($"/api/steps/{first.Id}/raci", new { lane_id = other, role = "C" }, await project.StepVersionAsync(first.Id))).Ok();
        (await facilitator.PutAsync($"/api/steps/{first.Id}/translations", new { translations = new { en = "Extract" } }, await project.StepVersionAsync(first.Id))).Ok();
        return project;
    }

    // T-CORB-01
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Only_the_facilitator_moves_a_project_to_the_trash_with_its_exact_code_and_then_nobody_reaches_it_the_administrator_included(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;
        using var stranger = project.Stranger;
        using var reader = project.Reader;
        using var admin = project.Admin;
        (await MoveToTrashAsync(contributor, project, project.Code)).Refused(403, "ACCES_REFUSE");
        (await MoveToTrashAsync(stranger, project, project.Code)).Refused(404, "INTROUVABLE");
        var wrongCase = await MoveToTrashAsync(facilitator, project, project.Code.ToLowerInvariant());
        Assert.Equal(project.Code, wrongCase.Refused(400, "CONFIRMATION_INVALIDE").GetProperty("code").GetString());
        Assert.Contains(project.Code, wrongCase.Json.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Contains(project.Id, await ProjectIdsAsync(facilitator));

        Assert.Equal(204, (await MoveToTrashAsync(facilitator, project, project.Code)).Status);
        foreach (var client in new[] { admin, facilitator, reader })
        {
            Assert.DoesNotContain(project.Id, await ProjectIdsAsync(client));
            (await client.GetAsync($"/api/maps/{project.RootMapId}")).Refused(404, "INTROUVABLE");
            (await client.GetAsync($"/api/projects/{project.Id}")).Refused(404, "INTROUVABLE");
            var roles = (await client.GetAsync("/api/me")).Ok().GetProperty("roles").EnumerateArray();
            Assert.DoesNotContain(project.Id, roles.Select(role => role.GetProperty("project_id").GetGuid()));
        }
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/steps", new { lane_id = project.FirstLaneId, column = 0, label = "x" })).Refused(404, "INTROUVABLE");
    }

    // T-CORB-02
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_trash_shows_the_project_to_its_facilitator_and_the_administrator_its_code_stays_reserved_and_only_the_facilitator_restores_it(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;
        using var stranger = project.Stranger;
        using var reader = project.Reader;
        using var admin = project.Admin;
        Assert.Equal(204, (await MoveToTrashAsync(facilitator, project, project.Code)).Status);

        var trash = (await facilitator.GetAsync("/api/trash")).Ok();
        Assert.Equal(30, trash.GetProperty("retention_days").GetInt32());
        var item = trash.GetProperty("projects").EnumerateArray().Single(entry => entry.GetProperty("id").GetGuid() == project.Id);
        Assert.Equal((project.Code, 0), (item.GetProperty("code").GetString(), item.GetProperty("step_count").GetInt32()));
        Assert.Equal(TimeSpan.FromDays(30), item.GetProperty("purge_at").GetDateTime() - item.GetProperty("deleted_at").GetDateTime());
        Assert.Contains(project.Id, await TrashIdsAsync(admin));
        Assert.DoesNotContain(project.Id, await TrashIdsAsync(contributor));
        Assert.DoesNotContain(project.Id, await TrashIdsAsync(stranger));

        var reserved = await admin.PostAsync("/api/projects", new { code = project.Code, name = "Autre" });
        reserved.Refused(409, "PROJET_CODE_CORBEILLE");
        Assert.Contains(project.Code, reserved.Json.GetProperty("message").GetString(), StringComparison.Ordinal);

        (await contributor.PostAsync($"/api/trash/{project.Id}/restore")).Refused(403, "ACCES_REFUSE");
        Assert.Equal(204, (await facilitator.PostAsync($"/api/trash/{project.Id}/restore")).Status);
        Assert.Contains(project.Id, await ProjectIdsAsync(reader));
        Assert.DoesNotContain(project.Id, await TrashIdsAsync(facilitator));
        (await facilitator.PostAsync($"/api/trash/{project.Id}/restore")).Refused(404, "INTROUVABLE");
        (await admin.DeleteAsync($"/api/trash/{project.Id}")).Refused(404, "INTROUVABLE");
        Assert.Equal(project.Code, (await reader.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("code").GetString());
    }

    // T-CORB-03
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_purge_by_the_administrator_removes_the_whole_project_only_and_frees_its_code(EngineKind kind)
    {
        var project = await FullProjectAsync(kind);
        var witness = await FullProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        using var witnessReader = witness.Reader;
        using var witnessFacilitator = witness.Facilitator;
        var witnessProject = (await witnessReader.GetAsync($"/api/projects/{witness.Id}")).Ok().GetRawText();
        var witnessMap = (await witnessReader.GetAsync($"/api/maps/{witness.RootMapId}")).Ok().GetProperty("map").GetRawText();
        var witnessTexts = (await witnessFacilitator.GetAsync($"/api/projects/{witness.Id}/translations?scope=all")).Ok().GetRawText();
        Assert.Equal(204, (await MoveToTrashAsync(facilitator, project, project.Code)).Status);

        (await facilitator.DeleteAsync($"/api/trash/{project.Id}")).Refused(403, "ACCES_REFUSE");
        Assert.Equal(204, (await admin.DeleteAsync($"/api/trash/{project.Id}")).Status);
        Assert.DoesNotContain(project.Id, await TrashIdsAsync(admin));
        (await admin.PostAsync($"/api/trash/{project.Id}/restore")).Refused(404, "INTROUVABLE");
        (await admin.GetAsync($"/api/maps/{project.RootMapId}")).Refused(404, "INTROUVABLE");

        Assert.Equal(witnessProject, (await witnessReader.GetAsync($"/api/projects/{witness.Id}")).Ok().GetRawText());
        Assert.Equal(witnessMap, (await witnessReader.GetAsync($"/api/maps/{witness.RootMapId}")).Ok().GetProperty("map").GetRawText());
        Assert.Equal(witnessTexts, (await witnessFacilitator.GetAsync($"/api/projects/{witness.Id}/translations?scope=all")).Ok().GetRawText());
        var reused = (await admin.PostAsync("/api/projects", new { code = project.Code, name = "Nouveau" })).Ok(201);
        Assert.Equal(project.Code, reused.GetProperty("code").GetString());
        Assert.NotEqual(project.Id, reused.GetProperty("id").GetGuid());
    }
}
