// PmapValueStreamTests.cs
// The entries of the value stream in a .pmap 1.2: read, built by the rules of the domain, written back the same; unknown in a file 1.1 (ADR 085, spec 14.7).

using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Interchange.Pmap;
using static Topogenia.Interchange.Tests.Pmap.PmapBuildTests;
using static Topogenia.Interchange.Tests.Pmap.PmapReaderTests;

namespace Topogenia.Interchange.Tests.Pmap;

public sealed class PmapValueStreamTests
{
    private static readonly DateTime At = new(2026, 10, 31, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>A file 1.2 of a decision that sends a share back, with every entry of the value stream.</summary>
    private const string Claims = """
        {
          "format": "topogenia",
          "version": "1.2",
          "project": { "name": "Notes de frais", "code": "NDF", "workday_hours": 7.5, "fte_hours_per_month": 160 },
          "map": {
            "lanes": [{ "id": "c1", "name": "Comptabilité", "type": "department", "headcount": 3 }, { "id": "c2", "name": "ERP", "type": "system" }],
            "steps": [
              { "id": "e1", "lane": "c1", "label": "Note reçue", "type": "start", "volume": { "quantity": 120, "period": "month" } },
              { "id": "e2", "lane": "c1", "label": "Contrôler", "sheet": { "frequency": "per_case", "duration_minutes": 10, "lead_time": { "value": 2, "unit": "day" } } },
              { "id": "e3", "lane": "c1", "label": "Conforme ?", "type": "decision" },
              { "id": "e4", "lane": "c2", "label": "Payer", "automation": "automated" }
            ],
            "flows": [
              { "source": "e1", "target": "e2" }, { "source": "e2", "target": "e3" },
              { "source": "e3", "target": "e2", "label": "non", "share": 15 }, { "source": "e3", "target": "e4", "label": "oui" }
            ]
          }
        }
        """;

    [Fact]
    public void A_file_1_2_carries_every_entry_into_the_project_built_by_the_rules_of_the_domain()
    {
        var (built, workspace) = Build(Read(Claims));

        Assert.Equal((7.5m, 160m), (built.Project.WorkdayHours, built.Project.FteHoursPerMonth));
        var lanes = workspace.LanesOf(workspace.RootMap.Id);
        Assert.Equal([3, null], lanes.Select(lane => lane.Headcount));
        var steps = workspace.ReadingOrder(workspace.RootMap.Id);
        Assert.Equal((120m, (VolumePeriod?)VolumePeriod.Month), (steps[0].VolumeQuantity, steps[0].VolumePeriod));
        var back = workspace.FlowsOf(workspace.RootMap.Id).Single(flow => flow.Label == "non");
        Assert.Equal(15m, back.SharePercent);
        var sheet = workspace.All<StepSheet>().Single();
        Assert.Equal((Frequency.PerCase, 2m, (LeadTimeUnit?)LeadTimeUnit.Day), (sheet.Frequency!.Value, sheet.LeadTime!.Value, sheet.LeadTimeUnit));
        Assert.Empty(Domain.Workspace.StructureInvariants.Violations(workspace));
    }

    [Fact]
    public void A_copy_writes_the_version_1_2_and_every_entry_back_the_same()
    {
        var document = Read(Claims);
        var text = PmapWriter.Write(document, "Test", At);

        Assert.Contains("\"version\": \"1.2\"", text, StringComparison.Ordinal);
        Assert.Equal(text, PmapWriter.Write(Read(text), "Test", At));
        Assert.Contains("\"volume\": {\n          \"quantity\": 120,\n          \"period\": \"month\"\n        }", text, StringComparison.Ordinal);
        Assert.Contains("\"share\": 15", text, StringComparison.Ordinal);
        Assert.Contains("\"headcount\": 3", text, StringComparison.Ordinal);
        Assert.Contains("\"lead_time\": {\n            \"value\": 2,\n            \"unit\": \"day\"\n          }", text, StringComparison.Ordinal);
        Assert.Contains("\"workday_hours\": 7.5", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_project_outlined_from_its_workspace_gives_its_entries_to_the_file()
    {
        var (_, workspace) = Build(Read(Claims));
        var outlined = PmapOutline.Of(workspace, new PmapOutlineOptions(true, _ => new Domain.Sheets.SheetViewer(true, true))).Document;

        Assert.Equal((7.5m, 160m), (outlined.Project.WorkdayHours!.Value, outlined.Project.FteHoursPerMonth!.Value));
        Assert.Equal(new PmapVolume(120m, "month"), outlined.Map.Steps[0].Volume);
        Assert.Equal(15m, outlined.Map.Flows.Single(flow => flow.Label == "non").Share);
        Assert.Equal(3, outlined.Map.Lanes[0].Headcount);
        Assert.Equal(new PmapLeadTime(2m, "day"), outlined.Map.Steps[1].Sheet!.LeadTime);
    }

    [Theory]
    [InlineData("\"headcount\": 3", "map.lanes[0].headcount")]
    [InlineData("\"volume\": { \"quantity\": 1, \"period\": \"day\" }", "map.steps[0].volume")]
    [InlineData("\"share\": 10", "map.flows[0].share")]
    public void A_property_of_the_version_1_2_is_unknown_in_a_file_1_1(string property, string path)
    {
        var map = """{ "lanes": [{ "id": "c1", "name": "A", "type": "department", PROPERTY_LANE }], "steps": [{ "id": "e1", "lane": "c1", "label": "Début", "type": "start" PROPERTY_STEP }, { "id": "e2", "lane": "c1", "label": "B" }], "flows": [{ "source": "e1", "target": "e2" PROPERTY_FLOW }] }""";
        var lane = path.StartsWith("map.lanes", StringComparison.Ordinal) ? property : "\"color\": \"#FFFFFF\"";
        var step = path.StartsWith("map.steps", StringComparison.Ordinal) ? ", " + property : "";
        var flow = path.StartsWith("map.flows", StringComparison.Ordinal) ? ", " + property : "";
        var text = Doc(map.Replace("PROPERTY_LANE", lane, StringComparison.Ordinal).Replace(" PROPERTY_STEP", step, StringComparison.Ordinal)
            .Replace(" PROPERTY_FLOW", flow, StringComparison.Ordinal));
        var fault = Assert.Single(Faults(text));
        Assert.Equal((path, FileErrorCodes.UnknownProperty), (fault.Path, fault.Code));
        Read(text.Replace("\"version\": \"1.1\"", "\"version\": \"1.2\"", StringComparison.Ordinal));
    }

    [Fact]
    public void The_frequency_per_case_is_refused_in_a_file_1_1_and_read_in_a_file_1_2()
    {
        var map = """{ "lanes": [{ "id": "c1", "name": "A" }], "steps": [{ "id": "e1", "lane": "c1", "label": "B", "sheet": { "frequency": "per_case" } }] }""";
        var fault = Assert.Single(Faults(Doc(map)));
        Assert.Equal(("map.steps[0].sheet.frequency", FileErrorCodes.InvalidValue), (fault.Path, fault.Code));
        Assert.Equal("per_case", Read(Doc(map).Replace("\"1.1\"", "\"1.2\"", StringComparison.Ordinal)).Map.Steps[0].Sheet!.Frequency);
    }

    [Theory]
    [InlineData("\"type\": \"department\", \"headcount\": 3", "\"type\": \"system\", \"headcount\": 3", "map.lanes[0].headcount", "headcount")]
    [InlineData("\"label\": \"Note reçue\", \"type\": \"start\"", "\"label\": \"Note reçue\", \"type\": \"task\"", "map.steps[0].volume", "volume")]
    [InlineData("{ \"source\": \"e1\", \"target\": \"e2\" }", "{ \"source\": \"e1\", \"target\": \"e2\", \"share\": 50 }", "map.flows[0]", "share")]
    [InlineData("\"workday_hours\": 7.5", "\"workday_hours\": 25", "project", "workday_hours")]
    public void An_entry_out_of_its_place_or_its_bounds_is_a_fault_of_the_file_at_its_path(string valid, string invalid, string path, string field)
    {
        var refusal = Assert.Throws<DomainException>(() => Build(Read(Claims.Replace(valid, invalid, StringComparison.Ordinal))));
        var fault = Assert.Single((IEnumerable<FileError>)refusal.Details[FileError.DetailsKey]!);
        Assert.Equal((path, ErrorCodes.InvalidValue, (object?)field), (fault.Path, fault.Code, fault.Values["field"]));
    }
}
