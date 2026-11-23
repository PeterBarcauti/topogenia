// CollectionDocumentationTests.cs
// The published schema of the template collection loads, the sample follows it and each of its processes the schema of its version; the templates of the gallery open alone and together (ADR 114).

using System.Text.Json;
using System.Text.Json.Nodes;
using Topogenia.Domain.Raci;
using Topogenia.Interchange.Pmap;
using Topogenia.Interchange.Schemas;
using Topogenia.Interchange.Tests.Support;
using static Topogenia.Interchange.Tests.Pmap.PmapCollectionReaderTests;

namespace Topogenia.Interchange.Tests.Pmap;

public sealed class CollectionDocumentationTests
{
    private static string Text(params string[] path) => File.ReadAllText(Path.Combine([RepositoryRoot.Path, .. path])).ReplaceLineEndings("\n");

    private static SchemaValidator CollectionSchema() =>
        new(JsonDocument.Parse(Text("schemas", "topogenia-collection-1.0.schema.json")).RootElement);

    /// <summary>The published schema of the version a process declares (ADR 091).</summary>
    private static SchemaValidator SchemaOf(JsonNode process) =>
        new(JsonDocument.Parse(Text("schemas", PublishedSchemas.For(process["version"]!.GetValue<string>()))).RootElement);

    private static List<SchemaError> Validate(SchemaValidator schema, string json) => [.. schema.Validate(JsonDocument.Parse(json).RootElement)];

    [Fact]
    public void The_published_schema_of_the_collection_loads_and_the_sample_follows_it_and_each_process_its_own_schema()
    {
        var schema = CollectionSchema();
        var sample = PmapReaderTests.Sample("exemple.pmaps");

        Assert.Empty(Validate(schema, sample));
        var processes = JsonNode.Parse(sample)!["processes"]!.AsArray();
        Assert.Equal(2, processes.Count);
        Assert.All(processes, process => Assert.Empty(SchemaOf(process!).Validate(JsonDocument.Parse(process!.ToJsonString()).RootElement)));
        // The two processes of the sample are the two samples of the .pmap, as they are (format-collection.md).
        Assert.True(JsonNode.DeepEquals(processes[0], JsonNode.Parse(PmapReaderTests.Sample("minimal.pmap"))));
        Assert.True(JsonNode.DeepEquals(processes[1], JsonNode.Parse(PmapReaderTests.Sample("cloture-mensuelle.pmap"))));

        // The schema refuses what the reader refuses at the envelope: what it does not describe is the process.
        Assert.NotEmpty(Validate(schema, Collection([Process("Un")], """, "language": "fr" """)));
        // HTML in the description, refused by the reader (PmapCollectionReaderTests), refused by the schema (audit 3.4, P3-04).
        Assert.NotEmpty(Validate(schema, Collection([Process("Un")], """, "description": "<script>x</script>" """)));
        Assert.Empty(Validate(schema, Collection([Process("Un")], """, "description": "Achats : 3 < 4" """)));
        Assert.NotEmpty(Validate(schema, Collection([])));
        Assert.NotEmpty(Validate(schema, Collection([Process("Un"), "42"])));
        Assert.NotEmpty(Validate(schema, Collection(Enumerable.Range(0, PmapFormat.ProcessesMax + 1).Select(i => Process($"P{i}")))));
        Assert.Empty(Validate(schema, Collection(Enumerable.Range(0, PmapFormat.ProcessesMax).Select(i => Process($"P{i}")))));
        Assert.NotEmpty(Validate(schema, Collection([Process("Un")]).Replace("topogenia-collection", "topogenia", StringComparison.Ordinal)));
        Assert.Equal(PmapFormat.ProcessesMax, JsonNode.Parse(Text("schemas", "topogenia-collection-1.0.schema.json"))!["properties"]!["processes"]!["maxItems"]!.GetValue<int>());
    }

    /// <summary>
    /// The templates of the gallery (lot 6a) as the site publishes them: the folder of the site is given by the bench that
    /// runs this test (TOPOGENIA_GALLERY_SITE), the French templates under modeles, the English ones under en/templates.
    /// </summary>
    private static readonly string? GallerySite = Environment.GetEnvironmentVariable("TOPOGENIA_GALLERY_SITE");

    [Fact]
    public void The_thirty_two_templates_of_the_gallery_open_alone_and_together_in_a_collection_by_language()
    {
        Assert.SkipUnless(GallerySite is not null && Directory.Exists(GallerySite), "TOPOGENIA_GALLERY_SITE does not name the folder of the site: this test runs where the site is built");
        var files = new[] { Path.Combine(GallerySite, "modeles"), Path.Combine(GallerySite, "en", "templates") }
            .SelectMany(folder => Directory.GetFiles(folder, "*.pmap", SearchOption.AllDirectories))
            .Where(file => file.EndsWith(".pmap", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(32, files.Count);
        // One folder per domain and per language.
        Assert.Equal(32, files.Select(Path.GetDirectoryName).Distinct(StringComparer.Ordinal).Count());

        foreach (var language in new[] { "fr", "en" })
        {
            var texts = files.Where(file => file.EndsWith($".{language}.pmap", StringComparison.Ordinal))
                .OrderBy(Path.GetFileName, StringComparer.Ordinal).Select(File.ReadAllText).ToList();
            Assert.Equal(16, texts.Count);
            var alone = texts.Select(text => Opened(text, language)).ToList();

            var collection = Collection(texts);
            Assert.Empty(Validate(CollectionSchema(), collection));
            var read = PmapCollectionReader.Read(collection, Settings);

            Assert.Empty(read.Errors);
            Assert.Equal(16, read.Processes.Count);
            Assert.All(read.Processes, process => Assert.Empty(process.Read.Errors));
            var at = new DateTime(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc);
            Assert.Equal(alone.Select(document => PmapWriter.Write(document, "essai", at)),
                read.Processes.Select(process => PmapWriter.Write(process.Read.Document!, "essai", at)));
        }
    }

    /// <summary>
    /// A template opened for good: read by the rules of the opening of a file, valid by the schema of its version, and built
    /// as a new project by the rules of the domain, with at least one sheet.
    /// </summary>
    private static PmapDocument Opened(string text, string language)
    {
        var read = PmapReader.Read(text, Settings);
        Assert.True(read.Document is not null, string.Join("\n", read.Errors.Select(error => $"{error.Path} : {error.Code}")));
        var document = read.Document!;
        Assert.Equal(language, document.Project.Language);
        Assert.Empty(SchemaOf(JsonNode.Parse(text)!).Validate(JsonDocument.Parse(text).RootElement));
        var built = PmapBuild.Run(PmapBuildTests.Context(RaciRules.KnownLetters), document, document.Project.Code ?? "PRJ", document.Project.Name,
            PmapBuildTests.Inputs());
        Assert.True(built.Counts.Steps > 0 && built.Counts.Sheets > 0, document.Project.Name);
        return document;
    }
}
