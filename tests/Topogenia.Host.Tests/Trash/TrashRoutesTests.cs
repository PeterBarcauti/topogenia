// TrashRoutesTests.cs
// The routes of the trash through the whole host on both databases: move a project to the trash, list, restore, purge (annex A 1, A 2.15).

using System.Text.Json;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Trash;

public sealed class TrashRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static async Task MoveToTrashAsync(ApiProject project)
    {
        using var facilitator = project.Facilitator;
        Assert.Equal("", (await facilitator.DeleteAsync($"/api/projects/{project.Id}?confirmation={project.Code}", await project.ProjectVersionAsync())).Body);
    }

    /// <summary>The members of the project without its creator, so that the administrator acts as administrator only.</summary>
    private static async Task WithoutAdministratorAsync(ApiProject project)
    {
        using var facilitator = project.Facilitator;
        var members = (await facilitator.PutAsync($"/api/projects/{project.Id}/members", new object[]
        {
            new { login = Logins.Facilitator, role = "facilitator", is_sponsor = true },
            new { login = Logins.Contributor, role = "contributor", is_sponsor = false },
            new { login = Logins.Reader, role = "reader", is_sponsor = false },
        }, await project.ProjectVersionAsync())).Ok().GetProperty("members");
        Assert.DoesNotContain(Logins.Admin, members.EnumerateArray().Select(member => member.GetProperty("login").GetString()));
    }

    private static JsonElement? Listed(JsonElement trash, ApiProject project) =>
        trash.GetProperty("projects").EnumerateArray().Where(item => item.GetProperty("id").GetGuid() == project.Id).Select(item => (JsonElement?)item).SingleOrDefault();

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_project_moved_to_the_trash_with_its_exact_code_is_listed_with_its_purge_date_then_restored(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;
        using var admin = project.Admin;
        await WithoutAdministratorAsync(project);
        var deleted = await facilitator.DeleteAsync($"/api/projects/{project.Id}?confirmation=%20{project.Code}%20", await project.ProjectVersionAsync());
        Assert.Equal((204, ""), (deleted.Status, deleted.Body));
        (await facilitator.GetAsync($"/api/projects/{project.Id}")).Refused(404, "INTROUVABLE");
        (await admin.GetAsync($"/api/projects/{project.Id}")).Refused(404, "INTROUVABLE");

        var trash = (await facilitator.GetAsync("/api/trash")).Ok();
        var item = Listed(trash, project)!.Value;
        var retention = trash.GetProperty("retention_days").GetInt32();
        Assert.Equal((project.Code, $"Projet {project.Code}", 0), (item.GetProperty("code").GetString(), item.GetProperty("name").GetString(), item.GetProperty("step_count").GetInt32()));
        Assert.False(string.IsNullOrEmpty(item.GetProperty("deleted_by").GetString()));
        Assert.Equal(item.GetProperty("deleted_at").GetDateTime().AddDays(retention), item.GetProperty("purge_at").GetDateTime());
        Assert.Null(Listed((await reader.GetAsync("/api/trash")).Ok(), project));
        Assert.NotNull(Listed((await admin.GetAsync("/api/trash")).Ok(), project));

        var restored = await facilitator.PostAsync($"/api/trash/{project.Id}/restore");
        Assert.Equal((204, ""), (restored.Status, restored.Body));
        Assert.Equal(project.Code, (await reader.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("code").GetString());
        Assert.Null(Listed((await facilitator.GetAsync("/api/trash")).Ok(), project));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_move_to_the_trash_needs_the_exact_code_and_a_facilitator(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;
        using var stranger = project.Stranger;
        var url = $"/api/projects/{project.Id}";
        var version = await project.ProjectVersionAsync();
        Assert.Equal(project.Code, (await facilitator.DeleteAsync(url, version)).Refused(400, "CONFIRMATION_INVALIDE").GetProperty("code").GetString());
        (await facilitator.DeleteAsync($"{url}?confirmation={project.Code.ToLowerInvariant()}", version)).Refused(400, "CONFIRMATION_INVALIDE");
        (await contributor.DeleteAsync($"{url}?confirmation={project.Code}")).Refused(403, "ACCES_REFUSE");
        (await stranger.DeleteAsync($"{url}?confirmation={project.Code}")).Refused(404, "INTROUVABLE");
        Assert.Equal(project.Code, (await facilitator.GetAsync(url)).Ok().GetProperty("code").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Only_a_facilitator_of_the_deleted_project_or_an_administrator_restores_it_and_a_live_project_is_not_found(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var contributor = project.Contributor;
        using var stranger = project.Stranger;
        using var admin = project.Admin;
        var url = $"/api/trash/{project.Id}/restore";
        (await admin.PostAsync(url)).Refused(404, "INTROUVABLE");
        await WithoutAdministratorAsync(project);
        await MoveToTrashAsync(project);
        (await contributor.PostAsync(url)).Refused(403, "ACCES_REFUSE");
        (await stranger.PostAsync(url)).Refused(404, "INTROUVABLE");
        Assert.Equal(204, (await admin.PostAsync(url)).Status);
        Assert.Equal(project.Code, (await contributor.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("code").GetString());
        (await admin.PostAsync(url)).Refused(404, "INTROUVABLE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Only_an_administrator_purges_a_project_of_the_trash_at_once_and_never_a_live_one(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        var url = $"/api/trash/{project.Id}";
        (await admin.DeleteAsync(url)).Refused(404, "INTROUVABLE");
        Assert.Equal(project.Code, (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("code").GetString());
        await MoveToTrashAsync(project);
        (await facilitator.DeleteAsync(url)).Refused(403, "ACCES_REFUSE");
        Assert.NotNull(Listed((await facilitator.GetAsync("/api/trash")).Ok(), project));

        var purged = await admin.DeleteAsync(url);
        Assert.Equal((204, ""), (purged.Status, purged.Body));
        Assert.Null(Listed((await admin.GetAsync("/api/trash")).Ok(), project));
        (await admin.PostAsync($"{url}/restore")).Refused(404, "INTROUVABLE");
        (await admin.DeleteAsync(url)).Refused(404, "INTROUVABLE");
    }
}
