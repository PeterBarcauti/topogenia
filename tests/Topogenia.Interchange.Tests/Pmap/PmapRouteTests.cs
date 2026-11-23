// PmapRouteTests.cs
// The route a user chose for a flow in a .pmap 1.3: read and built by the rules of the domain, copied in 1.3 and opened again the same, a copy without one still in 1.2; unknown in 1.2, refused without columns, each part refused with its fault, by the reader and by the published schema; a point of a decision taken twice or its entry left to the drawing; never read in a proposal of the assistant, never sent in the map it rewrites (ADR 091).

using System.Text.Json;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Interchange.Pmap;
using Topogenia.Interchange.Schemas;
using Topogenia.Interchange.Tests.Support;
using static Topogenia.Interchange.Tests.Pmap.PmapBuildTests;
using static Topogenia.Interchange.Tests.Pmap.PmapReaderTests;

namespace Topogenia.Interchange.Tests.Pmap;

public sealed class PmapRouteTests
{
    private static readonly DateTime At = new(2026, 11, 3, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>A file 1.3 of three placed steps and a decision: a band and a departure, an aisle and an arrival, a flow left to the drawing.</summary>
    private static string Routed(string first = """{ "from": "top", "via": { "lane": "c2", "row": 0, "edge": "bottom" } }""",
        string second = """{ "to": "left", "via": { "aisle": -1 } }""", string version = "1.3", bool columns = true) => $$"""
        {
          "format": "topogenia",
          "version": "{{version}}",
          "project": { "name": "Tracés", "code": "TRC" },
          "map": {
            "lanes": [{{TwoLanes}}],
            "steps": [
              { "id": "e1", "lane": "c1", "label": "Recevoir"{{(columns ? ", \"column\": 0" : "")}} },
              { "id": "e2", "lane": "c1", "label": "Traiter"{{(columns ? ", \"column\": 1" : "")}} },
              { "id": "e3", "lane": "c2", "label": "Conforme ?", "type": "decision"{{(columns ? ", \"column\": 2" : "")}} },
              { "id": "e4", "lane": "c1", "label": "Classer"{{(columns ? ", \"column\": 3" : "")}} }
            ],
            "flows": [
              { "source": "e1", "target": "e2", "route": {{first}} },
              { "source": "e2", "target": "e3", "route": {{second}} },
              { "source": "e3", "target": "e4", "label": "oui" }
            ]
          }
        }
        """;

    private static Dictionary<string, Flow> FlowsByLabel(Domain.Workspace.ProjectWorkspace workspace) =>
        workspace.All<Flow>().Where(flow => flow.DeletedAt is null)
            .ToDictionary(flow => $"{workspace.Step(flow.SourceStepId)!.Label}>{workspace.Step(flow.TargetStepId)!.Label}", StringComparer.Ordinal);

    [Fact]
    public void A_route_of_a_file_1_3_is_read_then_built_by_the_rules_of_the_domain_the_lane_of_a_band_found_in_the_project()
    {
        var document = Read(Routed());
        Assert.Equal(new PmapRoute("top", null, new PmapPassage(null, "c2", 0, "bottom")), document.Map.Flows[0].Route);
        Assert.Equal(new PmapRoute(null, "left", new PmapPassage(-1, null, null, null)), document.Map.Flows[1].Route);
        Assert.Null(document.Map.Flows[2].Route);

        var (_, workspace) = Build(document);
        var flows = FlowsByLabel(workspace);
        var band = flows["Recevoir>Traiter"];
        Assert.Equal((FlowSide.Top, (FlowSide?)null, "Acheteur", 0, FlowSide.Bottom),
            (band.SourceSide!.Value, band.TargetSide, workspace.Lane(band.ViaLaneId!.Value)!.Name, band.ViaRow!.Value, band.ViaEdge!.Value));
        var aisle = flows["Traiter>Conforme ?"];
        Assert.Equal(((FlowSide?)null, FlowSide.Left, -1), (aisle.SourceSide, aisle.TargetSide!.Value, aisle.ViaAisle!.Value));
        Assert.False(flows["Conforme ?>Classer"].HasPassage);
    }

    [Fact]
    public void A_copy_writes_the_routes_in_1_3_and_opens_again_with_them_and_a_copy_without_any_stays_in_1_2()
    {
        var (_, workspace) = Build(Read(Routed()));
        var copy = PmapWriter.Write(PmapOutline.Of(workspace, new PmapOutlineOptions(false)).Document, "Test", At);
        Assert.Contains("\"version\": \"1.3\"", copy, StringComparison.Ordinal);
        var (_, again) = Build(Read(copy));
        Assert.Equal(
            FlowsByLabel(workspace).ToDictionary(pair => pair.Key, pair => (pair.Value.SourceSide, pair.Value.TargetSide, pair.Value.ViaAisle,
                workspace.Lane(pair.Value.ViaLaneId ?? Guid.Empty)?.Name, pair.Value.ViaRow, pair.Value.ViaEdge)),
            FlowsByLabel(again).ToDictionary(pair => pair.Key, pair => (pair.Value.SourceSide, pair.Value.TargetSide, pair.Value.ViaAisle,
                again.Lane(pair.Value.ViaLaneId ?? Guid.Empty)?.Name, pair.Value.ViaRow, pair.Value.ViaEdge)));

        var (_, plain) = Build(Read(Sample("cloture-mensuelle.pmap")));
        Assert.Contains("\"version\": \"1.2\"", PmapWriter.Write(PmapOutline.Of(plain, new PmapOutlineOptions(false)).Document, "Test", At), StringComparison.Ordinal);
    }

    [Fact]
    public void A_route_in_a_file_1_2_is_an_unknown_property()
    {
        var fault = Faults(Routed(version: "1.2")).First();
        Assert.Equal(("map.flows[0].route", FileErrorCodes.UnknownProperty), (fault.Path, fault.Code));
    }

    [Fact]
    public void A_passage_on_a_map_without_columns_is_refused_but_a_side_is_read()
    {
        var faults = Faults(Routed(columns: false));
        Assert.Equal([("map.flows[0].route.via", FileErrorCodes.PassageWithoutColumns), ("map.flows[1].route.via", FileErrorCodes.PassageWithoutColumns)],
            faults.Select(fault => (fault.Path, fault.Code)));
        var sides = Read(Routed(first: """{ "from": "top" }""", second: """{ "to": "left" }""", columns: false));
        Assert.Equal([new PmapRoute("top", null, null), new PmapRoute(null, "left", null)], sides.Map.Flows.Take(2).Select(flow => flow.Route));
    }

    [Fact]
    public void Each_part_of_a_route_out_of_its_rules_is_a_fault_at_its_place()
    {
        var cases = new (string Route, string Path, string Code)[]
        {
            ("""{ "from": "up" }""", "map.flows[0].route.from", FileErrorCodes.InvalidValue),
            ("""{ "to": "middle" }""", "map.flows[0].route.to", FileErrorCodes.InvalidValue),
            ("""{ "way": "x" }""", "map.flows[0].route.way", FileErrorCodes.UnknownProperty),
            ("""{ "via": { "aisle": -2 } }""", "map.flows[0].route.via.aisle", FileErrorCodes.InvalidValue),
            ("""{ "via": { "aisle": 10001 } }""", "map.flows[0].route.via.aisle", FileErrorCodes.InvalidValue),
            ("""{ "via": { "lane": "c9", "row": 0, "edge": "top" } }""", "map.flows[0].route.via.lane", FileErrorCodes.UnknownLane),
            ("""{ "via": { "lane": "c1", "row": 51, "edge": "top" } }""", "map.flows[0].route.via.row", FileErrorCodes.InvalidRow),
            ("""{ "via": { "lane": "c1", "row": 0, "edge": "left" } }""", "map.flows[0].route.via.edge", FileErrorCodes.InvalidValue),
            ("""{ "via": { "lane": "c1" } }""", "map.flows[0].route.via", FileErrorCodes.InvalidValue),
            ("""{ "via": { "aisle": 1, "lane": "c1", "row": 0, "edge": "top" } }""", "map.flows[0].route.via", FileErrorCodes.InvalidValue),
            ("""{ "via": { "aisle": 1, "column": 2 } }""", "map.flows[0].route.via", FileErrorCodes.InvalidValue),
            ("""{ "via": { "column": 2, "lane": "c1", "row": 0 } }""", "map.flows[0].route.via", FileErrorCodes.InvalidValue),
            ("""{ "via": { "edge": "top" } }""", "map.flows[0].route.via", FileErrorCodes.InvalidValue),
            ("""{ "via": { "column": 10001 } }""", "map.flows[0].route.via.column", FileErrorCodes.InvalidColumn),
            ("""{ "via": { "col": 1 } }""", "map.flows[0].route.via.col", FileErrorCodes.UnknownProperty),
        };
        foreach (var (route, path, code) in cases)
        {
            var faults = Faults(Routed(first: route)).Select(fault => (fault.Path, fault.Code)).ToList();
            Assert.True(faults.Contains((path, code)), $"{route}: {string.Join(", ", faults)}");
        }
    }

    [Fact]
    public void The_middle_of_a_column_and_the_middle_of_a_row_are_read_built_copied_and_opened_again_the_same()
    {
        var document = Read(Routed(first: """{ "via": { "column": 1 } }""", second: """{ "to": "left", "via": { "lane": "c1", "row": 0 } }"""));
        Assert.Equal(new PmapPassage(null, null, null, null) { Column = 1 }, document.Map.Flows[0].Route!.Via);
        Assert.Equal(new PmapPassage(null, "c1", 0, null), document.Map.Flows[1].Route!.Via);
        var (_, workspace) = Build(document);
        var flows = FlowsByLabel(workspace);
        Assert.Equal((1, (Guid?)null), (flows["Recevoir>Traiter"].ViaColumn!.Value, flows["Recevoir>Traiter"].ViaLaneId));
        var row = flows["Traiter>Conforme ?"];
        Assert.Equal(("Demandeur", 0, (FlowSide?)null), (workspace.Lane(row.ViaLaneId!.Value)!.Name, row.ViaRow!.Value, row.ViaEdge));
        var copy = PmapWriter.Write(PmapOutline.Of(workspace, new PmapOutlineOptions(false)).Document, "Test", At);
        Assert.Contains("\"column\": 1", copy, StringComparison.Ordinal);
        var again = FlowsByLabel(Build(Read(copy)).Workspace);
        Assert.Equal((1, 0, (FlowSide?)null), (again["Recevoir>Traiter"].ViaColumn!.Value, again["Traiter>Conforme ?"].ViaRow!.Value, again["Traiter>Conforme ?"].ViaEdge));
    }

    /// <summary>The file of <see cref="Routed"/>, the exits of its decision given these two routes.</summary>
    private static string Exits(string yes, string no) =>
        Routed(second: """{ "to": "left" }""").Replace("""{ "source": "e3", "target": "e4", "label": "oui" }""",
            $$"""{ "source": "e3", "target": "e4", "label": "oui", "route": {{yes}} }, { "source": "e3", "target": "e1", "label": "non", "route": {{no}} }""",
            StringComparison.Ordinal);

    [Fact]
    public void A_decision_whose_two_exits_chose_one_point_or_one_its_entry_opens_whole_the_drawing_choosing_for_them()
    {
        // The copy of a project whose step became a decision after its exits chose their points (ADR 091, AR-24).
        var (_, twice) = Build(Read(Exits("""{ "from": "top" }""", """{ "from": "top", "to": "bottom" }""")));
        var flows = FlowsByLabel(twice);
        Assert.Equal((FlowSide.Top, (FlowSide?)null, FlowSide.Bottom),
            (flows["Conforme ?>Classer"].SourceSide!.Value, flows["Conforme ?>Recevoir"].SourceSide, flows["Conforme ?>Recevoir"].TargetSide!.Value));
        var (_, entry) = Build(Read(Exits("""{ "from": "left" }""", """{ "from": "right" }""")));
        Assert.Equal(((FlowSide?)null, FlowSide.Right),
            (FlowsByLabel(entry)["Conforme ?>Classer"].SourceSide, FlowsByLabel(entry)["Conforme ?>Recevoir"].SourceSide!.Value));
    }

    [Fact]
    public void A_proposal_of_the_assistant_reads_no_route_not_even_a_wrong_one_and_the_map_it_rewrites_is_sent_without_them()
    {
        var wrong = Routed(first: """{ "from": "up", "via": { "lane": "c9", "row": 0, "edge": "top" } }""");
        Assert.NotEmpty(Faults(wrong));
        var read = PmapReader.Read(wrong, new PmapReadSettings(Languages, "ACI", Proposal: true));
        Assert.Empty(read.Errors);
        Assert.All(read.Document!.Map.Flows, flow => Assert.Null(flow.Route));

        // The map the assistant rewrites, as its panel sends it: the tree of the map, without the routes; the copy keeps them.
        var (_, workspace) = Build(Read(Routed()));
        var files = new PmapFiles();
        var open = files.Write(files.OutlineTree(workspace, workspace.All<ProcessMap>().Single(map => map.IsRoot).Id).Document, "Test", At);
        Assert.DoesNotContain("\"route\"", open, StringComparison.Ordinal);
        Assert.Contains("\"route\"", files.Write(files.Outline(workspace, null, null).Document, "Test", At), StringComparison.Ordinal);
    }

    [Fact]
    public void The_published_schema_refuses_a_passage_of_an_aisle_and_a_band_or_of_a_part_of_a_band_as_the_reader_does()
    {
        // The routes are of the 1.3, which the files of this test declare.
        var schema = new SchemaValidator(JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot.Path, "schemas", PublishedSchemas.For("1.3")))).RootElement);
        bool Valid(string via) => schema.Validate(JsonDocument.Parse(Routed(first: $$"""{ "via": {{via}} }""")).RootElement).Count == 0;
        Assert.True(Valid("""{ "aisle": 2 }"""));
        Assert.True(Valid("""{ "lane": "c1", "row": 0, "edge": "top" }"""));
        Assert.True(Valid("""{ "column": 2 }"""));
        Assert.True(Valid("""{ "lane": "c1", "row": 0 }"""));
        Assert.False(Valid("""{ "aisle": 2, "column": 2 }"""));
        Assert.False(Valid("""{ "column": 2, "lane": "c1", "row": 0 }"""));
        Assert.False(Valid("""{ "column": -1 }"""));
        Assert.False(Valid("""{ "edge": "top" }"""));
        Assert.False(Valid("""{ "aisle": 2, "lane": "c1", "row": 0, "edge": "top" }"""));
        Assert.False(Valid("""{ "row": 0, "edge": "top" }"""));
        Assert.False(Valid("""{ "aisle": 2, "edge": "top" }"""));
        Assert.False(Valid("{}"));
    }
}
