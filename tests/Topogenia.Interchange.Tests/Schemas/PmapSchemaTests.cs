// PmapSchemaTests.cs
// The published schema of the .pmap file, by itself and not only the reader of the host, refuses the faults common in a file written by hand or by an AI, and imposes its format and version (ADR 054).

using System.Text.Json;
using System.Text.Json.Nodes;
using Topogenia.Interchange.Pmap;
using Topogenia.Interchange.Schemas;
using Topogenia.Interchange.Tests.Support;
using static Topogenia.Interchange.Tests.Pmap.PmapReaderTests;

namespace Topogenia.Interchange.Tests.Schemas;

public sealed class PmapSchemaTests
{
    private static readonly SchemaValidator Schema =
        new(JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot.Path, "schemas", PublishedSchemas.PmapFile))).RootElement);

    /// <summary>The errors of the schema on <c>samples/minimal.pmap</c> changed as told, written « pointer:keyword ».</summary>
    private static List<string> Errors(Action<JsonObject> change)
    {
        var document = JsonNode.Parse(Sample("minimal.pmap"))!.AsObject();
        document["version"] = PmapFormat.Version;
        change(document);
        return [.. Schema.Validate(JsonSerializer.SerializeToElement(document)).Select(error => error.Path + ":" + error.Keyword)];
    }

    private static JsonObject Item(JsonObject document, string list, int index) => document["map"]![list]![index]!.AsObject();

    [Fact]
    public void The_schema_by_itself_refuses_the_common_faults_of_a_file_written_by_hand_or_by_an_ai()
    {
        var faults = new (string Fault, Action<JsonObject> Change)[]
        {
            ("unknown property", document => Item(document, "steps", 0)["lable"] = "x"),
            ("name missing", document => document["project"]!.AsObject().Remove("name")),
            ("unknown type", document => Item(document, "steps", 0)["type"] = "rectangle"),
            ("negative column", document => Item(document, "steps", 0)["column"] = -1),
            ("colour", document => Item(document, "lanes", 0)["color"] = "bleu"),
            ("HTML tag", document => Item(document, "lanes", 0)["name"] = "<b>x</b>"),
            ("blank label", document => Item(document, "steps", 0)["label"] = "  "),
            ("flow label too long", document => Item(document, "flows", 0)["label"] = new string('x', 61)),
            ("no lane", document => document["map"]!["lanes"] = new JsonArray()),
            ("RACI role", document => Item(document, "steps", 1)["raci"] = new JsonObject { ["c2"] = "X" }),
            ("route side", document => Item(document, "flows", 0)["route"] = new JsonObject { ["from"] = "up" }),
            ("route unknown", document => Item(document, "flows", 0)["route"] = new JsonObject { ["via"] = new JsonObject { ["col"] = 2 } }),
            ("route aisle", document => Item(document, "flows", 0)["route"] = new JsonObject { ["via"] = new JsonObject { ["aisle"] = -2 } }),
            ("route empty", document => Item(document, "flows", 0)["route"] = new JsonObject()),
            ("route edge", document => Item(document, "flows", 0)["route"] = new JsonObject
            {
                ["via"] = new JsonObject { ["lane"] = "c1", ["row"] = 0, ["edge"] = "left" },
            }),
        };

        Assert.Empty(Errors(_ => { }));
        Assert.Equal(
            [
                "unknown property: /map/steps/0/lable:additionalProperties",
                "name missing: /project/name:required",
                "unknown type: /map/steps/0/type:enum",
                "negative column: /map/steps/0/column:minimum",
                "colour: /map/lanes/0/color:pattern",
                "HTML tag: /map/lanes/0/name:not",
                "blank label: /map/steps/0/label:pattern",
                "flow label too long: /map/flows/0/label:maxLength",
                "no lane: /map/lanes:minItems",
                "RACI role: /map/steps/1/raci/c2:enum",
                "route side: /map/flows/0/route/from:enum",
                "route unknown: /map/flows/0/route/via/col:additionalProperties, /map/flows/0/route/via:not",
                "route aisle: /map/flows/0/route/via/aisle:minimum",
                "route empty: /map/flows/0/route:not",
                "route edge: /map/flows/0/route/via/edge:enum",
            ],
            faults.Select(fault => fault.Fault + ": " + string.Join(", ", Errors(fault.Change))));
    }

    [Fact]
    public void The_schema_imposes_its_format_and_the_version_1_3_present_both()
    {
        Assert.Empty(Errors(document => Item(document, "flows", 0)["route"] = new JsonObject
        {
            ["from"] = "top", ["to"] = "bottom", ["via"] = new JsonObject { ["lane"] = "c1", ["row"] = 0, ["edge"] = "top" },
        }));
        Assert.Equal(["/version:const"], Errors(document => document["version"] = "1.2"));
        Assert.Empty(Errors(document => document["format"] = PmapFormat.Name));
        Assert.Equal(["/format:const"], Errors(document => document["format"] = "bpmn"));
        Assert.Equal(["/format:required"], Errors(document => document.Remove("format")));
        Assert.Equal(["/version:required"], Errors(document => document.Remove("version")));
        Assert.Equal(["/version:const"], Errors(document => document["version"] = "2.0"));
        Assert.Equal(["/version:const"], Errors(document => document["version"] = "1.1"));
    }
}
