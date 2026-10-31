// DissolutionPortedPlan.cs
// The flow of the 1.x dissolution tests drawn through the routes: a detailed step between two others, its detail, its sheet, its output and pain point.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using static Topogenia.Host.Tests.Selection.MapDrawing;

namespace Topogenia.Host.Tests.Ported;

/// <summary>
/// The plan of the 1.x `RemonteeTests.Preparer`, built through the routes as the facilitator. On the root map, lane
/// « Controlling »: Extraction (column 0) -> Saisie (column 1, detailed; the entering flow is labelled « oui ») -> Envoi
/// (column 2). The detail of Saisie: Debut -> Preparer (lane « Controlling ») -> Verifier (lane « Logistique », absent from
/// the flow) -> Fin. With its remainder, Saisie holds a sheet (purpose and one line of procedure) and an output used by
/// Envoi, whose use reports a pain point; the pain point is held by the producer, Saisie.
/// </summary>
internal sealed record DissolutionPortedPlan(
    ApiProject Project,
    Guid Lane,
    Guid Extraction,
    Guid Saisie,
    Guid Envoi,
    Guid Detail,
    Guid Preparer,
    Guid Verifier,
    Guid? Document)
{
    public const string Purpose = "Saisir les ventes";

    public const string ProcedureLine = "Ouvrir le classeur";

    public const string DocumentName = "Classeur des ventes";

    public const string PainPointText = "Colonnes en double";

    public static async Task<DissolutionPortedPlan> CreateAsync(ApiProject project, bool withRemainder = true)
    {
        ArgumentNullException.ThrowIfNull(project);
        using var facilitator = project.Facilitator;
        await RenameLaneAsync(facilitator, project, project.RootMapId, project.FirstLaneId, "Controlling");
        var extraction = (await project.AddStepAsync(0, "Extraction")).Id;
        var saisie = (await project.AddStepAsync(1, "Saisie")).Id;
        var envoi = (await project.AddStepAsync(2, "Envoi")).Id;
        await project.AddFlowAsync(extraction, saisie, "oui");
        await project.AddFlowAsync(saisie, envoi);

        var (detail, detailLane) = await project.DetailAsync(saisie);
        await RenameLaneAsync(facilitator, project, detail, detailLane, "Controlling");
        var logistique = await project.AddLaneAsync("Logistique", detail);
        var debut = (await project.AddStepAsync(0, "Début", detailLane, detail, "start")).Id;
        var preparer = (await project.AddStepAsync(1, "Préparer", detailLane, detail)).Id;
        var verifier = (await project.AddStepAsync(2, "Vérifier", logistique, detail)).Id;
        var fin = (await project.AddStepAsync(3, "Fin", detailLane, detail, "end")).Id;
        await project.AddFlowAsync(debut, preparer, mapId: detail);
        await project.AddFlowAsync(preparer, verifier, mapId: detail);
        await project.AddFlowAsync(verifier, fin, mapId: detail);

        Guid? document = null;
        if (withRemainder)
        {
            (await facilitator.PutAsync($"/api/steps/{saisie}/sheet", SheetBody(Purpose), await SheetReadAsync(facilitator, saisie))).Ok();
            document = await OutputAsync(facilitator, saisie, DocumentName);
            (await facilitator.PutAsync($"/api/steps/{envoi}/uses/{document}",
                new { percent_complete_accurate = 50, pain_point = PainPointText, lost_hours_per_month = 1 }, NoUseRead)).Ok();
        }
        return new DissolutionPortedPlan(project, project.FirstLaneId, extraction, saisie, envoi, detail, preparer, verifier, document);
    }

    /// <summary>The `If-Match` of a client that ticks a document its step does not use yet: no use read (ADR 045).</summary>
    public const int NoUseRead = 0;

    /// <summary>A sheet with its purpose and the single line of procedure of the plan.</summary>
    public static object SheetBody(string purpose) =>
        new { fields = new { purpose = new { value = purpose } }, procedure = new[] { new { text = ProcedureLine } } };

    /// <summary>The `If-Match` a client sends after reading the sheet of a step: the version of the draft, or 0 when none was read.</summary>
    public static async Task<int> SheetReadAsync(ApiClient client, Guid stepId)
    {
        ArgumentNullException.ThrowIfNull(client);
        var sheet = (await client.GetAsync($"/api/steps/{stepId}/sheet")).Ok();
        return sheet.GetProperty("version") is { ValueKind: JsonValueKind.Number } version ? version.GetInt32() : 0;
    }

    /// <summary>Adds an output to a step and returns its identifier.</summary>
    public static async Task<Guid> OutputAsync(ApiClient client, Guid stepId, string name)
    {
        ArgumentNullException.ThrowIfNull(client);
        var sheet = (await client.PostAsync($"/api/steps/{stepId}/documents", new { direction = "output", name })).Ok();
        return sheet.GetProperty("outputs").EnumerateArray().Select(output => output.GetProperty("document"))
            .Single(output => output.GetProperty("name").GetString() == name).GetProperty("id").GetGuid();
    }

    /// <summary>Renames a lane with the version read on its map.</summary>
    public static async Task RenameLaneAsync(ApiClient client, ApiProject project, Guid mapId, Guid laneId, string name)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(project);
        var version = LaneOf(await project.MapAsync(mapId), laneId).GetProperty("version").GetInt32();
        (await client.PatchAsync($"/api/lanes/{laneId}", new { name }, version)).Ok();
    }

    public static JsonElement LaneOf(JsonElement map, Guid laneId) =>
        map.GetProperty("lanes").EnumerateArray().Single(lane => lane.GetProperty("id").GetGuid() == laneId);

    public static IReadOnlyList<string?> LaneNames(JsonElement map) =>
        [.. map.GetProperty("lanes").EnumerateArray().Select(lane => lane.GetProperty("name").GetString())];

    /// <summary>The flows of a map with their labels.</summary>
    public static IReadOnlySet<(Guid Source, Guid Target, string? Label)> LabelledFlows(JsonElement map) =>
        map.GetProperty("flows").EnumerateArray()
            .Select(flow => (flow.GetProperty("source_step_id").GetGuid(), flow.GetProperty("target_step_id").GetGuid(), flow.GetProperty("label").GetString()))
            .ToHashSet();

    public static int Column(JsonElement map, Guid stepId) => Step(map, stepId).GetProperty("column").GetInt32();

    /// <summary>Dissolves Saisie with the version of the step read on the root map; returns the reply.</summary>
    public async Task<JsonElement> DissolveAsync()
    {
        using var facilitator = Project.Facilitator;
        var version = Step(await Project.MapAsync(), Saisie).GetProperty("version").GetInt32();
        return (await facilitator.PostAsync($"/api/steps/{Saisie}/dissolve", null, version)).Ok();
    }

    /// <summary>The open frame of the root map read by the facilitator (null when none is open).</summary>
    public async Task<JsonElement?> FrameAsync()
    {
        var frame = (await Project.MapAsync()).GetProperty("dissolution");
        return frame.ValueKind == JsonValueKind.Null ? null : frame;
    }

    /// <summary>Assigns an element of the remainder of a frame as the facilitator and returns the reply.</summary>
    public async Task<ApiResponse> AssignAsync(Guid frame, string kind, Guid objectId, Guid? stepId, Guid? fromStepId = null)
    {
        using var facilitator = Project.Facilitator;
        return await facilitator.PostAsync($"/api/dissolutions/{frame}/assign", new { kind, object_id = objectId, step_id = stepId, from_step_id = fromStepId });
    }
}
