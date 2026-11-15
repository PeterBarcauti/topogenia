// PackageRouteTests.cs
// The route a user chose for a flow in the exchange package: written in the format 2.2 and read back as it was, the middle of a column and of a row too, a project without one still written in 2.1, a route refused by the schema 2.1, an unknown lane, a band on another map, a partial passage (ADR 091).

using System.Text.Json.Nodes;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Interchange.Packages;
using Topogenia.Interchange.Tests.Support;

namespace Topogenia.Interchange.Tests.Packages;

public sealed class PackageRouteTests
{
    private static int next;

    private static OperationContext Context() =>
        new(Guid.Parse("00000000-0000-0000-0000-0000000000f2"), SampleProject.At(1000), "fr", AppSettings.Default,
            () => new Guid($"30000000-0000-0000-0000-{Interlocked.Increment(ref next):x12}"));

    /// <summary>The sample with a band and a departure on the flow E001 to E002, an aisle and an arrival on the flow E002 to E003.</summary>
    private static SampleProject Routed()
    {
        var sample = new SampleProject().Before23();
        var first = FlowOf(sample, "E001", "E002");
        var second = FlowOf(sample, "E002", "E003");
        return sample.With(record => record switch
        {
            Flow flow when flow.Id == first => flow with { SourceSide = FlowSide.Top, ViaLaneId = sample.Lanes["C02"], ViaRow = 0, ViaEdge = FlowSide.Bottom },
            Flow flow when flow.Id == second => flow with { TargetSide = FlowSide.Left, ViaAisle = -1 },
            _ => record,
        });
    }

    private static Guid FlowOf(SampleProject sample, string source, string target) =>
        sample.Records.OfType<Flow>().Single(flow => flow.DeletedAt is null && flow.SourceStepId == sample.Steps[source] && flow.TargetStepId == sample.Steps[target]).Id;

    private static byte[] Written(SampleProject sample) =>
        PackageWriter.Write(sample.Workspace(), sample.Logins, new PackageOptions(false), new PackageStamp(SampleProject.At(500), "EXEMPLE\\alice"));

    /// <summary>The flows of the root map of a package rebuilt, by the codes of their steps.</summary>
    private static (ProjectWorkspace Workspace, Dictionary<string, Flow> Flows) Rebuilt(byte[] package)
    {
        var importer = new PackageImporter();
        var read = importer.Read(package);
        var records = importer.Build(read, new ImportPlan(Context(), "ROUTE", _ => null, false));
        var workspace = new ProjectWorkspace(((Project)records[0]).Id, records);
        var codes = workspace.All<Step>().ToDictionary(step => step.Id, step => step.Code[(step.Code.IndexOf('-', StringComparison.Ordinal) + 1)..]);
        return (workspace, workspace.All<Flow>().Where(flow => flow.DeletedAt is null)
            .ToDictionary(flow => $"{codes[flow.SourceStepId]}>{codes[flow.TargetStepId]}", StringComparer.Ordinal));
    }

    private static void AssertInvalid(Action read, string reason, string entry)
    {
        var refusal = Assert.Throws<DomainException>(read);
        Assert.Equal((ErrorCodes.InvalidPackage, (object?)reason, (object?)entry), (refusal.Code, refusal.Details["reason"], refusal.Details["entry"]));
    }

    [Fact]
    public void A_project_without_a_route_is_written_in_the_format_2_1_which_the_version_3_0_reads()
    {
        var manifest = new PackageFiles(Written(new SampleProject().Before23())).Json(PackageFormat.Manifest);
        Assert.Equal(("2.1", "2.1"), (manifest["formatVersion"]!.GetValue<string>(), manifest["schemaVersion"]!.GetValue<string>()));
    }

    [Fact]
    public void A_summary_is_written_in_the_format_2_3_with_or_without_a_route()
    {
        foreach (var sample in new[] { new SampleProject(), Routed().Add(new SampleProject().Records.OfType<MapSummary>().First() with { MapId = Routed().RootMap }) })
        {
            var manifest = new PackageFiles(Written(sample)).Json(PackageFormat.Manifest);
            Assert.Equal(("2.3", "2.3"), (manifest["formatVersion"]!.GetValue<string>(), manifest["schemaVersion"]!.GetValue<string>()));
        }
    }

    [Fact]
    public void A_route_is_written_in_the_format_2_2_the_lane_of_a_band_by_its_code_and_read_back_as_it_was()
    {
        var package = Written(Routed());
        var files = new PackageFiles(package);
        Assert.Equal(("2.2", "2.2"), (files.Json(PackageFormat.Manifest)["formatVersion"]!.GetValue<string>(), files.Json(PackageFormat.Manifest)["schemaVersion"]!.GetValue<string>()));
        var routes = files.Json(PackageFormat.Maps).ToJsonString();
        Assert.Contains("\"route\":{\"from\":\"top\",\"via\":{\"edge\":\"bottom\",\"lane\":\"CONGES-C02\",\"row\":0}}", routes, StringComparison.Ordinal);
        Assert.Contains("\"route\":{\"to\":\"left\",\"via\":{\"aisle\":-1}}", routes, StringComparison.Ordinal);

        var (workspace, flows) = Rebuilt(package);
        var band = flows["E001>E002"];
        var lane = workspace.All<Lane>().Single(item => item.Id == band.ViaLaneId);
        Assert.Equal((FlowSide.Top, (FlowSide?)null, "ROUTE-C02", 0, FlowSide.Bottom), (band.SourceSide!.Value, band.TargetSide, lane.Code, band.ViaRow!.Value, band.ViaEdge!.Value));
        var aisle = flows["E002>E003"];
        Assert.Equal(((FlowSide?)null, FlowSide.Left, -1, false), (aisle.SourceSide, aisle.TargetSide!.Value, aisle.ViaAisle!.Value, aisle.ViaLaneId.HasValue));
        Assert.Empty(ImportedProjects.Violations(workspace));
        Assert.All(flows.Where(pair => pair.Key is not "E001>E002" and not "E002>E003").Select(pair => pair.Value),
            flow => Assert.Equal(((FlowSide?)null, (FlowSide?)null, false), (flow.SourceSide, flow.TargetSide, flow.HasPassage)));
    }

