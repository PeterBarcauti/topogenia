// PmapCopyTests.cs
// The copy of a project as a .pmap (A-2.17-43 to 49, AR-93): codes as identifiers, reading order, visible RACI, sheets as seen, readable writing.

using System.Text.RegularExpressions;
using Topogenia.Domain.Model;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.SubMaps;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Interchange.Pmap;
using static Topogenia.Interchange.Tests.Pmap.PmapBuildTests;
using static Topogenia.Interchange.Tests.Pmap.PmapReaderTests;

namespace Topogenia.Interchange.Tests.Pmap;

public sealed class PmapCopyTests
{
    private static readonly DateTime At = new(2026, 10, 25, 15, 30, 0, DateTimeKind.Utc);

    private static readonly SheetViewer Facilitator = new(false, true);

    private static readonly SheetViewer Reader = new(false, false);

    private static string Copy(ProjectWorkspace workspace, SheetViewer? viewer = null) =>
        PmapWriter.Write(PmapOutline.Of(workspace, new PmapOutlineOptions(viewer is not null, viewer is null ? null : _ => viewer)).Document, "Test", At);

    [Fact]
    public void The_copy_of_a_project_built_from_its_copy_is_the_same_text_sheets_included()
    {
        var (_, first) = Build(Read(Sample("cloture-mensuelle.pmap")));
        var copy = Copy(first, Facilitator);

        var (_, second) = Build(Read(copy));

        Assert.Equal(copy, Copy(second, Facilitator));
        Assert.Equal(Copy(first), Copy(second));
    }

    [Fact]
    public void The_copy_names_each_element_by_its_code_in_reading_order_and_an_output_by_the_code_of_its_step()
    {
        var (_, workspace) = Build(Read(Sample("cloture-mensuelle.pmap")));

        var document = PmapOutline.Of(workspace, new PmapOutlineOptions(true, _ => Facilitator)).Document;

        Assert.Equal(["VTE-C01", "VTE-C02", "VTE-C03", "VTE-C04"], document.Map.Lanes.Select(lane => lane.Id));
        Assert.Equal(Enumerable.Range(1, 8).Select(n => $"VTE-E{n:D3}"), document.Map.Steps.Select(step => step.Id));
        Assert.Equal(["VTE-E001", "VTE-E002"], new[] { document.Map.Flows[0].Source, document.Map.Flows[0].Target });
        var table = document.Map.Steps.Single(step => step.Label == "Construire le tableau croisé");
        Assert.Equal("VTE-E003-S1", table.Sheet!.Outputs.Single().Id);
        Assert.Equal("VTE-E003-S1", document.Map.Steps.Single(step => step.Label.StartsWith("Compléter", StringComparison.Ordinal)).Sheet!.Uses.Single().Document);
        Assert.Equal(["VTE-E009", "VTE-E010", "VTE-E011"], table.SubMap!.Steps.Select(step => step.Id));
    }

    [Fact]
    public void Without_the_option_the_copy_carries_no_sheet()
    {
        var (_, workspace) = Build(Read(Sample("cloture-mensuelle.pmap")));

        var copy = Copy(workspace);

        Assert.DoesNotContain("\"sheet\"", copy, StringComparison.Ordinal);
        Assert.All(PmapOutline.Of(workspace, new PmapOutlineOptions(false)).Document.Map.Steps, step => Assert.Null(step.Sheet));
    }

    [Fact]
    public void A_reader_takes_the_latest_validated_version_of_a_sheet_never_its_draft()
    {
        var (_, workspace) = Build(Read(Sample("cloture-mensuelle.pmap")));
        var table = workspace.All<Step>().Single(step => step.Label == "Construire le tableau croisé");
        var context = Context();
        SheetWorkflow.Validate(workspace, context, table.Id);
        SheetWorkflow.Save(workspace, context, table.Id, new SheetInput(new Dictionary<string, SheetFieldValue?>
        {
            [SheetContentReader.PurposeKey] = new("Brouillon en cours", null),
        }, [], null));

        Assert.Equal("Brouillon en cours", Sheet(workspace, Facilitator).Purpose);
        Assert.Equal("Donner à la direction les ventes du mois par région.", Sheet(workspace, Reader).Purpose);

        var never = Build(Read(Sample("minimal.pmap"))).Workspace;
        SheetWorkflow.Save(never, context, never.All<Step>().First(step => step.StepType == StepType.Task).Id, new SheetInput(
            new Dictionary<string, SheetFieldValue?> { [SheetContentReader.PurposeKey] = new("Jamais validé", null) }, [], null));
        Assert.All(PmapOutline.Of(never, new PmapOutlineOptions(true, _ => Reader)).Document.Map.Steps, step => Assert.Null(step.Sheet));
    }

    private static PmapSheet Sheet(ProjectWorkspace workspace, SheetViewer viewer) =>
        PmapOutline.Of(workspace, new PmapOutlineOptions(true, _ => viewer)).Document.Map.Steps.Single(step => step.Label == "Construire le tableau croisé").Sheet!;

