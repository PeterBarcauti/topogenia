// ProjectPdfCopyRoutesTests.cs
// The copy the project as PDF reads, `GET /api/maps/{id}/pmap?purpose=pdf`, through the whole host on both databases (3.4, lot 5, ADR 113): the sheets each role may see, neither place of a document nor comment, one event of export with the format pdf, the refusals.

using System.Text.Json;
using Topogenia.Application.Audit;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Host.Tests.Ported.SheetPortingCalls;

namespace Topogenia.Host.Tests.Exchange;

public sealed class ProjectPdfCopyRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    /// <summary>The place of a document of the scene, which the copy for a pdf never names.</summary>
    private const string Place = "https://intranet.example/bons";

    private static string Copy(Guid mapId, string purpose = "pdf") => $"/api/maps/{mapId}/pmap?purpose={purpose}";

    /// <summary>
    /// The contributor holds the first lane of the root map. Three sheets are validated then changed in their drafts: the
    /// one of the step of the held lane, one of a free lane, and one on the map detailed below a fourth step; a fifth step
    /// has a draft never validated. The step of the held lane produces a document with a place and carries a comment of
    /// presentation.
    /// </summary>
    private async Task<ApiProject> SceneAsync(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        await StepSheets.MakeContributorActorAsync(project);
        var other = await project.AddLaneAsync("Comptabilité");
        var held = await StepSheets.StepAsync(project, 0, "Saisir");
        var free = await StepSheets.StepAsync(project, 1, "Extraire", laneId: other);
        var never = await StepSheets.StepAsync(project, 2, "Relancer", laneId: other);
        var macro = await StepSheets.StepAsync(project, 3, "Archiver", laneId: other);
        var (subMap, subLane) = await project.DetailAsync(macro);
        var deep = (await project.AddStepAsync(0, "Contrôler", subLane, subMap)).Id;
        using var facilitator = project.Facilitator;
        foreach (var (step, name) in new[] { (held, "Saisie"), (free, "Extraction"), (deep, "Contrôle") })
        {
            (await SaveAsync(facilitator, step, StepSheets.Purpose($"{name} validée"))).Ok();
            (await ValidateAsync(facilitator, step)).Ok();
            (await SaveAsync(facilitator, step, StepSheets.Purpose($"{name} en cours"))).Ok();
        }
        (await SaveAsync(facilitator, never, StepSheets.Purpose("Relance en cours"))).Ok();
        (await facilitator.PostAsync($"/api/steps/{held}/documents", new { direction = "output", name = "Bon de saisie", location = Place })).Ok();
        (await facilitator.PutAsync($"/api/steps/{held}/presentation-comment", new { text = "Ici tout commence.", language = "fr" }, StepSheets.NoRecord)).Ok();
        return project;
    }

    /// <summary>The purpose of the sheet of each step of the copy and of its sub-maps, by label; null for a step without sheet or without purpose.</summary>
    private static Dictionary<string, string?> Purposes(string body)
    {
        var seen = new Dictionary<string, string?>(StringComparer.Ordinal);
        void Walk(JsonElement map)
        {
            foreach (var step in map.GetProperty("steps").EnumerateArray())
            {
                seen[step.GetProperty("label").GetString()!] =
                    step.TryGetProperty("sheet", out var sheet) && sheet.TryGetProperty("purpose", out var purpose) ? purpose.GetString() : null;
                if (step.TryGetProperty("sub_map", out var sub))
                    Walk(sub);
            }
        }
        using var document = JsonDocument.Parse(body);
        Walk(document.RootElement.GetProperty("map"));
        return seen;
    }

    private static async Task<List<JsonElement>> ExportsAsync(ApiProject project)
    {
        using var admin = project.Admin;
        return [.. (await admin.GetAsync($"/api/admin/audit?project_id={project.Id}&kind={AuditKinds.ProjectExported}&limit=100")).Ok().EnumerateArray()];
    }

    /// <summary>
    /// The table of the rights of the cadrage (section 3.8): the reader reads the validated versions only, never a draft
    /// and no sheet never validated; the contributor the draft of the lane he holds and the validated version elsewhere,
    /// the detailed map included; the facilitator and the administrator every draft. None reads a place or a comment, and
    /// no file is named: the text is the source of the PDF, not a file to keep.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Each_role_reads_the_copy_for_a_pdf_with_the_sheets_it_may_see(EngineKind kind)
    {
        var project = await SceneAsync(kind);
        var drafts = new Dictionary<string, string?>
        {
            ["Saisir"] = "Saisie en cours", ["Extraire"] = "Extraction en cours", ["Relancer"] = "Relance en cours", ["Archiver"] = null,
            ["Contrôler"] = "Contrôle en cours",
        };
        var expected = new (ApiClient Client, Dictionary<string, string?> Purposes)[]
        {
            (project.Reader, new(drafts) { ["Saisir"] = "Saisie validée", ["Extraire"] = "Extraction validée", ["Relancer"] = null, ["Contrôler"] = "Contrôle validée" }),
            (project.Contributor, new(drafts) { ["Extraire"] = "Extraction validée", ["Relancer"] = null, ["Contrôler"] = "Contrôle validée" }),
            (project.Facilitator, drafts),
            (project.Admin, drafts),
        };
        foreach (var (client, purposes) in expected)
        {
            using var caller = client;
            var reply = await caller.GetAsync(Copy(project.RootMapId));

            Assert.True(reply.Status == 200, reply.Body);
            Assert.Equal(purposes.OrderBy(pair => pair.Key, StringComparer.Ordinal), Purposes(reply.Body).OrderBy(pair => pair.Key, StringComparer.Ordinal));
            Assert.Contains("\"Bon de saisie\"", reply.Body, StringComparison.Ordinal);
            Assert.All(new[] { "intranet.example", "\"location\"", "\"comment\"", "Ici tout commence" }, text => Assert.DoesNotContain(text, reply.Body, StringComparison.Ordinal));
            Assert.False(reply.Headers.ContainsKey("Content-Disposition"));
        }
    }

    /// <summary>The view in BPMN 2.0 writes no event; the copy for a pdf writes one, for the user who reads it, with the format `pdf` and the map.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_copy_for_a_pdf_writes_one_event_project_exported_with_the_format_pdf(EngineKind kind)
    {
        var project = await SceneAsync(kind);
        using var reader = project.Reader;
        var readerId = (await reader.GetAsync("/api/me")).Ok().GetProperty("id").GetGuid();
        Assert.Equal(200, (await reader.GetAsync(Copy(project.RootMapId, "view"))).Status);
        Assert.Empty(await ExportsAsync(project));

        Assert.Equal(200, (await reader.GetAsync(Copy(project.RootMapId))).Status);

        var audit = Assert.Single(await ExportsAsync(project));
        Assert.Equal((readerId, project.Id), (audit.GetProperty("user_id").GetGuid(), audit.GetProperty("project_id").GetGuid()));
        Assert.Equal(JsonSerializer.Serialize(new { format = "pdf", scope = "map", map = project.RootMapId, with_sheets = true }), audit.GetProperty("details").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_stranger_and_a_project_in_the_trash_get_not_found(EngineKind kind)
    {
        var project = await SceneAsync(kind);
        using var stranger = project.Stranger;
        using var reader = project.Reader;
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;

        (await stranger.GetAsync(Copy(project.RootMapId))).Refused(404, "INTROUVABLE");
        (await reader.GetAsync(Copy(Guid.NewGuid()))).Refused(404, "INTROUVABLE");
        (await facilitator.DeleteAsync($"/api/projects/{project.Id}?confirmation={project.Code}", await project.ProjectVersionAsync())).Ok(204);
        foreach (var member in new[] { reader, facilitator, admin })
            (await member.GetAsync(Copy(project.RootMapId))).Refused(404, "INTROUVABLE");

        Assert.Empty(await ExportsAsync(project));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Another_purpose_is_still_refused(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        using var reader = project.Reader;

        foreach (var purpose in new[] { "PDF", "print", "autre" })
            Assert.Equal("purpose", (await reader.GetAsync(Copy(project.RootMapId, purpose))).Refused(400, "VALEUR_INVALIDE").GetProperty("field").GetString());

        Assert.Empty(await ExportsAsync(project));
    }
}
