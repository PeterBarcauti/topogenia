// PainPointStatusRoutesTests.cs
// The status of a pain point chosen through the whole host on both databases (ADR 116): the right to fill the receiving sheet for each role, the refusals, If-Match required in server mode, two writes of one version, an unknown status, the reply, nothing logged nor audited.

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Documents;

public sealed class PainPointStatusRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private static string Status(Guid stepId, Guid painPointId) => $"/api/steps/{stepId}/pain-points/{painPointId}/status";

    /// <summary>
    /// The contributor holds the first lane; a second lane is held by nobody. « Saisie en double » is raised on a step of the
    /// second lane and received by a step of the first one (the contributor fills its sheet); « Relance » the other way round.
    /// </summary>
    private sealed record Scene(ApiProject Project, Guid Held, Guid Free, Guid Double, Guid Relance);

    private async Task<Scene> SceneAsync(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        await StepSheets.MakeContributorActorAsync(project);
        using var facilitator = project.Facilitator;
        var other = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Comptabilité" })).Ok().GetProperty("id").GetGuid();
        var held = await StepSheets.StepAsync(project, 0, "Saisir");
        var free = await StepSheets.StepAsync(project, 1, "Extraire", laneId: other);
        var twice = await RaiseAsync(facilitator, free, held, "Saisie en double", 1.5m);
        var relance = await RaiseAsync(facilitator, held, free, "Relance", 2m);
        return new Scene(project, held, free, twice, relance);
    }

    /// <summary>An output of the producing step used by the receiving step with a pain point; returns the pain point.</summary>
    private static async Task<Guid> RaiseAsync(ApiClient facilitator, Guid producer, Guid receiver, string painPoint, decimal hours)
    {
        var sheet = (await facilitator.PostAsync($"/api/steps/{producer}/documents", new { direction = "output", name = $"Sortie {painPoint}" })).Ok();
        var document = sheet.GetProperty("outputs").EnumerateArray().Last().GetProperty("document").GetProperty("id").GetGuid();
        var used = (await facilitator.PutAsync($"/api/steps/{receiver}/uses/{document}", new { pain_point = painPoint, lost_hours_per_month = hours }, StepSheets.NoRecord)).Ok();
        return used.GetProperty("pain_points").EnumerateArray().Single(point => point.GetProperty("description").GetString() == painPoint).GetProperty("id").GetGuid();
    }

    /// <summary>The pain point as the sheet of its receiving step shows it.</summary>
    private static async Task<JsonElement> PointAsync(ApiProject project, Guid receiver, Guid painPoint)
    {
        using var facilitator = project.Facilitator;
        return (await facilitator.GetAsync($"/api/steps/{receiver}/sheet")).Ok().GetProperty("pain_points").EnumerateArray()
            .Single(point => point.GetProperty("id").GetGuid() == painPoint);
    }

    private static async Task<int> VersionAsync(ApiProject project, Guid receiver, Guid painPoint) =>
        (await PointAsync(project, receiver, painPoint)).GetProperty("version").GetInt32();

    private static async Task<string?> StatusAsync(ApiProject project, Guid receiver, Guid painPoint) =>
        (await PointAsync(project, receiver, painPoint)).GetProperty("status").GetString();

    private static async Task<List<PainPointStatusChange>> HistoryAsync(ApiProject project, Guid painPoint)
    {
        await using var scope = project.Host.Services.CreateAsyncScope();
        var workspace = (await scope.ServiceProvider.GetRequiredService<IWorkspaceStore>().LoadAsync(project.Id, WorkspaceContent.Documents, TestContext.Current.CancellationToken))!;
        return [.. workspace.All<PainPointStatusChange>().Where(change => change.PainPointId == painPoint).OrderBy(change => change.ChangedAt)
            .ThenBy(change => change.StatusBefore is not null)];
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Each_role_changes_the_status_as_the_right_to_fill_the_receiving_sheet_says(EngineKind kind)
    {
        var scene = await SceneAsync(kind);
        var project = scene.Project;
        async Task<ApiResponse> ChangeAsync(ApiClient client, Guid receiver, Guid point, string status)
        {
            using (client)
                return await client.PutAsync(Status(receiver, point), new { status }, await VersionAsync(project, receiver, point));
        }

        (await ChangeAsync(project.Reader, scene.Held, scene.Double, "implemented")).Refused(403, "ACCES_REFUSE");
        (await ChangeAsync(project.Reader, scene.Free, scene.Relance, "implemented")).Refused(403, "ACCES_REFUSE");
        (await ChangeAsync(project.Contributor, scene.Free, scene.Relance, "implemented")).Refused(403, "ACCES_REFUSE");
        Assert.Equal(("reported", "reported"), (await StatusAsync(project, scene.Held, scene.Double), await StatusAsync(project, scene.Free, scene.Relance)));

        (await ChangeAsync(project.Contributor, scene.Held, scene.Double, "in_progress")).Ok();
        Assert.Equal("in_progress", await StatusAsync(project, scene.Held, scene.Double));
        (await ChangeAsync(project.Facilitator, scene.Free, scene.Relance, "implemented")).Ok();
        Assert.Equal("implemented", await StatusAsync(project, scene.Free, scene.Relance));
        (await ChangeAsync(project.Admin, scene.Free, scene.Relance, "dropped")).Ok();
        Assert.Equal("dropped", await StatusAsync(project, scene.Free, scene.Relance));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_stranger_a_project_in_the_trash_and_a_pain_point_of_another_step_get_not_found(EngineKind kind)
    {
        var scene = await SceneAsync(kind);
        var project = scene.Project;
        var version = await VersionAsync(project, scene.Held, scene.Double);
        using var stranger = project.Stranger;
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        var body = new { status = "implemented" };
        (await stranger.PutAsync(Status(scene.Held, scene.Double), body, version)).Refused(404, "INTROUVABLE");
        // The producing step, another step, an unknown pain point: not told from one another.
        (await facilitator.PutAsync(Status(scene.Free, scene.Double), body, version)).Refused(404, "INTROUVABLE");
        var other = await StepSheets.StepAsync(project, 2, "Classer");
        (await facilitator.PutAsync(Status(other, scene.Double), body, version)).Refused(404, "INTROUVABLE");
        (await facilitator.PutAsync(Status(scene.Held, Guid.NewGuid()), body, StepSheets.NoRecord)).Refused(404, "INTROUVABLE");
        Assert.Equal("reported", await StatusAsync(project, scene.Held, scene.Double));

        (await facilitator.DeleteAsync($"/api/projects/{project.Id}?confirmation={project.Code}", await project.ProjectVersionAsync())).Ok(204);
        foreach (var member in new[] { facilitator, admin })
            (await member.PutAsync(Status(scene.Held, scene.Double), body, version)).Refused(404, "INTROUVABLE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task In_server_mode_a_change_without_If_Match_is_refused_428(EngineKind kind)
    {
        var scene = await SceneAsync(kind);
        using var facilitator = scene.Project.Facilitator;
        var withoutVersion = await facilitator.PutAsync(Status(scene.Held, scene.Double), new { status = "implemented" });
        if (kind == EngineKind.SqlServer)
        {
            withoutVersion.Refused(428, "VERSION_REQUISE");
            Assert.Equal("reported", await StatusAsync(scene.Project, scene.Held, scene.Double));
            Assert.Single(await HistoryAsync(scene.Project, scene.Double));
        }
        else
        {
            withoutVersion.Ok();
            Assert.Equal("implemented", await StatusAsync(scene.Project, scene.Held, scene.Double));
        }
    }

    /// <summary>Two people read the same version and choose a status at the same time: the lock of the project orders them, the second is refused.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Two_changes_from_the_same_version_one_is_written_the_other_refused_409_and_the_history_holds_one_record(EngineKind kind)
    {
        var scene = await SceneAsync(kind);
        var project = scene.Project;
        var version = await VersionAsync(project, scene.Held, scene.Double);
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;

        var replies = await Task.WhenAll(
            facilitator.PutAsync(Status(scene.Held, scene.Double), new { status = "implemented" }, version),
            contributor.PutAsync(Status(scene.Held, scene.Double), new { status = "dropped" }, version));

        Assert.Single(replies, reply => reply.Status == 200);
        var refused = Assert.Single(replies, reply => reply.Status == 409);
        Assert.NotNull(refused.Refused(409, "CONFLIT_VERSION").GetProperty("by").GetString());
        var chosen = replies[0].Status == 200 ? "implemented" : "dropped";
        Assert.Equal(chosen, await StatusAsync(project, scene.Held, scene.Double));
        Assert.Equal(version + 1, await VersionAsync(project, scene.Held, scene.Double));
        var history = await HistoryAsync(project, scene.Double);
        Assert.Equal([(null, PainPointStatus.Reported), (PainPointStatus.Reported, Codes.Parse<PainPointStatus>(chosen, "status"))],
            history.Select(change => (change.StatusBefore, change.StatusAfter)));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_unknown_status_is_refused_400_and_nothing_changes(EngineKind kind)
    {
        var scene = await SceneAsync(kind);
        var version = await VersionAsync(scene.Project, scene.Held, scene.Double);
        using var facilitator = scene.Project.Facilitator;
        var url = Status(scene.Held, scene.Double);
        static string? Field(JsonElement details) => details.GetProperty("field").GetString();
        Assert.Equal("status", Field((await facilitator.PutAsync(url, new { status = "closed" }, version)).Refused(400, "VALEUR_INVALIDE")));
        Assert.Equal("status", Field((await facilitator.PutAsync(url, new { status = "Implemented" }, version)).Refused(400, "VALEUR_INVALIDE")));
        Assert.Equal("status", Field((await facilitator.PutAsync(url, new { }, version)).Refused(400, "VALEUR_INVALIDE")));
        (await facilitator.PutAsync(url, new { status = 4 }, version)).Refused(400, "REQUETE_INVALIDE");
        Assert.Equal(("reported", version), (await StatusAsync(scene.Project, scene.Held, scene.Double), await VersionAsync(scene.Project, scene.Held, scene.Double)));
        Assert.Single(await HistoryAsync(scene.Project, scene.Double));
    }

    /// <summary>The reply is the sheet of the receiving step read again, as every write of a document; the plan reads the change after it.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_reply_is_the_sheet_of_the_receiving_step(EngineKind kind)
    {
        var scene = await SceneAsync(kind);
        var project = scene.Project;
        var version = await VersionAsync(project, scene.Held, scene.Double);
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;

        var reply = await facilitator.PutAsync(Status(scene.Held, scene.Double), new { status = "implemented" }, version);

        var sheet = reply.Ok();
        Assert.Equal(scene.Held, sheet.GetProperty("step_id").GetGuid());
        var point = sheet.GetProperty("pain_points").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == scene.Double);
        Assert.Equal(("implemented", version + 1), (point.GetProperty("status").GetString(), point.GetProperty("version").GetInt32()));
        Assert.Equal((await facilitator.GetAsync($"/api/steps/{scene.Held}/sheet")).Body, reply.Body);

        var plan = (await reader.GetAsync($"/api/maps/{project.RootMapId}/action-plan")).Ok();
        var line = plan.GetProperty("lines").EnumerateArray().Single(item => item.GetProperty("pain_point_id").GetGuid() == scene.Double);
        Assert.Equal(("implemented", false, version + 1), (line.GetProperty("status").GetString(), line.GetProperty("open").GetBoolean(), line.GetProperty("version").GetInt32()));
        var changedAt = line.GetProperty("status_changed_at").GetDateTime();
        Assert.Equal(DateTimeKind.Utc, changedAt.Kind);
        Assert.Equal((await HistoryAsync(project, scene.Double))[^1].ChangedAt, changedAt, TimeSpan.FromMilliseconds(1));
        Assert.Equal("2", plan.GetProperty("open_lost_hours_per_month").GetRawText());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_change_writes_no_operation_of_the_map_nor_event_of_audit(EngineKind kind)
    {
        var scene = await SceneAsync(kind);
        var project = scene.Project;
        using var admin = project.Admin;
        using var facilitator = project.Facilitator;
        async Task<string> TracesAsync() => string.Join("\n",
            (await admin.GetAsync($"/api/admin/audit?project_id={project.Id}&limit=1000")).Ok().GetRawText(),
            (await facilitator.GetAsync($"/api/maps/{project.RootMapId}/history")).Ok().GetRawText());
        var before = await TracesAsync();

        (await facilitator.PutAsync(Status(scene.Held, scene.Double), new { status = "implemented" }, await VersionAsync(project, scene.Held, scene.Double))).Ok();
        (await facilitator.PutAsync(Status(scene.Held, scene.Double), new { status = "reported" }, await VersionAsync(project, scene.Held, scene.Double))).Ok();

        Assert.Equal(before, await TracesAsync());
        Assert.Equal(3, (await HistoryAsync(project, scene.Double)).Count);
    }
}
