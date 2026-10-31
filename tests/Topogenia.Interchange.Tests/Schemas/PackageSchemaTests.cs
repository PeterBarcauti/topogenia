// PackageSchemaTests.cs
// The published schema of the package (spec 7.2): the written entries follow it, it is the embedded one, it holds the limits
// of the domain and refuses the hostile texts.

using System.Text.Json;
using System.Text.Json.Nodes;
using Topogenia.Domain.Documents;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Structure;
using Topogenia.Interchange.Packages;
using Topogenia.Interchange.Schemas;
using Topogenia.Interchange.Tests.Support;

namespace Topogenia.Interchange.Tests.Schemas;

public sealed class PackageSchemaTests
{
    private static readonly JsonElement EmptyObject = JsonSerializer.SerializeToElement(new JsonObject());

    private static string SchemaPath => Path.Combine(RepositoryRoot.Path, "schemas", PackageFormat.SchemaFile);

    /// <summary>The required properties of a definition, written as the errors of an object that has none of them.</summary>
    private static string Required(JsonElement definition) => string.Join(", ", definition.GetProperty("required").EnumerateArray()
        .Select(name => "/" + name.GetString() + ":required").Order(StringComparer.Ordinal));

    /// <summary>The errors of an empty object as an entry of the package.</summary>
    private static string MissingFromAnEmptyObject(string path) => string.Join(", ", PackageDefinition.Current.Validate(path, EmptyObject)
        .Select(error => error.Path + ":" + error.Keyword).Order(StringComparer.Ordinal));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Every_entry_written_by_an_export_follows_the_published_schema(bool withoutPeople)
    {
        var sample = new SampleProject();
        var files = new PackageFiles(PackageWriter.Write(sample.Workspace(), sample.Logins, new PackageOptions(withoutPeople),
            new PackageStamp(SampleProject.At(300), "EXEMPLE\\alice")));

        foreach (var path in files.Paths)
            Assert.Empty(PackageDefinition.Current.Validate(path, files.Element(path)));
    }

    [Fact]
    public void The_embedded_schema_is_the_published_file()
    {
        using var stream = typeof(PackageSchema).Assembly.GetManifestResourceStream("Schemas/" + PackageFormat.SchemaFile)!;
        using var embedded = new MemoryStream();
        stream.CopyTo(embedded);

        Assert.Equal(File.ReadAllBytes(SchemaPath), embedded.ToArray());
    }

    /// <summary>
    /// Read through <see cref="PackageDefinition.Validate"/>, the check of the import: an empty object breaks exactly the
    /// required properties of the root for the manifest, and those of the definition named after its file for an entry.
    /// </summary>
    [Fact]
    public void The_schema_describes_the_manifest_at_its_root_and_each_entry_by_the_definition_named_after_its_file()
    {
        using var schema = JsonDocument.Parse(File.ReadAllBytes(SchemaPath));
        var definitions = schema.RootElement.GetProperty("$defs");
        string[] named = ["manifest", "project", "members", "maps", "sheets", "documents", "painpoints", "translations"];

        Assert.Equal(named.Select(name => Required(definitions.GetProperty(name))),
            PackageFormat.Entries.Prepend(PackageFormat.Manifest).Select(MissingFromAnEmptyObject));
        Assert.Throws<ArgumentException>(() => PackageDefinition.Current.Validate("../evil.json", EmptyObject));
        Assert.Equal(PackageFormat.Name, definitions.GetProperty("manifest").GetProperty("properties").GetProperty("format")
            .GetProperty("const").GetString());
    }

    [Fact]
    public void A_manifest_of_another_format_or_with_a_wrong_sha256_shape_breaks_the_schema()
    {
        var manifest = Manifest();
        manifest["format"] = "ariane";
        manifest["entries"]![0]!["sha256"] = "ABC";
        manifest["surplus"] = 1;

        var errors = PackageDefinition.Current.Validate("manifest.json", JsonSerializer.SerializeToElement(manifest));

        Assert.Equal(["/entries/0/sha256:pattern", "/format:const", "/surplus:additionalProperties"],
            errors.Select(error => error.Path + ":" + error.Keyword).Order(StringComparer.Ordinal));
    }

    /// <summary>The limits of the schema are those of the domain: the longest text passes, one character more is refused.</summary>
    [Theory]
    [InlineData("label", StructureEdits.StepLabelMax)]
    [InlineData("lane", StructureEdits.LaneNameMax)]
    [InlineData("flow", StructureEdits.FlowLabelMax)]
    [InlineData("map", StructureEdits.MapNameMax)]
    public void The_text_limits_of_the_structure_are_those_of_the_domain(string field, int max)
    {
        Assert.Empty(MapsWith(field, new string('a', max)));
        Assert.Contains(MapsWith(field, new string('a', max + 1)), error => error.Keyword == "maxLength");
    }