    [Fact]
    public void The_copy_keeps_the_visible_raci_only_and_leaves_out_the_empty_sub_maps()
    {
        var (_, workspace) = Build(Read(Sample("cloture-mensuelle.pmap")));
        var root = workspace.RootMap.Id;
        var validation = workspace.StepsOf(root).Single(step => step.Label == "Valider le reporting");
        // A role on the lane of its own step is hidden by its R (annex A 6), as after a move of the step.
        workspace.Put(new StepRaci(validation.Id, validation.LaneId, RaciRole.C, At, Creator));
        var analyser = workspace.StepsOf(root).Single(step => step.Label == "Analyser les écarts");
        SubMapRules.Detail(workspace, Context(), analyser.Id);

        var document = PmapOutline.Of(workspace, new PmapOutlineOptions(false)).Document;

        Assert.Equal(new Dictionary<string, string> { ["VTE-C01"] = "I", ["VTE-C03"] = "I" }, document.Map.Steps.Single(step => step.Label == "Valider le reporting").Raci);
        Assert.Null(document.Map.Steps.Single(step => step.Label == "Analyser les écarts").SubMap);
    }

    [Fact]
    public void Sub_maps_beyond_twelve_levels_are_left_out_and_the_cut_is_reported()
    {
        static string Nested(int levels, int level = 1) => level > levels
            ? $$"""{ "lanes": [{ "id": "l{{level}}", "name": "X" }], "steps": [{ "id": "s{{level}}", "lane": "l{{level}}", "label": "Fin" }] }"""
            : $$"""{ "lanes": [{ "id": "l{{level}}", "name": "X" }], "steps": [{ "id": "s{{level}}", "lane": "l{{level}}", "label": "N", "sub_map": {{Nested(levels, level + 1)}} }] }""";
        var (_, workspace) = Build(Read(Doc(Nested(11))));
        Assert.False(PmapOutline.Of(workspace, new PmapOutlineOptions(false)).DepthCut);
        var deepest = workspace.All<Step>().Single(step => step.Label == "Fin");
        var detail = SubMapRules.Detail(workspace, Context(), deepest.Id);
        workspace.Put(new Step(Guid.NewGuid(), detail.MapId, detail.FirstLaneId!.Value, "PRJ-E099", "Treizième", "fr", StepType.Task, 0, 0, null,
            AutomationStatus.Manual, null, At, Creator, null, null, null, null, 1));

        var outlined = PmapOutline.Of(workspace, new PmapOutlineOptions(false));

        Assert.True(outlined.DepthCut);
        static int Depth(PmapMap map) => 1 + map.Steps.Select(step => step.SubMap is null ? 0 : Depth(step.SubMap)).DefaultIfEmpty(0).Max();
        Assert.Equal(12, Depth(outlined.Document.Map));
    }

    [Fact]
    public void Translations_in_the_language_of_origin_are_not_written()
    {
        var (_, workspace) = Build(Read(Sample("cloture-mensuelle.pmap")));
        var step = workspace.StepsOf(workspace.RootMap.Id)[0];
        workspace.Put(new Translation(TranslationObject.Step, step.Id, TranslationField.Label, "fr", "Doublon", TranslationSource.Manual, At, Creator));

        var copied = PmapOutline.Of(workspace, new PmapOutlineOptions(false)).Document.Map.Steps.Single(item => item.Id == step.Code);

        Assert.DoesNotContain("fr", copied.Translations.Keys);
    }

    [Fact]
    public void The_writing_is_readable_lf_two_spaces_accents_as_they_are_and_no_default_value()
    {
        var text = PmapWriter.Write(Read(Sample("cloture-mensuelle.pmap")), "Test", At);

        Assert.Equal(text, PmapWriter.Write(Read(text), "Test", At));
        Assert.Contains("\"generated_at\": \"2026-10-25T15:30:00Z\"", text, StringComparison.Ordinal);
        Assert.Contains("\"format\": \"topogenia\",\n  \"version\": \"1.2\"", text, StringComparison.Ordinal);
        Assert.Contains("\"label\": \"Importer l'extraction dans le tableur\"", text, StringComparison.Ordinal);
        Assert.Contains("\"name\": \"Clôture mensuelle des ventes\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
        Assert.EndsWith("}\n", text, StringComparison.Ordinal);
        Assert.Contains("\n  \"project\": {\n    \"code\": \"VTE\",", text, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(text, "\"automation\""));
        Assert.DoesNotContain("\"row\"", text, StringComparison.Ordinal);
        Assert.Contains("{\n        \"source\": \"debut\",\n        \"target\": \"extraire\"\n      }", text, StringComparison.Ordinal);
        Assert.Contains("\"duration_minutes\": 90,", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_writing_escapes_what_must_be()
    {
        var document = Read(Doc("""{ "lanes": [{ "id": "c\"1", "name": "Guillemets \" et barre \\ " }] }"""));

        var text = PmapWriter.Write(document, "Test", At);

        Assert.Contains("\"name\": \"Guillemets \\\" et barre \\\\\"", text, StringComparison.Ordinal);
        Assert.Equal("c\"1", Read(text).Map.Lanes[0].Id);
    }
}
