// ProjectsAndSecurityTranslationPortedTests.cs
// The 1.x integration tests of the translation by workbook ported to the whole host on both databases (T-TRAD-01 to 03 and 05).

using System.Text.Json;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Ported;

public sealed class ProjectsAndSecurityTranslationPortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    /// <summary>A project in French: a lane translated by hand, two steps and a flow « oui » between them (1.x `Preparer`).</summary>
    private sealed record Plan(ApiProject Project, Guid Accounting, Guid First, Guid Second, Guid Flow);

    private async Task<Plan> PlanAsync(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        using var facilitator = project.Facilitator;
        var accounting = await project.AddLaneAsync("Comptabilité");
        (await facilitator.PatchAsync($"/api/lanes/{accounting}", new { translations = new Dictionary<string, string?> { ["en"] = "Accounting" } },
            await LaneVersionAsync(project, accounting))).Ok();
        var first = await project.AddStepAsync(0, "Saisir la facture");
        var second = await project.AddStepAsync(1, "Valider la facture", accounting);
        var flow = await project.AddFlowAsync(first.Id, second.Id, "oui");
        return new Plan(project, accounting, first.Id, second.Id, flow);
    }

    private static async Task<int> LaneVersionAsync(ApiProject project, Guid laneId) =>
        Lane(await project.MapAsync(), laneId).GetProperty("version").GetInt32();

    private static JsonElement Lane(JsonElement map, Guid laneId) =>
        map.GetProperty("lanes").EnumerateArray().Single(lane => lane.GetProperty("id").GetGuid() == laneId);

    private static string Url(ApiProject project) => $"/api/projects/{project.Id}/translations";

    private static async Task<IReadOnlyList<JsonElement>> TextsAsync(ApiProject project, string scope = "missing")
    {
        using var facilitator = project.Facilitator;
        return [.. (await facilitator.GetAsync($"{Url(project)}?scope={scope}&languages=en")).Ok().GetProperty("texts").EnumerateArray()];
    }

    private static JsonElement TextOf(IEnumerable<JsonElement> texts, Guid id) => texts.Single(text => text.GetProperty("id").GetGuid() == id);

    /// <summary>A row of the workbook as the client sends it back: the text as exported and its English translation.</summary>
    private static object English(int line, JsonElement text, string english) => new
    {
        line,
        type = text.GetProperty("type").GetString(),
        id = text.GetProperty("id").GetGuid(),
        original = text.GetProperty("original").GetString(),
        translations = new Dictionary<string, string> { ["en"] = english },
    };

    private static async Task<JsonElement> ImportAsync(ApiProject project, string scope, params object[] rows)
    {
        using var facilitator = project.Facilitator;
        return (await facilitator.PutAsync(Url(project), new { scope, rows })).Ok();
    }

    private static string? EnglishOf(JsonElement item) =>
        item.GetProperty("translations").TryGetProperty("en", out var english) ? english.GetString() : null;

    private static List<(int, string?)> Ignored(JsonElement reply) =>
        [.. reply.GetProperty("ignored").EnumerateArray().Select(row => (row.GetProperty("line").GetInt32(), row.GetProperty("reason").GetString()))];

    // T-TRAD-01
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_texts_without_translation_are_given_in_their_language_imported_in_one_pass_and_given_again_with_the_scope_auto(EngineKind kind)
    {
        var plan = await PlanAsync(kind);
        var missing = await TextsAsync(plan.Project);
        Assert.DoesNotContain(plan.Accounting, missing.Select(text => text.GetProperty("id").GetGuid()));
        Assert.Contains("project", missing.Select(text => text.GetProperty("type").GetString()));
        Assert.All(missing, text => Assert.Equal("fr", text.GetProperty("language").GetString()));
        var step = TextOf(missing, plan.First);
        var flow = TextOf(missing, plan.Flow);
        Assert.Equal(("step", "Saisir la facture"), (step.GetProperty("type").GetString(), step.GetProperty("original").GetString()));
        Assert.Equal(("flow", "oui"), (flow.GetProperty("type").GetString(), flow.GetProperty("original").GetString()));

        var imported = await ImportAsync(plan.Project, "missing", English(2, step, "Enter the invoice"), English(3, flow, "yes"));
        Assert.Equal((2, 0), (imported.GetProperty("written").GetInt32(), imported.GetProperty("ignored").GetArrayLength()));
        var map = await plan.Project.MapAsync();
        Assert.Equal("Enter the invoice", EnglishOf(StructureCalls.StepOf(map, plan.First)));
        Assert.Equal("yes", EnglishOf(StructureCalls.FlowOf(map, plan.Flow)));
        Assert.DoesNotContain(plan.First, (await TextsAsync(plan.Project)).Select(text => text.GetProperty("id").GetGuid()));
        var auto = await TextsAsync(plan.Project, "auto");
        Assert.Equal("auto", TextOf(auto, plan.First).GetProperty("sources").GetProperty("en").GetString());

        await ImportAsync(plan.Project, "auto", English(2, step, "Enter the invoice"), English(3, flow, "yes"));
        var replayed = await plan.Project.MapAsync();
        foreach (var part in new[] { "lanes", "steps", "flows", "translations" })
            Assert.Equal(map.GetProperty(part).GetRawText(), replayed.GetProperty(part).GetRawText());
        Assert.Equal(auto.Select(text => text.GetRawText()), (await TextsAsync(plan.Project, "auto")).Select(text => text.GetRawText()));
    }

    // T-TRAD-02
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_manual_translation_is_overwritten_only_with_the_scope_all_and_then_becomes_automatic(EngineKind kind)
    {
        var plan = await PlanAsync(kind);
        var lane = TextOf(await TextsAsync(plan.Project, "all"), plan.Accounting);
        Assert.Equal(("lane", "manual"), (lane.GetProperty("type").GetString(), lane.GetProperty("sources").GetProperty("en").GetString()));

        var kept = await ImportAsync(plan.Project, "auto", English(5, lane, "Accounts"));
        Assert.Equal(0, kept.GetProperty("written").GetInt32());
        Assert.Equal([(5, "TRADUCTION_MANUELLE")], Ignored(kept));
        Assert.Equal("Accounting", EnglishOf(Lane(await plan.Project.MapAsync(), plan.Accounting)));

        var overwritten = await ImportAsync(plan.Project, "all", English(5, lane, "Accounts"));
        Assert.Equal(1, overwritten.GetProperty("written").GetInt32());
        Assert.Equal("Accounts", EnglishOf(Lane(await plan.Project.MapAsync(), plan.Accounting)));
        Assert.Equal("auto", TextOf(await TextsAsync(plan.Project, "all"), plan.Accounting).GetProperty("sources").GetProperty("en").GetString());
    }

    // T-TRAD-03
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_row_whose_text_changed_since_the_export_whose_object_was_deleted_or_whose_cell_is_empty_is_ignored_with_its_reason(EngineKind kind)
    {
        var plan = await PlanAsync(kind);
        using var facilitator = plan.Project.Facilitator;
        var missing = await TextsAsync(plan.Project);
        var (first, second, flow) = (TextOf(missing, plan.First), TextOf(missing, plan.Second), TextOf(missing, plan.Flow));
        (await facilitator.PatchAsync($"/api/steps/{plan.First}", new { label = "Saisir la facture fournisseur" }, await plan.Project.StepVersionAsync(plan.First))).Ok();
        (await facilitator.DeleteAsync($"/api/flows/{plan.Flow}", await plan.Project.FlowVersionAsync(plan.Flow))).Ok();

        var reply = await ImportAsync(plan.Project, "missing", English(2, first, "Enter the invoice"), English(3, flow, "yes"), English(4, second, "  "));
        Assert.Equal(0, reply.GetProperty("written").GetInt32());
        Assert.Equal([(2, "TEXTE_MODIFIE"), (3, "OBJET_INTROUVABLE"), (4, "SANS_TRADUCTION")], Ignored(reply));
        var map = await plan.Project.MapAsync();
        Assert.Null(EnglishOf(StructureCalls.StepOf(map, plan.First)));
        Assert.Null(EnglishOf(StructureCalls.StepOf(map, plan.Second)));
    }

    // T-TRAD-05
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_detailed_map_follows_the_translation_of_its_step_a_rename_drops_it_and_an_unchanged_text_keeps_it(EngineKind kind)
    {
        var plan = await PlanAsync(kind);
        using var facilitator = plan.Project.Facilitator;
        var (subMap, _) = await plan.Project.DetailAsync(plan.Second);
        var missing = await TextsAsync(plan.Project);
        await ImportAsync(plan.Project, "missing", English(2, TextOf(missing, plan.Second), "Approve the invoice"), English(3, TextOf(missing, plan.First), "Enter the invoice"),
            English(4, TextOf(missing, plan.Flow), "yes"));
        Assert.Equal("Approve the invoice", (await plan.Project.MapAsync(subMap)).GetProperty("translations").GetProperty("en").GetString());

        (await facilitator.PatchAsync($"/api/steps/{plan.First}", new { label = "Saisir la facture" }, await plan.Project.StepVersionAsync(plan.First))).Ok();
        var flowVersion = StructureCalls.FlowOf(await plan.Project.MapAsync(), plan.Flow).GetProperty("version").GetInt32();
        (await facilitator.PatchAsync($"/api/flows/{plan.Flow}", new { label = "oui" }, flowVersion)).Ok();
        var unchanged = await plan.Project.MapAsync();
        Assert.Equal(("Enter the invoice", "yes"), (EnglishOf(StructureCalls.StepOf(unchanged, plan.First)), EnglishOf(StructureCalls.FlowOf(unchanged, plan.Flow))));

        (await facilitator.PatchAsync($"/api/steps/{plan.Second}", new { label = "Approuver la facture" }, await plan.Project.StepVersionAsync(plan.Second))).Ok();
        Assert.Empty(StructureCalls.StepOf(await plan.Project.MapAsync(), plan.Second).GetProperty("translations").EnumerateObject());
    }
}
