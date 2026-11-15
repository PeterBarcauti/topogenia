// SheetRoundTripTests.cs
// Load, operate, save, load again for sheets, documents, uses and translations, on both engines.

using System.Text.Json;
using Topogenia.Domain.Documents;
using Topogenia.Domain.Model;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Structure;
using Topogenia.Domain.SubMaps;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.RoundTrips;

/// <summary>
/// The part of the persistence of the sheets (annex A 2.10), documents and uses (2.11) and translations (2.14): rows
/// created, rewritten, updated in place or kept through a logical deletion, as the domain left them.
/// </summary>
public sealed class SheetRoundTripTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private async Task<StoreScenario> ScenarioAsync(EngineKind kind) => await StoreScenario.CreateAsync(await databases.OfAsync(kind));

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_empty_draft_writes_no_sheet_row(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var step = (await scenario.LinkedStepsAsync(1))[0];

        var saved = await scenario.RunAsync((workspace, context) =>
            SheetWorkflow.Save(workspace, context, step, new SheetInput(new Dictionary<string, SheetFieldValue?>(), [], null)));

        Assert.True(saved.Changes.IsEmpty);
        var stored = await scenario.LoadAsync();
        Assert.Empty(stored.All<StepSheet>());
        Assert.Empty(stored.All<SheetOperation>());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_procedure_is_rewritten_entirely_in_the_order_of_the_request_at_each_save(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var step = (await scenario.LinkedStepsAsync(1))[0];
        await scenario.RunAsync((workspace, context) => SheetWorkflow.Save(workspace, context, step, ScenarioSteps.Sheet("Order", "Open", "Check", "File")));

        var rewritten = await scenario.RunAsync((workspace, context) => SheetWorkflow.Save(workspace, context, step, ScenarioSteps.Sheet("Order", "File", "Open")));

        var stored = await scenario.AssertStoredAsync(rewritten);
        Assert.Equal(new[] { (1, "File"), (2, "Open") }, stored.All<SheetOperation>().OrderBy(row => row.Position).Select(row => (row.Position, row.Text)));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_validated_version_keeps_its_json_and_its_denormalised_frequency_and_duration(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var step = (await scenario.LinkedStepsAsync(1))[0];
        await scenario.RunAsync((workspace, context) => SheetWorkflow.Save(workspace, context, step, ScenarioSteps.Sheet("Order", "Open")));

        var validated = await scenario.RunAsync((workspace, context) => SheetWorkflow.Validate(workspace, context, step));

        var version = Assert.Single((await scenario.AssertStoredAsync(validated)).All<StepSheetVersion>());
        Assert.Equal((1, Frequency.Daily, 12.5m), (version.VersionNumber, version.Frequency, version.DurationMinutes));
        using var json = JsonDocument.Parse(version.ContentJson);
        Assert.Equal(new[] { "fields", "procedure", "filled_count" }, json.RootElement.EnumerateObject().Select(property => property.Name).Where(name => name != "free_text"));
        Assert.Equal("Order", json.RootElement.GetProperty("fields").GetProperty(SheetContentReader.PurposeKey).GetProperty("text").GetString());
        Assert.Equal("Open", json.RootElement.GetProperty("procedure")[0].GetProperty("text").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Ticking_creates_the_use_then_ticking_again_updates_the_same_row(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var steps = await scenario.LinkedStepsAsync(2);
        var document = await scenario.OutputOfAsync(steps[0]);
        await scenario.RunAsync((workspace, context) => DocumentUses.Use(workspace, context, steps[1], document, new DocumentUseInput(1, 50)));
        // AR-111: a use starts at the structure version of the operation that creates it.
        var created = Assert.Single((await scenario.LoadAsync()).All<DocumentUse>()).Version;

        var updated = await scenario.RunAsync((workspace, context) => DocumentUses.Use(workspace, context, steps[1], document, new DocumentUseInput(2.5m, 80)));

        var use = Assert.Single((await scenario.AssertStoredAsync(updated)).All<DocumentUse>());
        Assert.Equal((2.5m, (int?)80, created + 1), (use.WaitHours!.Value, use.PercentCompleteAccurate, use.Version));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_pain_point_of_a_use_is_updated_in_place_one_per_use(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var steps = await scenario.LinkedStepsAsync(2);
        var document = await scenario.OutputOfAsync(steps[0]);
        await scenario.RunAsync((workspace, context) => DocumentUses.Use(workspace, context, steps[1], document, new DocumentUseInput(1, PainPoint: "Typed twice")));

        var updated = await scenario.RunAsync((workspace, context) =>
            DocumentUses.Use(workspace, context, steps[1], document, new DocumentUseInput(1, PainPoint: "Typed three times")));

        var stored = await scenario.AssertStoredAsync(updated);
        var painPoint = Assert.Single(stored.All<PainPoint>());
        Assert.Equal(("Typed three times", (Guid?)steps[0]), (painPoint.Description, painPoint.StepId));
        Assert.Equal(painPoint.Id, Assert.Single(stored.All<DocumentUse>()).PainPointId);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Translations_of_every_object_field_and_source_are_read_back_as_written(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var steps = await scenario.LinkedStepsAsync(2);

        var translated = await scenario.RunAsync((workspace, context) =>
        {
            var flow = workspace.FlowsOf(workspace.RootMap.Id)[0].Id;
            var objects = new (TranslationObject Type, Guid Id)[]
            {
                (TranslationObject.Project, workspace.ProjectId), (TranslationObject.Map, workspace.RootMap.Id), (TranslationObject.Lane, workspace.FirstLane()),
                (TranslationObject.Step, steps[0]), (TranslationObject.Flow, flow),
            };
            foreach (var (type, id) in objects)
            {
                TranslationRules.WriteManual(workspace, context, type, id, [new ManualTranslation("en", $"{type} (en)")]);
                TranslationRules.WriteAutomatic(workspace, context, type, id, "de", $"{type} (de)");
            }
        });

        var stored = await scenario.AssertStoredAsync(translated);
        var rows = stored.All<Translation>().ToList();
        Assert.Equal(10, rows.Count);
        Assert.All(rows, row => Assert.Equal(row.ObjectType is TranslationObject.Step or TranslationObject.Flow ? TranslationField.Label : TranslationField.Name, row.Field));
        Assert.Equal(5, rows.Count(row => row.Source == TranslationSource.Manual && row.Language == "en"));
        Assert.Equal(5, rows.Count(row => row.Source == TranslationSource.Auto && row.Language == "de"));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_translation_survives_the_logical_deletion_of_its_step_and_comes_back_with_it(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var step = (await scenario.LinkedStepsAsync(2))[1];
        await scenario.RunAsync((workspace, context) =>
            TranslationRules.WriteManual(workspace, context, TranslationObject.Step, step, [new ManualTranslation("en", "Second step")]));
        var translation = Assert.Single((await scenario.LoadAsync()).All<Translation>());

        var deletion = await scenario.RunAsync((workspace, context) => MacroStepDeletion.DeleteStep(workspace, context, step));
        var deleted = await scenario.AssertStoredAsync(deletion);
        var undo = await scenario.RunAsync((workspace, context) => UndoReplay.Undo(workspace, context, deletion.Changes));

        Assert.NotNull(deleted.Step(step)!.DeletedAt);
        Assert.Equal(translation, Assert.Single(deleted.All<Translation>()));
        var restored = await scenario.AssertStoredAsync(undo);
        Assert.Null(restored.Step(step)!.DeletedAt);
        Assert.Equal(translation, Assert.Single(restored.All<Translation>()));
    }
}
