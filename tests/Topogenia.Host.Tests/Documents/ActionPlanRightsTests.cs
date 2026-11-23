// ActionPlanRightsTests.cs
// The action plan of the panel through the whole host on both databases (ADR 112): every line for every role with its own right to fill, every level and the total of the open hours, nothing written, the refusals.

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Dissolutions;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Documents;

public sealed class ActionPlanRightsTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private static string Plan(Guid mapId) => $"/api/maps/{mapId}/action-plan";

    private static async Task<JsonElement> PlanAsync(ApiClient client, Guid mapId) => (await client.GetAsync(Plan(mapId))).Ok();

    private static List<JsonElement> Lines(JsonElement plan) => [.. plan.GetProperty("lines").EnumerateArray()];

    /// <summary>The lines without their right to fill, written as one text, to compare what two roles read.</summary>
    private static string Shared(JsonElement plan) => string.Join("\n", Lines(plan).Select(line =>
        string.Join(",", line.EnumerateObject().Where(property => property.Name != "can_fill").Select(property => $"{property.Name}={property.Value.GetRawText()}")))
        + "\n" + plan.GetProperty("open_lost_hours_per_month").GetRawText());

    /// <summary>An output of the producing step used by the receiving step with a pain point and its hours; returns the identifier of the output.</summary>
    private static async Task<Guid> RaiseAsync(ApiClient facilitator, Guid producer, Guid receiver, string painPoint, decimal hours)
    {
        var sheet = (await facilitator.PostAsync($"/api/steps/{producer}/documents", new { direction = "output", name = $"Sortie {painPoint}" })).Ok();
        var document = sheet.GetProperty("outputs").EnumerateArray().Last().GetProperty("document").GetProperty("id").GetGuid();
        (await facilitator.PutAsync($"/api/steps/{receiver}/uses/{document}", new { pain_point = painPoint, lost_hours_per_month = hours }, StepSheets.NoRecord)).Ok();
        return document;
    }

    /// <summary>
    /// The contributor holds the first lane of the root map; a second lane is held by nobody. « Saisie en double » is raised
    /// on a step of the second lane by a step of the first one, which receives it; « Relance » the other way round.
    /// </summary>
    private async Task<(ApiProject Project, Guid HeldReceives, Guid FreeReceives)> SceneAsync(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        await StepSheets.MakeContributorActorAsync(project);
        using var facilitator = project.Facilitator;
        var other = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Comptabilité" })).Ok().GetProperty("id").GetGuid();
        var held = await StepSheets.StepAsync(project, 0, "Saisir");
        var free = await StepSheets.StepAsync(project, 1, "Extraire", laneId: other);
        await RaiseAsync(facilitator, free, held, "Saisie en double", 1.5m);
        await RaiseAsync(facilitator, held, free, "Relance", 2m);
        return (project, held, free);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Each_role_reads_every_line_of_the_root_map_and_its_own_right_to_fill(EngineKind kind)
    {
        var (project, held, free) = await SceneAsync(kind);
        // By description: « Relance » received on the free lane, « Saisie en double » received on the lane the contributor holds.
        var expected = new (ApiClient Client, bool[] CanFill)[]
        {
            (project.Reader, [false, false]),
            (project.Contributor, [false, true]),
            (project.Facilitator, [true, true]),
            (project.Admin, [true, true]),
        };
        var shared = new List<string>();
        foreach (var (client, canFill) in expected)
        {
            using var caller = client;
            var lines = Lines(await PlanAsync(caller, project.RootMapId)).OrderBy(line => line.GetProperty("description").GetString(), StringComparer.Ordinal).ToList();
            Assert.Equal(["Relance", "Saisie en double"], lines.Select(line => line.GetProperty("description").GetString()));
            Assert.Equal(canFill, lines.Select(line => line.GetProperty("can_fill").GetBoolean()));
            Assert.Equal([free, held], lines.Select(line => line.GetProperty("receiving_step_id").GetGuid()));
            Assert.All(lines, line => Assert.Equal(project.RootMapId, line.GetProperty("receiving_map_id").GetGuid()));
            Assert.All(lines, line => Assert.True(line.GetProperty("open").GetBoolean()));
            shared.Add(Shared(await PlanAsync(caller, project.RootMapId)));
        }
        Assert.Single(shared.Distinct(StringComparer.Ordinal));

        using var reader = project.Reader;
        var line = Lines(await PlanAsync(reader, project.RootMapId))[0];
        Assert.Equal(["pain_point_id", "code", "description", "status", "lost_hours_per_month", "action", "owner", "due_date", "map_id", "map",
            "step_id", "step_code", "step", "lane", "open", "receiving_step_id", "receiving_map_id", "document_id", "can_fill", "status_step_id", "version", "status_changed_at"], line.EnumerateObject().Select(property => property.Name));
        Assert.Equal(JsonValueKind.String, line.GetProperty("document_id").ValueKind);
    }

    /// <summary>
    /// A pain point on the root map (0.1 h) and one on the detailed map (0.2 h): the plan of the root map holds both, the
    /// total is exactly 0.3. A package of the project in which the second is dealt with (implemented), imported in its
    /// place: it stays in the plan, closed, and the total is 0.1.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_plan_of_the_root_map_holds_the_pain_points_of_every_level_and_the_total_of_the_open_ones(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        var row = await DetailedRow.CreateAsync(project);
        using var facilitator = project.Facilitator;
        await RaiseAsync(facilitator, row.Before, row.After, "Saisie en double", 0.1m);
        await RaiseAsync(facilitator, row.First, row.Second, "Relance", 0.2m);
        using var reader = project.Reader;

        var plan = await PlanAsync(reader, project.RootMapId);
        Assert.Equal([(project.RootMapId, row.Before, row.After), (row.SubMap, row.First, row.Second)],
            Lines(plan).Select(line => (line.GetProperty("map_id").GetGuid(), line.GetProperty("step_id").GetGuid(), line.GetProperty("receiving_step_id").GetGuid())));
        Assert.Equal("0.3", plan.GetProperty("open_lost_hours_per_month").GetRawText());
        Assert.Equal(0.2m, (await PlanAsync(reader, row.SubMap)).GetProperty("open_lost_hours_per_month").GetDecimal());

        var package = (await facilitator.PostAsync($"/api/projects/{project.Id}/export", new { without_people = false })).Bytes;
        var imported = (await facilitator.SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/import?with_members=true",
            Implemented(package, "Relance"), await project.ProjectVersionAsync())).Ok(201);
        var again = await PlanAsync(reader, imported.GetProperty("root_map_id").GetGuid());
        Assert.Equal([("Saisie en double", "reported", true), ("Relance", "implemented", false)], Lines(again).Select(line =>
            (line.GetProperty("description").GetString(), line.GetProperty("status").GetString(), line.GetProperty("open").GetBoolean())));
        Assert.Equal("0.1", again.GetProperty("open_lost_hours_per_month").GetRawText());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Reading_the_plan_writes_no_event_of_audit(EngineKind kind)
    {
        var (project, held, _) = await SceneAsync(kind);
        using var admin = project.Admin;
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;
        using var reader = project.Reader;
        async Task<string> StateAsync() => string.Join("\n",
            (await admin.GetAsync($"/api/admin/audit?project_id={project.Id}&limit=1000")).Body,
            (await facilitator.GetAsync($"/api/projects/{project.Id}")).Body,
            (await facilitator.GetAsync($"/api/maps/{project.RootMapId}")).Body,
            (await facilitator.GetAsync($"/api/maps/{project.RootMapId}/history")).Body,
            (await facilitator.GetAsync($"/api/steps/{held}/sheet")).Body);
        var before = await StateAsync();

        foreach (var client in new[] { admin, facilitator, contributor, reader })
            Assert.Equal(2, Lines(await PlanAsync(client, project.RootMapId)).Count);

        Assert.Equal(before, await StateAsync());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_stranger_and_a_project_in_the_trash_get_not_found(EngineKind kind)
    {
        var (project, _, _) = await SceneAsync(kind);
        using var stranger = project.Stranger;
        using var reader = project.Reader;
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        Assert.Equal(2, Lines(await PlanAsync(reader, project.RootMapId)).Count);

        (await stranger.GetAsync(Plan(project.RootMapId))).Refused(404, "INTROUVABLE");
        (await reader.GetAsync(Plan(Guid.NewGuid()))).Refused(404, "INTROUVABLE");

        (await facilitator.DeleteAsync($"/api/projects/{project.Id}?confirmation={project.Code}", await project.ProjectVersionAsync())).Ok(204);
        foreach (var member in new[] { reader, facilitator, admin })
            (await member.GetAsync(Plan(project.RootMapId))).Refused(404, "INTROUVABLE");
    }

    /// <summary>The package with the pain point of this description made implemented, announced again in the manifest (size and SHA-256) as the export writes it.</summary>
    private static byte[] Implemented(byte[] package, string description)
    {
        var entries = Entries(package);
        var index = entries.FindIndex(item => item.Name == "painpoints.json");
        var node = JsonNode.Parse(entries[index].Content)!;
        node["painPoints"]!.AsArray().Single(point => (string?)point!["description"] == description)!["status"] = "implemented";
        var content = Encoding.UTF8.GetBytes(node.ToJsonString());
        entries[index] = ("painpoints.json", content);
        var manifestIndex = entries.FindIndex(item => item.Name == "manifest.json");
        var manifest = JsonNode.Parse(entries[manifestIndex].Content)!;
        var announced = manifest["entries"]!.AsArray().Single(item => (string?)item!["path"] == "painpoints.json")!;
        announced["size"] = content.LongLength;
        announced["sha256"] = Convert.ToHexStringLower(SHA256.HashData(content));
        entries[manifestIndex] = ("manifest.json", Encoding.UTF8.GetBytes(manifest.ToJsonString()));
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, bytes) in entries)
            {
                using var item = archive.CreateEntry(name).Open();
                item.Write(bytes);
            }
        }
        return stream.ToArray();
    }

    private static List<(string Name, byte[] Content)> Entries(byte[] package)
    {
        using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        return [.. archive.Entries.Select(entry =>
        {
            using var stream = entry.Open();
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return (entry.FullName, copy.ToArray());
        })];
    }
}
