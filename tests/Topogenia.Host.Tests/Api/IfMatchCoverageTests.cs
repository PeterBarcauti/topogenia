// IfMatchCoverageTests.cs
// The routes that read If-Match since the porte P4 (M2): 428 without it in server mode, a stale version a conflict, the version read accepted, optional in desktop mode.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Api;

/// <summary>
/// One case per route and engine: SQL Server runs the server mode, SQLite the desktop mode (`ApiHost`). Each case reads
/// the version of the object of the route, has it changed by the facilitator (so the version read is stale), then sends
/// the same request as the facilitator: in server mode without `If-Match` (428 VERSION_REQUISE, nothing written), with the
/// stale version (409 CONFLIT_VERSION, nothing written), then with the current version (done); in desktop mode with the
/// stale version (the version sent is still checked), then without `If-Match` (done, AR-07).
/// </summary>
public sealed class IfMatchCoverageTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    /// <summary>The request of the route with a version, or without `If-Match`.</summary>
    private delegate Task<ApiResponse> Send(int? version);

    private static async Task ProveAsync(EngineKind kind, Send send, int stale, Func<Task<int>> current, Func<Task<bool>> done)
    {
        var server = kind == EngineKind.SqlServer;
        if (server)
        {
            (await send(null)).Refused(428, "VERSION_REQUISE");
            Assert.False(await done());
        }
        Assert.Equal("anne", (await send(stale)).Refused(409, "CONFLIT_VERSION").GetProperty("by").GetString());
        Assert.False(await done());
        var reply = await send(server ? await current() : null);
        Assert.True(reply.Status is 200 or 204, $"Expected a success, got {reply.Status}: {reply.Body}");
        Assert.True(await done());
    }

    private static bool Has(JsonElement map, string collection, Guid id) =>
        map.GetProperty(collection).EnumerateArray().Any(item => item.GetProperty("id").GetGuid() == id);

    private static int LaneVersion(JsonElement map, Guid id) =>
        map.GetProperty("lanes").EnumerateArray().Single(lane => lane.GetProperty("id").GetGuid() == id).GetProperty("version").GetInt32();

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Deleting_a_lane_needs_the_version_read_of_the_lane(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var lane = await project.AddLaneAsync("Achats");
        var stale = LaneVersion(await project.MapAsync(), lane);
        (await facilitator.PatchAsync($"/api/lanes/{lane}", new { name = "Ventes" }, stale)).Ok();
        await ProveAsync(kind, version => facilitator.DeleteAsync($"/api/lanes/{lane}", version), stale,
            async () => LaneVersion(await project.MapAsync(), lane), async () => !Has(await project.MapAsync(), "lanes", lane));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Deleting_a_step_needs_the_version_read_of_the_step(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var step = (await project.AddStepAsync(0, "Commander")).Id;
        var stale = await project.StepVersionAsync(step);
        (await facilitator.PatchAsync($"/api/steps/{step}", new { label = "Commander vite" }, stale)).Ok();
        await ProveAsync(kind, version => facilitator.DeleteAsync($"/api/steps/{step}", version), stale,
            () => project.StepVersionAsync(step), async () => !Has(await project.MapAsync(), "steps", step));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Translating_a_step_needs_the_version_read_of_the_step(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var step = (await project.AddStepAsync(0, "Commander")).Id;
        var stale = await project.StepVersionAsync(step);
        (await facilitator.PatchAsync($"/api/steps/{step}", new { label = "Commander vite" }, stale)).Ok();
        await ProveAsync(kind, version => facilitator.PutAsync($"/api/steps/{step}/translations", new { translations = new { en = "Order" } }, version), stale,
            () => project.StepVersionAsync(step),
            async () => StructureCalls.StepOf(await project.MapAsync(), step).GetProperty("translations").TryGetProperty("en", out _));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Deleting_a_flow_needs_the_version_read_of_the_flow(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var flow = await project.AddFlowAsync((await project.AddStepAsync(0, "Commander")).Id, (await project.AddStepAsync(1, "Payer")).Id, "oui");
        async Task<int> VersionAsync() => StructureCalls.FlowOf(await project.MapAsync(), flow).GetProperty("version").GetInt32();
        var stale = await VersionAsync();
        (await facilitator.PatchAsync($"/api/flows/{flow}", new { label = "non" }, stale)).Ok();
        await ProveAsync(kind, version => facilitator.DeleteAsync($"/api/flows/{flow}", version), stale, VersionAsync,
            async () => !Has(await project.MapAsync(), "flows", flow));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Reversing_a_flow_needs_the_version_read_of_the_flow(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var source = (await project.AddStepAsync(0, "Commander")).Id;
        var flow = await project.AddFlowAsync(source, (await project.AddStepAsync(1, "Payer")).Id, "oui");
        async Task<int> VersionAsync() => StructureCalls.FlowOf(await project.MapAsync(), flow).GetProperty("version").GetInt32();
        var stale = await VersionAsync();
        (await facilitator.PatchAsync($"/api/flows/{flow}", new { label = "non" }, stale)).Ok();
        await ProveAsync(kind, version => facilitator.PostAsync($"/api/flows/{flow}/reverse", null, version), stale, VersionAsync,
            async () => StructureCalls.FlowOf(await project.MapAsync(), flow).GetProperty("target_step_id").GetGuid() == source);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Translating_a_flow_needs_the_version_read_of_the_flow(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var flow = await project.AddFlowAsync((await project.AddStepAsync(0, "Commander")).Id, (await project.AddStepAsync(1, "Payer")).Id, "oui");
        async Task<int> VersionAsync() => StructureCalls.FlowOf(await project.MapAsync(), flow).GetProperty("version").GetInt32();
        var stale = await VersionAsync();
        (await facilitator.PatchAsync($"/api/flows/{flow}", new { label = "non" }, stale)).Ok();
        await ProveAsync(kind, version => facilitator.PutAsync($"/api/flows/{flow}/translations", new { translations = new { en = "no" } }, version), stale,
            VersionAsync, async () => StructureCalls.FlowOf(await project.MapAsync(), flow).GetProperty("translations").TryGetProperty("en", out _));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Deleting_a_document_needs_the_version_read_of_the_document(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var step = await StepSheets.StepAsync(project, 0);
        var document = (await facilitator.PostAsync($"/api/steps/{step}/documents", new { direction = "output", name = "Bon" })).Ok()
            .GetProperty("outputs")[0].GetProperty("document");
        var (id, stale) = (document.GetProperty("id").GetGuid(), document.GetProperty("version").GetInt32());
        var url = $"/api/steps/{step}/documents/{id}";
        (await facilitator.PutAsync(url, new { direction = "output", name = "Bon de commande" }, stale)).Ok();
        async Task<JsonElement> OutputsAsync() => (await facilitator.GetAsync($"/api/steps/{step}/sheet")).Ok().GetProperty("outputs");
        await ProveAsync(kind, version => facilitator.DeleteAsync(url, version), stale,
            async () => (await OutputsAsync())[0].GetProperty("document").GetProperty("version").GetInt32(), async () => (await OutputsAsync()).GetArrayLength() == 0);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Unticking_a_use_needs_the_version_read_of_the_use_and_version_zero_once_there_is_none(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var producer = await StepSheets.StepAsync(project, 0);
        var document = (await facilitator.PostAsync($"/api/steps/{producer}/documents", new { direction = "output", name = "Bon" })).Ok()
            .GetProperty("outputs")[0].GetProperty("document").GetProperty("id").GetGuid();
        var consumer = await StepSheets.StepAsync(project, 1, "Livrer");
        var url = $"/api/steps/{consumer}/uses/{document}";
        async Task<JsonElement> InputsAsync() => (await facilitator.GetAsync($"/api/steps/{consumer}/sheet")).Ok().GetProperty("inputs");
        (await facilitator.PutAsync(url, new { wait_hours = 2 }, 0)).Ok();
        var stale = (await InputsAsync())[0].GetProperty("use").GetProperty("version").GetInt32();
        (await facilitator.PutAsync(url, new { wait_hours = 5 }, stale)).Ok();
        await ProveAsync(kind, version => facilitator.DeleteAsync(url, version), stale,
            async () => (await InputsAsync())[0].GetProperty("use").GetProperty("version").GetInt32(), async () => (await InputsAsync()).GetArrayLength() == 0);
        Assert.Equal(0, (await facilitator.DeleteAsync(url, 0)).Ok().GetProperty("inputs").GetArrayLength());
    }

    /// <summary>AR-111: a use unticked then ticked again is another record; the version read of the first is stale for it.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_use_ticked_again_after_an_untick_refuses_the_version_read_before_the_untick(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var producer = await StepSheets.StepAsync(project, 0);
        var document = (await facilitator.PostAsync($"/api/steps/{producer}/documents", new { direction = "output", name = "Bon" })).Ok()
            .GetProperty("outputs")[0].GetProperty("document").GetProperty("id").GetGuid();
        var consumer = await StepSheets.StepAsync(project, 1, "Livrer");
        var url = $"/api/steps/{consumer}/uses/{document}";
        async Task<JsonElement> InputsAsync() => (await facilitator.GetAsync($"/api/steps/{consumer}/sheet")).Ok().GetProperty("inputs");
        (await facilitator.PutAsync(url, new { wait_hours = 2 }, 0)).Ok();
        var read = (await InputsAsync())[0].GetProperty("use").GetProperty("version").GetInt32();
        (await facilitator.DeleteAsync(url, read)).Ok();
        (await facilitator.PutAsync(url, new { wait_hours = 7 }, 0)).Ok();
        var again = (await InputsAsync())[0].GetProperty("use").GetProperty("version").GetInt32();
        Assert.True(again > read, $"The use ticked again took the version {again}, read before as {read}.");

        (await facilitator.DeleteAsync(url, read)).Refused(409, "CONFLIT_VERSION");
        (await facilitator.PutAsync(url, new { wait_hours = 1 }, read)).Refused(409, "CONFLIT_VERSION");
        Assert.Equal(7m, (await InputsAsync())[0].GetProperty("use").GetProperty("wait_hours").GetDecimal());
        Assert.Equal(0, (await facilitator.DeleteAsync(url, again)).Ok().GetProperty("inputs").GetArrayLength());
    }

    private static object[] MembersBody(string readerRole) =>
    [
        new { login = Logins.Facilitator, role = "facilitator", is_sponsor = true },
        new { login = Logins.Contributor, role = "contributor", is_sponsor = false },
        new { login = Logins.Reader, role = readerRole, is_sponsor = false },
    ];

    private static async Task<JsonElement> DetailAsync(ApiProject project)
    {
        using var facilitator = project.Facilitator;
        return (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok();
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Replacing_the_members_needs_the_version_read_of_the_project_which_the_replacement_moves_on(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var url = $"/api/projects/{project.Id}/members";
        var stale = (await DetailAsync(project)).GetProperty("version").GetInt32();
        (await facilitator.PutAsync(url, MembersBody("contributor"), stale)).Ok();
        Assert.True((await DetailAsync(project)).GetProperty("version").GetInt32() > stale);
        async Task<bool> DoneAsync() => (await DetailAsync(project)).GetProperty("members").EnumerateArray()
            .Single(member => member.GetProperty("login").GetString() == Logins.Reader).GetProperty("role").GetString() == "reader";
        await ProveAsync(kind, version => facilitator.PutAsync(url, MembersBody("reader"), version), stale,
            async () => (await DetailAsync(project)).GetProperty("version").GetInt32(), DoneAsync);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Changing_the_classification_needs_the_version_read_of_the_project(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var stale = (await DetailAsync(project)).GetProperty("version").GetInt32();
        (await facilitator.PutAsync($"/api/projects/{project.Id}/sharing-group-label", new { label = "Achats" }, stale)).Ok();
        await ProveAsync(kind, version => facilitator.PutAsync($"/api/projects/{project.Id}/classification", new { classification = "confidential" }, version), stale,
            async () => (await DetailAsync(project)).GetProperty("version").GetInt32(),
            async () => (await DetailAsync(project)).GetProperty("classification").GetString() == "confidential");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Moving_a_project_to_the_trash_needs_the_version_read_of_the_project(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var stale = (await DetailAsync(project)).GetProperty("version").GetInt32();
        (await facilitator.PutAsync($"/api/projects/{project.Id}/sharing-group-label", new { label = "Achats" }, stale)).Ok();
        await ProveAsync(kind, version => facilitator.DeleteAsync($"/api/projects/{project.Id}?confirmation={project.Code}", version), stale,
            async () => (await DetailAsync(project)).GetProperty("version").GetInt32(),
            async () => (await facilitator.GetAsync($"/api/projects/{project.Id}")).Status == 404);
    }
}
