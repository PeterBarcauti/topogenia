// CommentRoundTripTests.cs
// The comments of presentation through the whole host on both databases (ADR 099): those of the steps and those of the flows carried by the package of a project, written again by its import and by a target state.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Exchange;

public sealed class CommentRoundTripTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static async Task<List<JsonElement>> CommentsAsync(ApiClient client, Guid mapId) =>
        [.. (await client.GetAsync($"/api/maps/{mapId}/presentation-comments")).Ok().EnumerateArray()];

    /// <summary>The code of each commented step of a map, with the text, the language and the source of each line of its comment, in the order of the codes.</summary>
    private static async Task<List<(string Code, string Text, string Language, string Source)>> SaidAsync(ApiClient client, Guid mapId)
    {
        var map = (await client.GetAsync($"/api/maps/{mapId}")).Ok().GetProperty("map");
        var codes = map.GetProperty("steps").EnumerateArray().ToDictionary(step => step.GetProperty("id").GetGuid(), step => step.GetProperty("code").GetString()!);
        return [.. (await CommentsAsync(client, mapId))
            .Select(comment => (codes[comment.GetProperty("step_id").GetGuid()][^4..], comment.GetProperty("text").GetString()!, comment.GetProperty("language").GetString()!,
                comment.GetProperty("source").GetString()!))
            .Order()];
    }

    private static async Task<ApiProject> CommentedAsync(ApiProject project)
    {
        using var facilitator = project.Facilitator;
        var first = (await project.AddStepAsync(0, "Demander")).Id;
        var second = (await project.AddStepAsync(1, "Valider")).Id;
        (await facilitator.PutAsync($"/api/steps/{first}/presentation-comment", new { text = "Le salarié saisit sa demande.", language = "fr" }, 0)).Ok();
        (await facilitator.PutAsync($"/api/steps/{second}/presentation-comment", new { text = "The manager approves it.", language = "en" }, 0)).Ok();
        // A translation brought by a workbook, beside the original of the first step and the translation without original of the second.
        (await facilitator.PutAsync($"/api/projects/{project.Id}/translations", new
        {
            scope = "missing",
            rows = new[] { new { line = 2, type = "presentation_comment", id = first, original = "Le salarié saisit sa demande.", translations = new { de = "Der Mitarbeiter erfasst seinen Antrag." } } },
        })).Ok();
        return project;
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_package_carries_the_comments_and_its_import_writes_them_on_the_new_steps(EngineKind kind)
    {
        var project = await CommentedAsync(await ProjectAsync(kind));
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        var said = await SaidAsync(facilitator, project.RootMapId);
        Assert.Equal([("E001", "Der Mitarbeiter erfasst seinen Antrag.", "de", "workbook"), ("E001", "Le salarié saisit sa demande.", "fr", "manual"),
            ("E002", "The manager approves it.", "en", "manual")], said);
        var package = (await facilitator.PostAsync($"/api/projects/{project.Id}/export", new { without_people = true })).Bytes;

        var imported = (await admin.SendAsync(HttpMethod.Post, $"/api/projects/import?code={ApiProject.NewCode()}&with_members=false", package, null)).Ok(201);

        var root = imported.GetProperty("root_map_id").GetGuid();
        Assert.Equal(said, await SaidAsync(admin, root));
        Assert.All(await CommentsAsync(admin, root), comment => Assert.Equal(1, comment.GetProperty("version").GetInt32()));
        // The source keeps its own: the import wrote new comments, it moved none.
        Assert.Equal(said, await SaidAsync(facilitator, project.RootMapId));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_target_state_takes_the_comments_of_its_source(EngineKind kind)
    {
        var project = await CommentedAsync(await ProjectAsync(kind));
        using var admin = project.Admin;

        var target = (await admin.PostAsync($"/api/projects/{project.Id}/target", new { code = ApiProject.NewCode(), name = "État cible", label = "État actuel" })).Ok(201);

        Assert.Equal(await SaidAsync(admin, project.RootMapId), await SaidAsync(admin, target.GetProperty("root_map_id").GetGuid()));
    }

    /// <summary>
    /// The ends of each commented flow of a map, by the codes of its steps, with the text, the language, the source and the
    /// version of each line of its comment, in order.
    /// </summary>
    private static async Task<List<(string Source, string Target, string Text, string Language, string Origin, int Version)>> SaidOnFlowsAsync(ApiClient client, Guid mapId)
    {
        var map = (await client.GetAsync($"/api/maps/{mapId}")).Ok().GetProperty("map");
        var codes = map.GetProperty("steps").EnumerateArray().ToDictionary(step => step.GetProperty("id").GetGuid(), step => step.GetProperty("code").GetString()![^4..]);
        var ends = map.GetProperty("flows").EnumerateArray().ToDictionary(flow => flow.GetProperty("id").GetGuid(),
            flow => (Source: codes[flow.GetProperty("source_step_id").GetGuid()], Target: codes[flow.GetProperty("target_step_id").GetGuid()]));
        return [.. (await client.GetAsync($"/api/maps/{mapId}/flow-presentation-comments")).Ok().EnumerateArray()
            .Select(comment => (ends[comment.GetProperty("flow_id").GetGuid()].Source, ends[comment.GetProperty("flow_id").GetGuid()].Target,
                comment.GetProperty("text").GetString()!, comment.GetProperty("language").GetString()!, comment.GetProperty("source").GetString()!,
                comment.GetProperty("version").GetInt32()))
            .Order()];
    }

    /// <summary>
    /// A project with three steps in a row: the first flow with an original and a translation typed, written twice; the
    /// second one with a translation alone; a step commented too, and a flow without any comment.
    /// </summary>
    private static async Task<ApiProject> WithFlowCommentsAsync(ApiProject project)
    {
        using var facilitator = project.Facilitator;
        Guid[] steps = [(await project.AddStepAsync(0, "Demander")).Id, (await project.AddStepAsync(1, "Valider")).Id, (await project.AddStepAsync(2, "Archiver")).Id,
            (await project.AddStepAsync(3, "Clore")).Id];
        var first = await project.AddFlowAsync(steps[0], steps[1]);
        var second = await project.AddFlowAsync(steps[1], steps[2]);
        await project.AddFlowAsync(steps[2], steps[3]);
        (await facilitator.PutAsync($"/api/flows/{first}/presentation-comment", new { text = "La demande part.", language = "fr" }, 0)).Ok();
        (await facilitator.PutAsync($"/api/flows/{first}/presentation-comment", new { text = "La demande part chez le responsable.", language = "fr" }, 1)).Ok();
        (await facilitator.PutAsync($"/api/flows/{first}/presentation-comment", new { text = "The request goes to the manager.", language = "en" }, 0)).Ok();
        (await facilitator.PutAsync($"/api/flows/{second}/presentation-comment", new { text = "Approved requests are filed.", language = "en" }, 0)).Ok();
        (await facilitator.PutAsync($"/api/steps/{steps[0]}/presentation-comment", new { text = "Le salarié saisit sa demande.", language = "fr" }, 0)).Ok();
        return project;
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_package_carries_the_comments_of_the_flows_and_its_import_writes_them_on_the_new_flows(EngineKind kind)
    {
        var project = await WithFlowCommentsAsync(await ProjectAsync(kind));
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        var said = await SaidOnFlowsAsync(facilitator, project.RootMapId);
        Assert.Equal([("E001", "E002", "La demande part chez le responsable.", "fr", "manual", 2), ("E001", "E002", "The request goes to the manager.", "en", "manual", 1),
            ("E002", "E003", "Approved requests are filed.", "en", "manual", 1)], said);
        var package = (await facilitator.PostAsync($"/api/projects/{project.Id}/export", new { without_people = true })).Bytes;

        var imported = (await admin.SendAsync(HttpMethod.Post, $"/api/projects/import?code={ApiProject.NewCode()}&with_members=false", package, null)).Ok(201);

        var root = imported.GetProperty("root_map_id").GetGuid();
        // The same lines on the flows of the new project, each in version 1: the package carries no version.
        Assert.Equal(said.Select(line => line with { Version = 1 }), await SaidOnFlowsAsync(admin, root));
        Assert.Equal([("E001", "Le salarié saisit sa demande.", "fr", "manual")], await SaidAsync(admin, root));
        // The source keeps its own: the import wrote new comments, it moved none.
        Assert.Equal(said, await SaidOnFlowsAsync(facilitator, project.RootMapId));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_target_state_takes_the_comments_of_the_flows_of_its_source(EngineKind kind)
    {
        var project = await WithFlowCommentsAsync(await ProjectAsync(kind));
        using var admin = project.Admin;

        var target = (await admin.PostAsync($"/api/projects/{project.Id}/target", new { code = ApiProject.NewCode(), name = "État cible", label = "État actuel" })).Ok(201);

        var root = target.GetProperty("root_map_id").GetGuid();
        Assert.Equal((await SaidOnFlowsAsync(admin, project.RootMapId)).Select(line => line with { Version = 1 }), await SaidOnFlowsAsync(admin, root));
        Assert.Equal(await SaidAsync(admin, project.RootMapId), await SaidAsync(admin, root));
    }

    /// <summary>A replacement by a package writes the comments of the flows of the new project; the replaced one goes to the trash with its own.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_project_replaced_by_its_own_package_has_the_comments_of_its_flows_again(EngineKind kind)
    {
        var project = await WithFlowCommentsAsync(await ProjectAsync(kind));
        using var facilitator = project.Facilitator;
        var said = await SaidOnFlowsAsync(facilitator, project.RootMapId);
        var package = (await facilitator.PostAsync($"/api/projects/{project.Id}/export", new { without_people = true })).Bytes;
        var version = (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("version").GetInt32();

        var replaced = (await facilitator.SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/import?with_members=false", package, version)).Ok(201);

        Assert.Equal(said.Select(line => line with { Version = 1 }), await SaidOnFlowsAsync(facilitator, replaced.GetProperty("root_map_id").GetGuid()));
        // The replaced project is in the trash: its map is no longer read, its comments wait there with its flows.
        (await facilitator.GetAsync($"/api/maps/{project.RootMapId}/flow-presentation-comments")).Refused(404, "INTROUVABLE");
    }
}