    [Fact]
    public void The_middle_of_a_column_and_the_middle_of_a_row_are_written_and_read_back_as_they_were()
    {
        var sample = new SampleProject();
        var (first, second) = (FlowOf(sample, "E001", "E002"), FlowOf(sample, "E002", "E003"));
        var package = Written(sample.With(record => record switch
        {
            Flow flow when flow.Id == first => flow with { ViaColumn = 1 },
            Flow flow when flow.Id == second => flow with { ViaLaneId = sample.Lanes["C02"], ViaRow = 0 },
            _ => record,
        }));
        var routes = new PackageFiles(package).Json(PackageFormat.Maps).ToJsonString();
        Assert.Contains("\"route\":{\"via\":{\"column\":1}}", routes, StringComparison.Ordinal);
        Assert.Contains("\"route\":{\"via\":{\"lane\":\"CONGES-C02\",\"row\":0}}", routes, StringComparison.Ordinal);
        var (workspace, flows) = Rebuilt(package);
        Assert.Equal((1, (int?)null, (Guid?)null), (flows["E001>E002"].ViaColumn!.Value, flows["E001>E002"].ViaAisle, flows["E001>E002"].ViaLaneId));
        var row = flows["E002>E003"];
        Assert.Equal(("ROUTE-C02", 0, (FlowSide?)null), (workspace.All<Lane>().Single(lane => lane.Id == row.ViaLaneId).Code, row.ViaRow!.Value, row.ViaEdge));
        Assert.Empty(ImportedProjects.Violations(workspace));
    }

    [Fact]
    public void A_route_in_a_package_that_says_it_is_of_the_format_2_1_is_refused_by_its_own_schema()
    {
        var forge = new PackageForge(sample: Routed()).Manifest(manifest =>
        {
            manifest["formatVersion"] = "2.1";
            manifest["schemaVersion"] = "2.1";
        });
        AssertInvalid(() => new PackageImporter().Read(forge.Bytes()), "schema", PackageFormat.Maps);
    }

    [Fact]
    public void A_band_on_a_lane_unknown_to_the_package_is_an_unresolved_reference()
    {
        var forge = new PackageForge(sample: Routed()).With(PackageFormat.Maps, node =>
            node["maps"]!.AsArray().SelectMany(map => map!["flows"]!.AsArray()).Select(flow => flow!["route"]?["via"])
                .Single(via => via?["lane"] is not null)!["lane"] = "CONGES-C99");
        var importer = new PackageImporter();
        AssertInvalid(() => importer.Build(importer.Read(forge.Bytes()), new ImportPlan(Context(), "ROUTE", _ => null, false)), "reference", PackageFormat.Maps);
    }

    [Fact]
    public void A_band_on_a_lane_of_another_map_breaks_the_structure_of_the_imported_project()
    {
        var sample = Routed();
        var detailLane = sample.Records.OfType<Lane>().First(lane => lane.MapId == sample.DetailMap && lane.DeletedAt is null);
        var (workspace, flows) = Rebuilt(Written(sample.With(record => record is Flow flow && flow.ViaLaneId is not null ? flow with { ViaLaneId = detailLane.Id } : record)));
        Assert.Contains($"route:{flows["E001>E002"].Id}", ImportedProjects.Violations(workspace));
    }

    [Fact]
    public void The_schema_2_2_refuses_an_empty_route_both_passages_and_a_partial_band()
    {
        var routes = new JsonNode[]
        {
            new JsonObject(),
            new JsonObject { ["via"] = new JsonObject { ["aisle"] = 1, ["lane"] = "CONGES-C02", ["row"] = 0, ["edge"] = "top" } },
            new JsonObject { ["via"] = new JsonObject { ["aisle"] = 1, ["column"] = 2 } },
            new JsonObject { ["via"] = new JsonObject { ["column"] = 2, ["lane"] = "CONGES-C02", ["row"] = 0 } },
            new JsonObject { ["via"] = new JsonObject { ["column"] = -1 } },
            new JsonObject { ["via"] = new JsonObject { ["lane"] = "CONGES-C02", ["edge"] = "top" } },
            new JsonObject { ["via"] = new JsonObject { ["row"] = 0, ["edge"] = "top" } },
            new JsonObject { ["via"] = new JsonObject { ["edge"] = "top" } },
            new JsonObject { ["via"] = new JsonObject() },
            new JsonObject { ["from"] = "middle" },
        };
        foreach (var route in routes)
        {
            var forge = new PackageForge(sample: Routed());
            forge.With(PackageFormat.Maps, node =>
            {
                var flow = node["maps"]!.AsArray().SelectMany(map => map!["flows"]!.AsArray()).First(item => item!["route"] is not null)!;
                flow["route"] = route.DeepClone();
            });
            AssertInvalid(() => new PackageImporter().Read(forge.Bytes()), "schema", PackageFormat.Maps);
        }
    }
}
