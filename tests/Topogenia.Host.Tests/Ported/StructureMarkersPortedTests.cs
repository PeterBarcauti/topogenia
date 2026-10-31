// StructureMarkersPortedTests.cs
// The 1.x tests of the step markers and of the sheet frequency read with the map (ReperesTests, FrequenceTests, T-REPE-01, T-FREQ-01) ported to the whole host on both databases.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Ported;

public sealed class StructureMarkersPortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    /// <summary>The step of the root map as a client reads it.</summary>
    private static async Task<JsonElement> StepReadAsync(ApiProject project, ApiClient client, Guid stepId) =>
        StructureCalls.StepOf((await client.GetAsync($"/api/maps/{project.RootMapId}")).Ok().GetProperty("map"), stepId);

    private static async Task<JsonElement> SaveSheetAsync(ApiClient client, Guid stepId, object fields) =>
        (await client.PutAsync($"/api/steps/{stepId}/sheet", new { fields }, await StepSheets.ReadVersionAsync(client, stepId))).Ok();

    private static async Task ValidateAsync(ApiClient client, Guid stepId) =>
        (await client.PostAsync($"/api/steps/{stepId}/sheet/validate", null, StepSheets.VersionOf((await client.GetAsync($"/api/steps/{stepId}/sheet")).Ok()))).Ok();

    private static IReadOnlyList<string> DocumentMarkers(JsonElement step) =>
        [.. step.GetProperty("markers").GetProperty("documents").EnumerateArray()
            .Select(marker => $"{marker.GetProperty("name").GetString()}:{marker.GetProperty("direction").GetString()}").Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_map_gives_each_step_its_documents_its_open_pain_points_on_the_producer_and_the_duration_of_its_sheet(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;
        var extraction = (await project.AddStepAsync(0, "Extraction")).Id;
        var table = (await project.AddStepAsync(1, "Tableau")).Id;
        await project.AddFlowAsync(extraction, table);
        var document = (await facilitator.PostAsync($"/api/steps/{extraction}/documents", new { direction = "output", name = "Extraction SAP" })).Ok()
            .GetProperty("outputs").EnumerateArray().Single().GetProperty("document").GetProperty("id").GetGuid();
        (await facilitator.PostAsync($"/api/steps/{table}/documents", new { direction = "input", name = "Fichier des groupes" })).Ok();
        (await facilitator.PutAsync($"/api/steps/{table}/uses/{document}",
            new { percent_complete_accurate = 0, pain_point = "Requête périmée", lost_hours_per_month = 2 }, StepSheets.NoRecord)).Ok();
        await SaveSheetAsync(facilitator, table, new { duration_minutes = new { number = 45 } });

        var producer = await StepReadAsync(project, reader, extraction);
        var user = await StepReadAsync(project, reader, table);
        Assert.Equal(["Extraction SAP:output"], DocumentMarkers(producer));
        // The pain point of a received document is put on the step that produces it (where it is corrected).
        Assert.Equal("Requête périmée", Assert.Single(producer.GetProperty("markers").GetProperty("pain_points").EnumerateArray()).GetProperty("description").GetString());
        Assert.Equal(["Extraction SAP:input", "Fichier des groupes:input"], DocumentMarkers(user));
        Assert.Empty(user.GetProperty("markers").GetProperty("pain_points").EnumerateArray());

        // Duration: the draft one for the facilitator, nothing for the reader while the sheet is not validated.
        Assert.Equal(JsonValueKind.Null, user.GetProperty("sheet").GetProperty("duration_minutes").ValueKind);
        Assert.Equal(45m, (await StepReadAsync(project, facilitator, table)).GetProperty("sheet").GetProperty("duration_minutes").GetDecimal());
        await ValidateAsync(facilitator, table);
        Assert.Equal(45m, (await StepReadAsync(project, reader, table)).GetProperty("sheet").GetProperty("duration_minutes").GetDecimal());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_frequency_of_the_sheet_comes_with_the_map_as_a_draft_for_the_facilitator_and_validated_only_for_the_reader(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;
        var step = (await project.AddStepAsync(0, "Suivi journalier des ventes")).Id;
        async Task<string?> FrequencyAsync(ApiClient client) => (await StepReadAsync(project, client, step)).GetProperty("sheet").GetProperty("frequency").GetString();
        Assert.Null(await FrequencyAsync(facilitator));

        await SaveSheetAsync(facilitator, step, new { frequency = new { value = "daily" } });
        Assert.Equal("daily", await FrequencyAsync(facilitator));
        Assert.Null(await FrequencyAsync(reader));

        await ValidateAsync(facilitator, step);
        Assert.Equal("daily", await FrequencyAsync(reader));
        // A draft after the validation: the reader keeps the validated version.
        await SaveSheetAsync(facilitator, step, new { frequency = new { value = "month_end_close" } });
        Assert.Equal("month_end_close", await FrequencyAsync(facilitator));
        Assert.Equal("daily", await FrequencyAsync(reader));
    }
}
