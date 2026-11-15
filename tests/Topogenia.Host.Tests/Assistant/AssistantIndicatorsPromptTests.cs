// AssistantIndicatorsPromptTests.cs
// The indicators given to the model (F-109): written now, then the reference of a target state, then each photo, each indicator before and now with the way the core says it goes; given to the practices that read and analyse, not to a change of the map.

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Assistant;
using Topogenia.Domain.Values;
using Topogenia.Domain.ValueStreams;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Assistant;

public sealed class AssistantIndicatorsPromptTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private const string Key = "sk-ant-test-0000000000000000";

    private static readonly StreamIndicators Now = new(31.96, 12.065, 19.17, 32.1, 0.6, 0.087, 5, 4.2, 1, 0.8, 3.5, 2);

    [Fact]
    public void The_indicators_are_written_now_then_the_reference_then_each_photo_before_and_now_with_their_way()
    {
        var reference = new AssistantPhoto("État actuel", SnapshotOrigin.Baseline, new DateTime(2026, 11, 8, 1, 0, 0, DateTimeKind.Utc),
            IndicatorComparison.Of(Now with { Handoffs = 7 }, Now));
        var photo = new AssistantPhoto("Avant l'atelier", SnapshotOrigin.Photo, new DateTime(2026, 9, 1, 9, 9, 0, DateTimeKind.Utc),
            IndicatorComparison.Of(Now with { LeadHours = 40, Coverage = null }, Now));

        var text = AssistantInstructions.IndicatorsOf(new AssistantIndicators(Now with { HandoffsPerCase = null }, [reference, photo], 7.5m));

        Assert.StartsWith("<indicators of=\"the main map of the project and its detailed maps, as the person sees them\" workday_hours=\"7.5\">\n"
            + "now | lead_hours 31.96 | handoff_wait_hours 12.065 | other_wait_hours 19.17 | work_minutes 32.1 | value_added_share 0.6 | coverage 0.087 | "
            + "handoffs 5 | handoffs_per_case unknown | media_breaks 1 | rolled_complete_accurate 0.8 | lost_hours_per_month 3.5 | open_pain_points 2\n"
            + "reference of the target state \"État actuel\" of 2026-11-08 | lead_hours 31.96 -> 31.96 |", text, StringComparison.Ordinal);
        Assert.Contains("| handoffs 7 -> 5 better |", text, StringComparison.Ordinal);
        Assert.Contains("\nphoto \"Avant l'atelier\" of 2026-09-01 | lead_hours 40 -> 31.96 better |", text, StringComparison.Ordinal);
        Assert.Contains("| coverage unknown -> 0.087 |", text, StringComparison.Ordinal);
        Assert.EndsWith("open_pain_points 2 -> 2\n</indicators>", text, StringComparison.Ordinal);
    }

    private static int VersionOf(JsonElement map, string list, Guid id) =>
        map.GetProperty(list).EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == id).GetProperty("version").GetInt32();

    private static object Ask(string intent, string request) =>
        new { intent, request, selection = Array.Empty<string>(), history = Array.Empty<object>() };

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_question_is_given_the_indicators_as_the_person_sees_them_and_the_photo_and_a_change_of_the_map_is_not(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var project = await ApiProject.CreateAsync(host);
        using var admin = host.As(Logins.Admin);
        var version = (await admin.GetAsync("/api/admin/assistant")).Ok().GetProperty("version").GetInt32();
        (await admin.PutAsync("/api/admin/assistant", new { connector = "anthropic", secret = Key, model = (string?)null, region = (string?)null,
            allowed_classifications = new[] { "internal" } }, version)).Ok();
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;
        var start = await project.AddStepAsync(0, "Demande reçue", type: "start");
        var work = await project.AddStepAsync(1, "Traiter la demande");
        await project.AddFlowAsync(start.Id, work.Id);
        var map = await project.MapAsync();
        (await facilitator.PatchAsync($"/api/steps/{start.Id}", new { volume = new { quantity = 120, period = "month" } }, VersionOf(map, "steps", start.Id))).Ok();
        // A draft sheet: its duration counts for the facilitator, not for a reader, as on the panel « Analyse ».
        (await facilitator.PutAsync($"/api/steps/{work.Id}/sheet", new { fields = new { duration_minutes = new { number = 30 } } },
            await StepSheets.ReadVersionAsync(facilitator, work.Id))).Ok();
        (await facilitator.PostAsync($"/api/projects/{project.Id}/snapshots", new { label = "Avant l'atelier" })).Ok();
        var connector = host.Services.GetServices<IAssistantConnector>().OfType<TestConnector>().Single(item => item.Kind == AssistantConnector.Anthropic);
        const string Answer = """{"form":"answer","message":"Rien ne bouge.","cited":[],"questions":[],"findings":[],"pmap":null}""";

        connector.Next(Answer);
        Assert.Equal(200, (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/exchanges", Ask("ask", "Qu'a-t-on gagné ?"))).Status);
        var prompt = connector.Prompts[^1];
        Assert.Equal(30d, prompt.Indicators!.Now.WorkMinutes!.Value, 9);
        var photo = Assert.Single(prompt.Indicators.Photos);
        Assert.Equal(("Avant l'atelier", 12), (photo.Label, photo.Changes.Count));
        Assert.Contains("\nphoto \"Avant l'atelier\" of ", AssistantInstructions.MessageOf(prompt), StringComparison.Ordinal);

        connector.Next(Answer);
        Assert.Equal(200, (await reader.PostAsync($"/api/maps/{project.RootMapId}/assistant/exchanges", Ask("ask", "Qu'a-t-on gagné ?"))).Status);
        Assert.Null(connector.Prompts[^1].Indicators!.Now.WorkMinutes);
        Assert.Contains(" | work_minutes unknown | ", AssistantInstructions.MessageOf(connector.Prompts[^1]), StringComparison.Ordinal);

        connector.Next(Answer);
        var changed = await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/exchanges", Ask("modify", "Ajoute une étape."));
        Assert.Equal(200, changed.Status);
        Assert.Null(connector.Prompts[^1].Indicators);
        Assert.DoesNotContain("<indicators", AssistantInstructions.MessageOf(connector.Prompts[^1]), StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(changed.Body.Split('\n', StringSplitOptions.RemoveEmptyEntries)[^1]).RootElement.ValueKind);
    }
}
