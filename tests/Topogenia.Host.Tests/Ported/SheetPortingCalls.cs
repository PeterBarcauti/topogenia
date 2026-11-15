// SheetPortingCalls.cs
// The calls the ported tests of the sheets and documents repeat: save and validate with the version read, assign a step, tick a document, read the map summary.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Host.Tests.Steps;

namespace Topogenia.Host.Tests.Ported;

/// <summary>
/// Every write sends the version its client has just read, as the interface does (`If-Match` required in server mode,
/// ADR 045): the version of the draft, or 0 when the client read none.
/// </summary>
internal static class SheetPortingCalls
{
    public static string SheetUrl(Guid stepId, string tail = "") => $"/api/steps/{stepId}/sheet{tail}";

    public static string UseUrl(Guid stepId, Guid documentId) => $"/api/steps/{stepId}/uses/{documentId}";

    public static async Task<JsonElement> ReadAsync(ApiClient client, Guid stepId) => (await client.GetAsync(SheetUrl(stepId))).Ok();

    /// <summary>Saves the sheet with the version the client reads just before.</summary>
    public static async Task<ApiResponse> SaveAsync(ApiClient client, Guid stepId, object body) =>
        await client.PutAsync(SheetUrl(stepId), body, await StepSheets.ReadVersionAsync(client, stepId));

    /// <summary>Validates the sheet with the version the client reads just before.</summary>
    public static async Task<ApiResponse> ValidateAsync(ApiClient client, Guid stepId) =>
        await client.PostAsync(SheetUrl(stepId, "/validate"), null, await StepSheets.ReadVersionAsync(client, stepId));

    /// <summary>A sheet whose simple fields are given as (key, text) pairs.</summary>
    public static object Fields(params (string Key, string Text)[] fields) =>
        new { fields = fields.ToDictionary(field => field.Key, field => (object)new { value = field.Text }) };

    /// <summary>Assigns the step to the contributor, as the facilitator, with the version of the step read on its map.</summary>
    public static async Task<Guid> AssignContributorAsync(ApiProject project, Guid stepId)
    {
        var contributor = await StepSheets.ContributorIdAsync(project);
        using var facilitator = project.Facilitator;
        (await facilitator.PatchAsync($"/api/steps/{stepId}", new { assignee_user_id = contributor }, await project.StepVersionAsync(stepId))).Ok();
        return contributor;
    }

    public static (bool Fill, bool Validate, bool Clarify, bool Assign) Rights(JsonElement sheet)
    {
        var rights = sheet.GetProperty("permissions");
        return (rights.GetProperty("fill").GetBoolean(), rights.GetProperty("validate").GetBoolean(),
            rights.GetProperty("request_clarification").GetBoolean(), rights.GetProperty("assign").GetBoolean());
    }

    public static string? StatusOf(JsonElement sheet) => sheet.GetProperty("status").GetString();

    public static int CompletenessOf(JsonElement sheet) => sheet.GetProperty("completeness").GetInt32();

    /// <summary>The summary of the sheet of a step on the map read by the client: status, completeness, right to fill.</summary>
    public static async Task<(string? Status, int Completeness, bool CanFill)> SummaryAsync(ApiClient client, ApiProject project, Guid stepId)
    {
        var map = (await client.GetAsync($"/api/maps/{project.RootMapId}")).Ok().GetProperty("map");
        var sheet = StructureCalls.StepOf(map, stepId).GetProperty("sheet");
        return (sheet.GetProperty("status").GetString(), sheet.GetProperty("completeness").GetInt32(), sheet.GetProperty("can_fill").GetBoolean());
    }

    /// <summary>The `If-Match` of a tick: the version of the use the step shows, or 0 when it does not use the document yet.</summary>
    public static async Task<int> UseReadAsync(ApiClient client, Guid stepId, Guid documentId)
    {
        var inputs = (await ReadAsync(client, stepId)).GetProperty("inputs").EnumerateArray();
        var use = inputs.Where(input => input.GetProperty("document").GetProperty("id").GetGuid() == documentId)
            .Select(input => input.GetProperty("use")).SingleOrDefault(use => use.ValueKind == JsonValueKind.Object);
        return use.ValueKind == JsonValueKind.Object ? use.GetProperty("version").GetInt32() : StepSheets.NoRecord;
    }

    /// <summary>Ticks (or changes the use of) a document on a step with the version of the use read just before.</summary>
    public static async Task<ApiResponse> UseAsync(ApiClient client, Guid stepId, Guid documentId, object body) =>
        await client.PutAsync(UseUrl(stepId, documentId), body, await UseReadAsync(client, stepId, documentId));

    /// <summary>Adds an output to a step and returns the identifier of the document added (the last output of the reply).</summary>
    public static async Task<Guid> OutputAsync(ApiClient client, Guid stepId, string name, string? location = null)
    {
        var sheet = (await client.PostAsync($"/api/steps/{stepId}/documents", new { direction = "output", name, location })).Ok();
        return sheet.GetProperty("outputs").EnumerateArray().Select(output => output.GetProperty("document"))
            .Single(document => document.GetProperty("name").GetString() == name).GetProperty("id").GetGuid();
    }

    /// <summary>The inputs of a sheet as (name, near, used).</summary>
    public static IReadOnlyList<(string? Name, bool Near, bool Used)> InputsOf(JsonElement sheet) =>
        [.. sheet.GetProperty("inputs").EnumerateArray().Select(input =>
            (input.GetProperty("document").GetProperty("name").GetString(), input.GetProperty("near").GetBoolean(), input.GetProperty("used").GetBoolean()))];

    /// <summary>The input of a sheet for a document.</summary>
    public static JsonElement InputOf(JsonElement sheet, Guid documentId) =>
        sheet.GetProperty("inputs").EnumerateArray().Single(input => input.GetProperty("document").GetProperty("id").GetGuid() == documentId);

    /// <summary>The steps that use the single output of a sheet.</summary>
    public static IReadOnlyList<Guid> UsersOfSingleOutput(JsonElement sheet) =>
        [.. sheet.GetProperty("outputs").EnumerateArray().Single().GetProperty("used_by").EnumerateArray().Select(use => use.GetProperty("step_id").GetGuid())];

    /// <summary>The names of the documents a flow carries on the map read by the facilitator.</summary>
    public static async Task<IReadOnlyList<string?>> FlowDocumentsAsync(ApiProject project, Guid flowId) =>
        [.. StructureCalls.FlowOf(await project.MapAsync(), flowId).GetProperty("documents").EnumerateArray().Select(name => name.GetString())];
}
