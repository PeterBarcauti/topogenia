// SheetStepPainPointsTests.cs
// R10 through the whole host on both databases: a pain point no use carries (a package may bring one) is shown on the sheet of its own step, under no document, and takes its status there under the right to fill that sheet; the plan names that step.

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Documents;

public sealed class SheetStepPainPointsTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private static string Status(Guid stepId, Guid painPointId) => $"/api/steps/{stepId}/pain-points/{painPointId}/status";

    /// <summary>
    /// The project once imported again: « Relance » raised on a step of the lane the contributor holds, « Saisie en double »
    /// on a step of a lane nobody holds, both without the use that carried them; the steps found through the plan.
    /// </summary>
    private sealed record Scene(ApiProject Project, Guid MapId, Guid Held, Guid Free, Guid Relance, Guid Double);

    private async Task<Scene> SceneAsync(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        await StepSheets.MakeContributorActorAsync(project);
        using var facilitator = project.Facilitator;
        var other = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Comptabilité" })).Ok().GetProperty("id").GetGuid();
        var held = await StepSheets.StepAsync(project, 0, "Saisir");
        var free = await StepSheets.StepAsync(project, 1, "Extraire", laneId: other);
        await RaiseAsync(facilitator, free, held, "Saisie en double", 1.5m);
        await RaiseAsync(facilitator, held, free, "Relance", 2m);
        var package = (await facilitator.PostAsync($"/api/projects/{project.Id}/export", new { without_people = false })).Bytes;
        var imported = (await facilitator.SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/import?with_members=true",
            WithoutUses(package), await project.ProjectVersionAsync())).Ok(201);
        var mapId = imported.GetProperty("root_map_id").GetGuid();
        var lines = (await facilitator.GetAsync($"/api/maps/{mapId}/action-plan")).Ok().GetProperty("lines").EnumerateArray()
            .ToDictionary(line => line.GetProperty("description").GetString()!);
        Guid Of(string description, string property) => lines[description].GetProperty(property).GetGuid();
        return new Scene(project, mapId, Of("Relance", "step_id"), Of("Saisie en double", "step_id"), Of("Relance", "pain_point_id"), Of("Saisie en double", "pain_point_id"));
    }

    /// <summary>An output of the producing step used by the receiving step with a pain point.</summary>
    private static async Task RaiseAsync(ApiClient facilitator, Guid producer, Guid receiver, string painPoint, decimal hours)
    {
        var sheet = (await facilitator.PostAsync($"/api/steps/{producer}/documents", new { direction = "output", name = $"Sortie {painPoint}" })).Ok();
        var document = sheet.GetProperty("outputs").EnumerateArray().Last().GetProperty("document").GetProperty("id").GetGuid();
        (await facilitator.PutAsync($"/api/steps/{receiver}/uses/{document}", new { pain_point = painPoint, lost_hours_per_month = hours }, StepSheets.NoRecord)).Ok();
    }

    private static async Task<JsonElement> SheetAsync(ApiProject project, Guid stepId)
    {
        using var facilitator = project.Facilitator;
        return (await facilitator.GetAsync($"/api/steps/{stepId}/sheet")).Ok();
    }

    private static List<JsonElement> StepPoints(JsonElement sheet) => [.. sheet.GetProperty("step_pain_points").EnumerateArray()];

    private static async Task<int> VersionAsync(ApiProject project, Guid stepId, Guid painPoint) =>
        StepPoints(await SheetAsync(project, stepId)).Single(point => point.GetProperty("id").GetGuid() == painPoint).GetProperty("version").GetInt32();

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_pain_point_no_use_carries_is_shown_on_the_sheet_of_its_own_step_under_no_document(EngineKind kind)
    {
        var scene = await SceneAsync(kind);
        var project = scene.Project;

        var held = await SheetAsync(project, scene.Held);
        var point = Assert.Single(StepPoints(held));
        Assert.Equal((scene.Relance, "Relance", "reported", 2m), (point.GetProperty("id").GetGuid(), point.GetProperty("description").GetString(),
            point.GetProperty("status").GetString(), point.GetProperty("lost_hours_per_month").GetDecimal()));
        Assert.Empty(held.GetProperty("pain_points").EnumerateArray());
        Assert.Equal([scene.Double], StepPoints(await SheetAsync(project, scene.Free)).Select(item => item.GetProperty("id").GetGuid()));
        using var reader = project.Reader;
        var line = (await reader.GetAsync($"/api/maps/{scene.MapId}/action-plan")).Ok().GetProperty("lines").EnumerateArray()
            .Single(item => item.GetProperty("pain_point_id").GetGuid() == scene.Relance);
        Assert.Equal((scene.Held, JsonValueKind.Null, JsonValueKind.Null, false), (line.GetProperty("status_step_id").GetGuid(),
            line.GetProperty("receiving_step_id").ValueKind, line.GetProperty("document_id").ValueKind, line.GetProperty("can_fill").GetBoolean()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Each_role_changes_its_status_on_the_sheet_of_its_own_step_as_the_right_to_fill_it_says(EngineKind kind)
    {
        var scene = await SceneAsync(kind);
        var project = scene.Project;
        async Task<ApiResponse> ChangeAsync(ApiClient client, Guid stepId, Guid point, string status)
        {
            using (client)
                return await client.PutAsync(Status(stepId, point), new { status }, await VersionAsync(project, stepId, point));
        }

        (await ChangeAsync(project.Reader, scene.Held, scene.Relance, "implemented")).Refused(403, "ACCES_REFUSE");
        (await ChangeAsync(project.Contributor, scene.Free, scene.Double, "implemented")).Refused(403, "ACCES_REFUSE");
        using (var facilitator = project.Facilitator)
        {
            // On another step: not told from an absent pain point.
            (await facilitator.PutAsync(Status(scene.Free, scene.Relance), new { status = "implemented" }, await VersionAsync(project, scene.Held, scene.Relance)))
                .Refused(404, "INTROUVABLE");
        }
        using (var contributor = project.Contributor)
        {
            var plan = (await contributor.GetAsync($"/api/maps/{scene.MapId}/action-plan")).Ok().GetProperty("lines").EnumerateArray()
                .ToDictionary(line => line.GetProperty("pain_point_id").GetGuid(), line => line.GetProperty("can_fill").GetBoolean());
            Assert.Equal((true, false), (plan[scene.Relance], plan[scene.Double]));
        }

        var reply = (await ChangeAsync(project.Contributor, scene.Held, scene.Relance, "in_progress")).Ok();
        Assert.Equal((scene.Held, "in_progress"), (reply.GetProperty("step_id").GetGuid(), StepPoints(reply).Single().GetProperty("status").GetString()));
        (await ChangeAsync(project.Facilitator, scene.Free, scene.Double, "implemented")).Ok();
        Assert.Equal("implemented", StepPoints(await SheetAsync(project, scene.Free)).Single().GetProperty("status").GetString());
    }

    /// <summary>The package with no use carrying a pain point any more (a package written by hand), the manifest announcing it again.</summary>
    private static byte[] WithoutUses(byte[] package)
    {
        var entries = Entries(package);
        var index = entries.FindIndex(item => item.Name == "documents.json");
        var node = JsonNode.Parse(entries[index].Content)!;
        foreach (var use in Named(node, "uses").SelectMany(uses => uses.AsArray()).OfType<JsonObject>())
            use.Remove("painPoint");
        var content = Encoding.UTF8.GetBytes(node.ToJsonString());
        entries[index] = ("documents.json", content);
        var manifestIndex = entries.FindIndex(item => item.Name == "manifest.json");
        var manifest = JsonNode.Parse(entries[manifestIndex].Content)!;
        var announced = manifest["entries"]!.AsArray().Single(item => (string?)item!["path"] == "documents.json")!;
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

    /// <summary>The values of a JSON tree under a property name, at any depth.</summary>
    private static IEnumerable<JsonNode> Named(JsonNode node, string name) => node switch
    {
        JsonObject item => item.Where(pair => pair.Value is not null)
            .SelectMany(pair => (pair.Key == name ? [pair.Value!] : Enumerable.Empty<JsonNode>()).Concat(Named(pair.Value!, name))),
        JsonArray list => list.OfType<JsonNode>().SelectMany(value => Named(value, name)),
        _ => [],
    };
}
