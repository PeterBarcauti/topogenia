// ActionPlanRoutesTests.cs
// The bounds of the duration of a sheet and the action plan of a pain point through the whole host on both databases (Makigami M5 and M7, plan 3.1): written, read back, refused, gathered by map.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Documents;

public sealed class ActionPlanRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static string Uses(Guid stepId, Guid documentId) => $"/api/steps/{stepId}/uses/{documentId}";

    private static string? Field(JsonElement details) => details.GetProperty("field").GetString();

    /// <summary>A step of the root map with one output, a second step that uses it, and the identifier of the output.</summary>
    private static async Task<(Guid Producer, Guid Consumer, Guid Document)> UsedAsync(ApiProject project)
    {
        var producer = await StepSheets.StepAsync(project, 0, "Extraire");
        var consumer = await StepSheets.StepAsync(project, 1, "Saisir");
        using var facilitator = project.Facilitator;
        var sheet = (await facilitator.PostAsync($"/api/steps/{producer}/documents", new { direction = "output", name = "Extraction" })).Ok();
        return (producer, consumer, sheet.GetProperty("outputs")[0].GetProperty("document").GetProperty("id").GetGuid());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_plan_of_a_pain_point_is_kept_with_it_and_gathered_in_the_action_plan_of_the_map(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var (producer, consumer, document) = await UsedAsync(project);
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;
        var sheet = (await facilitator.PutAsync(Uses(consumer, document), new
        {
            pain_point = "Saisie en double", lost_hours_per_month = 6, action = "Lire l'extraction", owner = "Service paie", due_date = "2026-12-15",
        }, StepSheets.NoRecord)).Ok();
        var painPoint = sheet.GetProperty("pain_points").EnumerateArray().Single();
        Assert.Equal(("Lire l'extraction", "Service paie", "2026-12-15"),
            (painPoint.GetProperty("action").GetString(), painPoint.GetProperty("owner").GetString(), painPoint.GetProperty("due_date").GetString()));

        var plan = (await reader.GetAsync($"/api/maps/{project.RootMapId}/action-plan")).Ok().GetProperty("lines").EnumerateArray().ToList();
        var line = Assert.Single(plan);
        Assert.Equal((painPoint.GetProperty("code").GetString(), "Saisie en double", "reported", 6m),
            (line.GetProperty("code").GetString(), line.GetProperty("description").GetString(), line.GetProperty("status").GetString(),
                line.GetProperty("lost_hours_per_month").GetDecimal()));
        Assert.Equal(("Lire l'extraction", "Service paie", "2026-12-15"),
            (line.GetProperty("action").GetString(), line.GetProperty("owner").GetString(), line.GetProperty("due_date").GetString()));
        Assert.Equal((producer, "Extraire", project.RootMapId), (line.GetProperty("step_id").GetGuid(), line.GetProperty("step").GetProperty("text").GetString(),
            line.GetProperty("map_id").GetGuid()));
        Assert.Equal(JsonValueKind.Object, line.GetProperty("lane").ValueKind);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_plan_without_a_pain_point_is_refused_and_cleared_with_its_pain_point(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var (_, consumer, document) = await UsedAsync(project);
        using var facilitator = project.Facilitator;
        Assert.Equal("owner", Field((await facilitator.PutAsync(Uses(consumer, document), new { owner = "Paie" }, StepSheets.NoRecord)).Refused(400, "VALEUR_INVALIDE")));
        (await facilitator.PutAsync(Uses(consumer, document), new { pain_point = "Relance", action = "Prévenir" }, StepSheets.NoRecord)).Ok();
        var version = (await facilitator.GetAsync($"/api/steps/{consumer}/sheet")).Ok().GetProperty("inputs")[0].GetProperty("use").GetProperty("version").GetInt32();
        var cleared = (await facilitator.PutAsync(Uses(consumer, document), new { wait_hours = 1 }, version)).Ok();
        Assert.Equal(0, cleared.GetProperty("pain_points").GetArrayLength());
        Assert.Equal(0, (await facilitator.GetAsync($"/api/maps/{project.RootMapId}/action-plan")).Ok().GetProperty("lines").GetArrayLength());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_bounds_of_the_duration_are_kept_by_the_draft_and_refused_out_of_order(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var step = await StepSheets.StepAsync(project, 0);
        using var facilitator = project.Facilitator;
        object Fields(decimal min, decimal duration, decimal max) => new
        {
            fields = new { duration_min_minutes = new { number = min }, duration_minutes = new { number = duration }, duration_max_minutes = new { number = max } },
        };
        var refused = (await facilitator.PutAsync($"/api/steps/{step}/sheet", Fields(10m, 15m, 12m), StepSheets.NoRecord)).Refused(400, "VALEUR_INVALIDE");
        Assert.Equal("duration_max_minutes", Field(refused));
        var saved = (await facilitator.PutAsync($"/api/steps/{step}/sheet", Fields(10m, 15m, 25.5m), StepSheets.NoRecord)).Ok();
        var content = (await facilitator.GetAsync($"/api/steps/{step}/sheet")).Ok().GetProperty("content");
        Assert.Equal((10m, 15m, 25.5m), (content.GetProperty("duration_min_minutes").GetDecimal(), content.GetProperty("duration_minutes").GetDecimal(),
            content.GetProperty("duration_max_minutes").GetDecimal()));
        Assert.Equal(StepSheets.VersionOf(saved), StepSheets.VersionOf((await facilitator.GetAsync($"/api/steps/{step}/sheet")).Ok()));
    }
}
