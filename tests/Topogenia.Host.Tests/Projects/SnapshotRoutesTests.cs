// SnapshotRoutesTests.cs
// The photos of the indicators and the target state through the whole host on both databases (Makigami M6, M8, ADR 095): a photo taken, read and deleted; a target created with its sheets and its reference, compared, its reference read again; its package and its import (P3d).

using System.IO.Compression;
using System.Text.Json;
using Topogenia.Application.Audit;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Dissolutions;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Projects;

public sealed class SnapshotRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static JsonElement[] Photos(JsonElement view) => [.. view.GetProperty("photos").EnumerateArray()];

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_facilitator_takes_a_photo_every_member_reads_it_and_it_is_deleted(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        await DetailedRow.CreateAsync(project, "Bon de commande");
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;

        var taken = (await facilitator.PostAsync($"/api/projects/{project.Id}/snapshots", new { label = "  Atelier du matin " })).Ok();
        var photo = Assert.Single(Photos(taken));
        Assert.Equal(("Atelier du matin", "photo"), (photo.GetProperty("label").GetString(), photo.GetProperty("origin").GetString()));
        // The two waits of the lead time (F-105) are indicators of the photo as the others, named in the API.
        var indicators = photo.GetProperty("indicators");
        Assert.True(indicators.TryGetProperty("handoff_wait_hours", out _) && indicators.TryGetProperty("other_wait_hours", out _));
        Assert.Equal(JsonValueKind.Null, taken.GetProperty("baseline").ValueKind);
        Assert.Equal(JsonValueKind.Null, taken.GetProperty("comparison").ValueKind);
        var read = (await reader.GetAsync($"/api/projects/{project.Id}/snapshots")).Ok();
        Assert.Equal(photo.GetProperty("id").GetGuid(), Assert.Single(Photos(read)).GetProperty("id").GetGuid());
        (await facilitator.PostAsync($"/api/projects/{project.Id}/snapshots", new { label = " " })).Refused(400, "CHAMP_OBLIGATOIRE");

        var left = (await facilitator.DeleteAsync($"/api/projects/{project.Id}/snapshots/{photo.GetProperty("id").GetGuid()}")).Ok();
        Assert.Empty(Photos(left));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_target_state_is_a_new_project_with_the_sheets_of_its_source_its_reference_and_its_comparison(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var row = await DetailedRow.CreateAsync(project, "Bon de commande");
        using var admin = project.Admin;
        var code = ApiProject.NewCode();

        var created = (await admin.PostAsync($"/api/projects/{project.Id}/target", new { code, name = "Commandes, état cible", label = "État actuel" })).Ok(201);
        var target = created.GetProperty("id").GetGuid();
        Assert.Equal(code, created.GetProperty("code").GetString());
        var view = (await admin.GetAsync($"/api/projects/{target}/snapshots")).Ok();
        Assert.Equal(project.Id, view.GetProperty("baseline_project_id").GetGuid());
        var baseline = view.GetProperty("baseline");
        Assert.Equal(("État actuel", "baseline"), (baseline.GetProperty("label").GetString(), baseline.GetProperty("origin").GetString()));
        Assert.Equal(12, view.GetProperty("comparison").GetArrayLength());
        Assert.Empty(Photos(view));
        // The documents of the source came with the target, as its complete package carries them.
        var sourceDocuments = row.Documents.Count;
        var map = (await admin.GetAsync($"/api/maps/{created.GetProperty("root_map_id").GetGuid()}")).Ok().GetProperty("map");
        Assert.Equal((await project.MapAsync()).GetProperty("steps").GetArrayLength(), map.GetProperty("steps").GetArrayLength());
        Assert.True(sourceDocuments > 0);

        var again = (await admin.PostAsync($"/api/projects/{target}/baseline", new { label = "État actuel, relu" })).Ok();
        Assert.Equal("État actuel, relu", again.GetProperty("baseline").GetProperty("label").GetString());
        (await admin.DeleteAsync($"/api/projects/{target}/snapshots/{baseline.GetProperty("id").GetGuid()}")).Refused(400, "VALEUR_INVALIDE");
        (await admin.PostAsync($"/api/projects/{project.Id}/baseline", new { label = "Sans source" })).Refused(400, "VALEUR_INVALIDE");
    }

    /// <summary>The object project of project.json in a package.</summary>
    private static JsonElement ProjectEntry(byte[] package)
    {
        using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry("project.json")!.Open());
        return JsonDocument.Parse(reader.ReadToEnd()).RootElement.GetProperty("project").Clone();
    }

    private static async Task<byte[]> ExportAsync(ApiClient client, Guid projectId)
    {
        var reply = await client.PostAsync($"/api/projects/{projectId}/export", new { without_people = false });
        Assert.True(reply.Status == 200, reply.Body);
        return reply.Bytes;
    }

    private static async Task<JsonElement> ImportAsync(ApiClient admin, byte[] package)
    {
        var imported = (await admin.SendAsync(HttpMethod.Post, $"/api/projects/import?with_members=false&code={ApiProject.NewCode()}", package, null)).Ok(201);
        return (await admin.GetAsync($"/api/projects/{imported.GetProperty("id").GetGuid()}/snapshots")).Ok();
    }

    /// <summary>
    /// The package carries the photos and the reference of a target state and names its source by code (P3d): imported, the
    /// target is linked again to its live source; once the source is in the trash, the package names it no more and the
    /// imported target keeps its reference without a source. The target never takes the photos of its source.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_package_of_a_target_state_carries_its_photos_and_its_import_links_it_again_to_its_live_source(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        await DetailedRow.CreateAsync(project, "Bon de commande");
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        (await facilitator.PostAsync($"/api/projects/{project.Id}/snapshots", new { label = "Photo de la source" })).Ok();
        var target = (await admin.PostAsync($"/api/projects/{project.Id}/target", new { code = ApiProject.NewCode(), name = "Cible", label = "État actuel" }))
            .Ok(201).GetProperty("id").GetGuid();
        Assert.Empty(Photos((await admin.GetAsync($"/api/projects/{target}/snapshots")).Ok()));
        (await admin.PostAsync($"/api/projects/{target}/snapshots", new { label = "Après l'atelier" })).Ok();

        var package = await ExportAsync(admin, target);
        var entry = ProjectEntry(package);
        Assert.Equal(project.Code, entry.GetProperty("baselineProject").GetString());
        Assert.Equal(["État actuel", "Après l'atelier"], entry.GetProperty("snapshots").EnumerateArray().Select(photo => photo.GetProperty("label").GetString()));
        var linked = await ImportAsync(admin, package);
        Assert.Equal(project.Id, linked.GetProperty("baseline_project_id").GetGuid());
        Assert.Equal("État actuel", linked.GetProperty("baseline").GetProperty("label").GetString());
        Assert.Equal("Après l'atelier", Assert.Single(Photos(linked)).GetProperty("label").GetString());
        Assert.Equal(12, linked.GetProperty("comparison").GetArrayLength());

        (await facilitator.DeleteAsync($"/api/projects/{project.Id}?confirmation={project.Code}", await project.ProjectVersionAsync())).Ok(204);
        var orphan = await ExportAsync(admin, target);
        Assert.False(ProjectEntry(orphan).TryGetProperty("baselineProject", out _));
        var unlinked = await ImportAsync(admin, orphan);
        Assert.Equal(JsonValueKind.Null, unlinked.GetProperty("baseline_project_id").ValueKind);
        Assert.Equal("État actuel", unlinked.GetProperty("baseline").GetProperty("label").GetString());
    }

    /// <summary>
    /// F-108 (decision of 07/11/2026): the source replaced by its own export takes a new identifier, and the target state
    /// follows it: it names the new project, whose reference it reads again.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_target_state_follows_its_source_replaced_by_an_import(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        await DetailedRow.CreateAsync(project, "Bon de commande");
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        var target = (await admin.PostAsync($"/api/projects/{project.Id}/target", new { code = ApiProject.NewCode(), name = "Cible", label = "État actuel" }))
            .Ok(201).GetProperty("id").GetGuid();
        var package = await ExportAsync(facilitator, project.Id);

        var replacement = (await facilitator.SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/import?with_members=true", package,
            await project.ProjectVersionAsync())).Ok(201).GetProperty("id").GetGuid();

        Assert.NotEqual(project.Id, replacement);
        Assert.Equal(replacement, (await admin.GetAsync($"/api/projects/{target}/snapshots")).Ok().GetProperty("baseline_project_id").GetGuid());
        var again = (await admin.PostAsync($"/api/projects/{target}/baseline", new { label = "Relue sur la source remplacée" })).Ok();
        Assert.Equal("Relue sur la source remplacée", again.GetProperty("baseline").GetProperty("label").GetString());
        var imported = Assert.Single((await admin.GetAsync($"/api/admin/audit?project_id={replacement}&kind={AuditKinds.ProjectImported}")).Ok().EnumerateArray());
        Assert.Equal("""{"mode":"replace","with_members":true,"targets_relinked":1}""", imported.GetProperty("details").GetString());
    }

    private static int VersionOf(JsonElement map, string list, Guid id) =>
        map.GetProperty(list).EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == id).GetProperty("version").GetInt32();

    /// <summary>
    /// P3e (choice of the client of 07/11/2026): a facilitator of the target state refreshes its reference, the indicators of
    /// the source as they see them there (a draft sheet counts for a facilitator of the source, not for one of its readers);
    /// the screen learns where the source stands: visible with its code, hidden from a person out of it, then missing.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_facilitator_of_the_target_refreshes_its_reference_as_they_see_the_source_and_learns_where_it_stands(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var start = await project.AddStepAsync(0, "Demande reçue", type: "start");
        var work = await project.AddStepAsync(1, "Traiter la demande");
        await project.AddFlowAsync(start.Id, work.Id);
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        using var reader = project.Reader;
        using var stranger = project.Stranger;
        var map = await project.MapAsync();
        (await facilitator.PatchAsync($"/api/steps/{start.Id}", new { volume = new { quantity = 120, period = "month" } }, VersionOf(map, "steps", start.Id))).Ok();
        // A draft sheet: its duration counts for a facilitator of the source, not for one of its readers.
        (await facilitator.PutAsync($"/api/steps/{work.Id}/sheet", new { fields = new { duration_minutes = new { number = 30 } } },
            await StepSheets.ReadVersionAsync(facilitator, work.Id))).Ok();
        var target = (await admin.PostAsync($"/api/projects/{project.Id}/target", new { code = ApiProject.NewCode(), name = "Cible", label = "État actuel" }))
            .Ok(201).GetProperty("id").GetGuid();
        // The reader of the source and a stranger to it become facilitators of the target.
        (await stranger.GetAsync("/api/me")).Ok();
        var members = new object[]
        {
            new { login = Logins.Facilitator, role = "facilitator", is_sponsor = true },
            new { login = Logins.Reader, role = "facilitator", is_sponsor = false },
            new { login = Logins.Stranger, role = "facilitator", is_sponsor = false },
        };
        (await admin.PutAsync($"/api/projects/{target}/members", members, (await admin.GetAsync($"/api/projects/{target}")).Ok().GetProperty("version").GetInt32())).Ok();

        var visible = (await facilitator.GetAsync($"/api/projects/{target}/snapshots")).Ok().GetProperty("source");
        Assert.Equal(("visible", project.Code), (visible.GetProperty("state").GetString(), visible.GetProperty("code").GetString()));
        var hidden = (await stranger.GetAsync($"/api/projects/{target}/snapshots")).Ok().GetProperty("source");
        Assert.Equal(("hidden", JsonValueKind.Null), (hidden.GetProperty("state").GetString(), hidden.GetProperty("code").ValueKind));
        (await stranger.PostAsync($"/api/projects/{target}/baseline", new { label = "Relue" })).Refused(403, "ACCES_REFUSE");

        var byReader = (await reader.PostAsync($"/api/projects/{target}/baseline", new { label = "Relue par le lecteur" })).Ok();
        Assert.Equal(JsonValueKind.Null, byReader.GetProperty("baseline").GetProperty("indicators").GetProperty("work_minutes").ValueKind);
        Assert.Equal(JsonValueKind.Null, (await reader.GetAsync($"/api/projects/{project.Id}/snapshots")).Ok().GetProperty("current").GetProperty("work_minutes").ValueKind);
        var byFacilitator = (await facilitator.PostAsync($"/api/projects/{target}/baseline", new { label = "Relue par l'animateur" })).Ok();
        Assert.Equal(30d, byFacilitator.GetProperty("baseline").GetProperty("indicators").GetProperty("work_minutes").GetDouble(), 9);
        Assert.Equal("Relue par l'animateur", byFacilitator.GetProperty("baseline").GetProperty("label").GetString());

        (await facilitator.DeleteAsync($"/api/projects/{project.Id}?confirmation={project.Code}", await project.ProjectVersionAsync())).Ok(204);
        var missing = (await facilitator.GetAsync($"/api/projects/{target}/snapshots")).Ok().GetProperty("source");
        Assert.Equal(("missing", JsonValueKind.Null), (missing.GetProperty("state").GetString(), missing.GetProperty("code").ValueKind));
        (await facilitator.PostAsync($"/api/projects/{target}/baseline", new { label = "Sans source" })).Refused(400, "VALEUR_INVALIDE");
    }
}