    [Theory]
    [InlineData("name", ProjectCreation.NameMax)]
    [InlineData("description", ProjectCreation.DescriptionMax)]
    [InlineData("sharedFolder", ProjectCreation.SharedFolderMax)]
    [InlineData("sharingGroupLabel", ProjectCreation.SharingGroupLabelMax)]
    [InlineData("translationGlossary", ProjectCreation.TranslationGlossaryMax)]
    public void The_text_limits_of_the_project_are_those_of_the_domain(string field, int max)
    {
        Assert.Empty(ProjectWith(field, new string('a', max)));
        Assert.Contains(ProjectWith(field, new string('a', max + 1)), error => error.Keyword == "maxLength");
    }

    [Fact]
    public void The_text_limits_of_the_content_are_those_of_the_domain()
    {
        Assert.Equal(DocumentRules.NameMax, MaxLength("documents", "name"));
        Assert.Equal(DocumentRules.PainPointDescriptionMax, MaxLength("painpoints", "description"));
        Assert.Equal(SheetContentReader.FreeTextMax, MaxLength("sheets", "freeText"));
        Assert.Equal(SheetContentReader.TextMax, MaxLength("sheets", "purpose"));
        Assert.Equal(SheetWorkflow.ClarificationMax, MaxLength("sheets", "clarificationRequest"));
    }

    [Theory]
    [InlineData("<b>gras</b>", "not")]
    [InlineData("<!-- note -->", "not")]
    [InlineData("tab\u0007bell", "not")]
    [InlineData(" espace", "pattern")]
    [InlineData("espace ", "pattern")]
    [InlineData("", "pattern")]
    public void A_hostile_or_badly_trimmed_text_breaks_the_schema(string text, string keyword)
    {
        Assert.Contains(ProjectWith("name", text), error => error.Path == "/project/name" && error.Keyword == keyword);
    }

    [Theory]
    [InlineData("Ligne un\nLigne deux\ttabulée")]
    [InlineData("a < b et 3 > 2")]
    public void A_text_on_several_lines_or_with_a_lone_angle_bracket_follows_the_schema(string text)
    {
        Assert.Empty(ProjectWith("description", text));
    }

    private static JsonObject Manifest()
    {
        var sample = new SampleProject();
        var files = new PackageFiles(PackageWriter.Write(sample.Workspace(), sample.Logins, new PackageOptions(WithoutPeople: true), new PackageStamp(SampleProject.At(300), null)));
        return files.Json("manifest.json").AsObject();
    }

    private static IReadOnlyList<SchemaError> ProjectWith(string field, string value)
    {
        var sample = new SampleProject();
        var entry = PackageWriter.Entries(sample.Workspace(), sample.Logins, new PackageOptions(WithoutPeople: true)).Single(item => item.Path == "project.json");
        var node = JsonNode.Parse(entry.Content)!;
        node["project"]![field] = value;
        return PackageDefinition.Current.Validate("project.json", JsonSerializer.SerializeToElement(node));
    }

    private static IReadOnlyList<SchemaError> MapsWith(string field, string value)
    {
        var sample = new SampleProject();
        var entry = PackageWriter.Entries(sample.Workspace(), sample.Logins, new PackageOptions(WithoutPeople: true)).Single(item => item.Path == "maps.json");
        var root = JsonNode.Parse(entry.Content)!["maps"]![0]!;
        switch (field)
        {
            case "label":
                root["steps"]![0]!["label"] = value;
                break;
            case "lane":
                root["lanes"]![0]!["name"] = value;
                break;
            case "flow":
                root["flows"]![0]!["label"] = value;
                break;
            default:
                root["name"] = value;
                break;
        }
        return PackageDefinition.Current.Validate("maps.json", JsonSerializer.SerializeToElement(new JsonObject { ["maps"] = new JsonArray(root.DeepClone()) }));
    }

    /// <summary>The longest text that an item of an entry accepts for a field, found by trying the limits around it.</summary>
    private static int MaxLength(string definition, string field)
    {
        using var schema = JsonDocument.Parse(File.ReadAllBytes(SchemaPath));
        var item = schema.RootElement.GetProperty("$defs").GetProperty(definition).GetProperty("properties").EnumerateObject().Single().Value
            .GetProperty("items").GetProperty("properties").GetProperty(field);
        var reference = item.GetProperty("$ref").GetString()!;
        var text = schema.RootElement.GetProperty("$defs").GetProperty(reference["#/$defs/".Length..]);
        return text.GetProperty("allOf")[1].GetProperty("maxLength").GetInt32();
    }
}
