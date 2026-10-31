// PmapDocumentationTests.cs
// The examples of the .pmap documents open as they are and follow the published schema; the prompt for a sketch carries a copy of it (S-7.5-01, T-UPMA-03, T-FICR-01).

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Interchange.Pmap;
using Topogenia.Interchange.Schemas;
using Topogenia.Interchange.Tests.Support;
using static Topogenia.Interchange.Tests.Pmap.PmapBuildTests;
using static Topogenia.Interchange.Tests.Pmap.PmapReaderTests;

namespace Topogenia.Interchange.Tests.Pmap;

public sealed partial class PmapDocumentationTests
{
    /// <summary>The heading of the section of the prompt for a sketch that carries the copy of the published schema.</summary>
    private const string SchemaSection = "\n## Schéma du fichier\n";

    [GeneratedRegex("```json\\n(?<json>.*?)\\n```", RegexOptions.Singleline)]
    private static partial Regex JsonBlock();

    private static string Text(params string[] path) => File.ReadAllText(Path.Combine([RepositoryRoot.Path, .. path])).ReplaceLineEndings("\n");

    private static List<string> Blocks(string text) => [.. JsonBlock().Matches(text).Select(match => match.Groups["json"].Value)];

    private static List<string> Examples(params string[] path) => Blocks(Text(path));

    /// <summary>The JSON blocks of the prompt for a sketch on either side of its section of the schema: the examples before it, the copy of the schema in it.</summary>
    private static (List<string> Examples, List<string> Schema) PromptBlocks()
    {
        var text = Text("docs", "pmap", "PROMPT_croquis_vers_pmap.md");
        var section = text.IndexOf(SchemaSection, StringComparison.Ordinal);
        Assert.True(section >= 0, "The prompt for a sketch has no section of the schema.");
        return (Blocks(text[..section]), Blocks(text[section..]));
    }

    private static SchemaValidator Schema() =>
        new(JsonDocument.Parse(Text("schemas", PublishedSchemas.PmapFile)).RootElement);

    private static int Steps(PmapMap map) => map.Steps.Count + map.Steps.Sum(step => step.SubMap is null ? 0 : Steps(step.SubMap));

    [Fact]
    public void The_example_of_the_guide_for_an_ai_opens_as_it_is_with_its_rows_sheets_and_uncertainties()
    {
        var examples = Examples("docs", "pmap", "PMAP_GUIDE_IA.md");
        Assert.Equal(2, examples.Count);

        var document = Read(examples[0]);

        Assert.Equal(17, Steps(document.Map));
        Assert.Equal([0, 1], document.Map.Steps.Where(step => step.Column == 5 && step.Lane == "com").Select(step => step.Row));
        Assert.Equal(3, document.Uncertainties.Count);
        Assert.Equal(["saisir", "archiver"], document.Map.Steps.Where(step => step.Sheet is not null).Select(step => step.Id));
        var (built, workspace) = Build(document);
        Assert.Equal((2, 17, 2), (built.Counts.Maps, built.Counts.Steps, built.Counts.Sheets));
        var programmer = workspace.All<Step>().Single(step => step.Label == "Programmer le paiement");
        Assert.Equal([RaciRole.A, RaciRole.I], workspace.All<StepRaci>().Where(role => role.StepId == programmer.Id).Select(role => role.Role).Order());
        Assert.Equal("La pièce arrive sans le bon de réception.", Assert.Single(workspace.All<PainPoint>()).Description);
    }

    [Fact]
    public void The_story_converted_by_the_guide_opens_with_its_sheets_and_the_pain_point_of_the_use()
    {
        var document = Read(Examples("docs", "pmap", "PMAP_GUIDE_IA.md")[1]);

        var (built, workspace) = Build(document);

        Assert.Equal((4, 2), (built.Counts.Steps, built.Counts.Sheets));
        var count = workspace.All<Step>().Single(step => step.Label.StartsWith("Compter", StringComparison.Ordinal));
        var sheet = workspace.All<StepSheet>().Single(item => item.StepId == count.Id);
        Assert.Equal((Frequency.Weekly, 30m, SheetStatus.Draft), (sheet.Frequency!.Value, sheet.DurationMinutes!.Value, sheet.Status));
        var use = Assert.Single(workspace.All<DocumentUse>());
        Assert.Equal((80, 24m), (use.PercentCompleteAccurate!.Value, use.WaitHours!.Value));
        Assert.Equal((count.Id, 2m), (Assert.Single(workspace.All<PainPoint>()).StepId!.Value, workspace.All<PainPoint>().Single().LostHoursPerMonth!.Value));
    }

    [Fact]
    public void The_example_of_the_prompt_for_a_sketch_and_the_one_of_the_format_open_as_they_are()
    {
        var sketch = Read(Assert.Single(PromptBlocks().Examples));
        Assert.Equal((4, 10m), (sketch.Map.Steps.Count, sketch.Map.Steps[1].Sheet!.DurationMinutes!.Value));
        Assert.Equal(4, Read(Examples("docs", "format-pmap.md")[0]).Map.Steps.Count);
    }

    [Fact]
    public void The_prompt_for_a_sketch_carries_an_exact_copy_of_the_published_schema_after_its_valid_example()
    {
        var (examples, schema) = PromptBlocks();
        Assert.Equal(2, Examples("docs", "pmap", "PROMPT_croquis_vers_pmap.md").Count);
        var copy = Assert.Single(schema);

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(copy), JsonNode.Parse(Text("schemas", PublishedSchemas.PmapFile))),
            "The schema copied in the prompt for a sketch is not the published one.");
        var example = JsonDocument.Parse(Assert.Single(examples)).RootElement;
        Assert.Empty(Schema().Validate(example));
        Assert.Empty(new SchemaValidator(JsonDocument.Parse(copy).RootElement).Validate(example));
    }

    [Fact]
    public void The_example_of_the_format_is_the_minimal_sample()
    {
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(Examples("docs", "format-pmap.md")[0]), JsonNode.Parse(Sample("minimal.pmap"))),
            "The example of docs/format-pmap.md is not samples/minimal.pmap.");
    }

    [Fact]
    public void The_published_schema_loads_and_every_example_and_every_copy_follows_it()
    {
        var schema = Schema();
        var texts = Examples("docs", "pmap", "PMAP_GUIDE_IA.md").Concat(PromptBlocks().Examples)
            .Concat([Examples("docs", "format-pmap.md")[0], Sample("minimal.pmap"), Sample("cloture-mensuelle.pmap")]).ToList();
        var (_, workspace) = Build(Read(Sample("cloture-mensuelle.pmap")));
        texts.Add(PmapWriter.Write(PmapOutline.Of(workspace, new PmapOutlineOptions(true, _ => new Domain.Sheets.SheetViewer(false, true))).Document, "Test",
            new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc)));

        Assert.All(texts, text => Assert.Empty(schema.Validate(JsonDocument.Parse(text).RootElement)));
        var unknown = Sample("minimal.pmap").Replace("\"label\": \"Saisir la demande\"", "\"label\": \"Saisir\", \"lable\": \"x\"", StringComparison.Ordinal);
        Assert.NotEmpty(schema.Validate(JsonDocument.Parse(unknown).RootElement));
        Assert.NotEmpty(schema.Validate(JsonDocument.Parse(Sample("minimal.pmap").Replace("\"1.2\"", "\"1.0\"", StringComparison.Ordinal)).RootElement));
    }
}
