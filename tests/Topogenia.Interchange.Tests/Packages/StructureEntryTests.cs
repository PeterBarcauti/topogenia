// StructureEntryTests.cs
// project.json, members.json and maps.json of the package (spec 7.2): every field of the structure, named by its codes.

using System.Text.Json.Nodes;
using Topogenia.Interchange.Packages;
using Topogenia.Interchange.Tests.Support;

namespace Topogenia.Interchange.Tests.Packages;

public sealed class StructureEntryTests
{
    private static JsonNode Entry(string path, bool withoutPeople = false)
    {
        var sample = new SampleProject();
        var entries = PackageWriter.Entries(sample.Workspace(), sample.Logins, new PackageOptions(withoutPeople));
        return JsonNode.Parse(entries.Single(entry => entry.Path == path).Content)!;
    }

    private static JsonObject Map(string? parentStep) =>
        Entry("maps.json")["maps"]!.AsArray().Single(map => (string?)map!["parentStep"] == parentStep)!.AsObject();

    private static JsonObject StepOf(JsonObject map, string code) =>
        map["steps"]!.AsArray().Single(step => (string?)step!["code"] == code)!.AsObject();

    [Fact]
    public void The_project_carries_its_fields_its_classification_and_every_counter()
    {
        var project = Entry("project.json")["project"]!;

        Assert.Equal("Demande de congés", (string?)project["name"]);
        Assert.Equal("fr", (string?)project["sourceLanguage"]);
        Assert.Equal("Du besoin du salarié à l'enregistrement en paie.", (string?)project["description"]);
        Assert.Equal("\\\\partage-fictif\\conges", (string?)project["sharedFolder"]);
        Assert.Equal("confidential", (string?)project["classification"]);
        Assert.Equal("Équipe RH", (string?)project["sharingGroupLabel"]);
        Assert.Equal("congé payé = paid leave\nsolde de congés = leave balance", (string?)project["translationGlossary"]);
        Assert.Equal("""{"lane":7,"pain_point":4,"solution":0,"step":23}""", project["counters"]!.ToJsonString());
        Assert.Equal("2026-09-01T07:30:00.0000000Z", (string?)project["createdAt"]);
        Assert.Equal("EXEMPLE\\alice", (string?)project["createdBy"]);
        Assert.Equal("2026-09-01T08:10:00.0000000Z", (string?)project["updatedAt"]);
        Assert.Equal("EXEMPLE\\bruno", (string?)project["updatedBy"]);
    }

    [Fact]
    public void The_project_carries_no_technical_field_of_the_database()
    {
        var project = Entry("project.json")["project"]!.AsObject();

        Assert.Equal(["classification", "code", "counters", "createdAt", "createdBy", "description", "fteHoursPerMonth", "name", "sharedFolder",
            "sharingGroupLabel", "sourceLanguage", "translationGlossary", "updatedAt", "updatedBy", "workdayHours"], project.Select(property => property.Key));
    }

    [Fact]
    public void The_members_carry_their_login_name_role_and_flags_by_display_order()
    {
        var members = Entry("members.json")["members"]!.AsArray();

        Assert.Equal(["EXEMPLE\\alice", "EXEMPLE\\bruno", "EXEMPLE\\chloe"], members.Select(member => (string?)member!["login"]));
        Assert.Equal(["facilitator", "contributor", "reader"], members.Select(member => (string?)member!["role"]));
        Assert.Equal(["Alice Martin", "Bruno Lefort", "Chloé Duval"], members.Select(member => (string?)member!["displayName"]));
        Assert.Equal([true, false, false], members.Select(member => (bool)member!["isSponsor"]!));
        Assert.Equal([false, true, false], members.Select(member => (bool)member!["isKeyContributor"]!));
        Assert.Equal([0, 1, 2], members.Select(member => (int)member!["displayOrder"]!));
        Assert.Equal("EXEMPLE\\alice", (string?)members[1]!["updatedBy"]);
    }

    [Fact]
    public void The_maps_are_the_root_map_then_the_live_sub_maps_by_their_parent_step_the_empty_one_included()
    {
        var maps = Entry("maps.json")["maps"]!.AsArray();

        Assert.Equal([null, "CONGES-E002", "CONGES-E004"], maps.Select(map => (string?)map!["parentStep"]));
        Assert.Equal(["Demande de congés", "Saisir la demande", "Valider la demande"], maps.Select(map => (string?)map!["name"]));
        Assert.Empty(maps[2]!["steps"]!.AsArray());
        Assert.False(maps[0]!.AsObject().ContainsKey("parentStep"));
    }

    [Fact]
    public void The_lanes_carry_their_code_type_actor_colour_order_and_source_lane()
    {
        var root = Map(null);
        var lanes = root["lanes"]!.AsArray();

        Assert.Equal(["CONGES-C01", "CONGES-C02", "CONGES-C03", "CONGES-C04", "CONGES-C06"], lanes.Select(lane => (string?)lane!["code"]));
        Assert.Equal(["person", "person", "department", "system", "department"], lanes.Select(lane => (string?)lane!["laneType"]));
        Assert.Equal([0, 1, 2, 3, 4], lanes.Select(lane => (int)lane!["sortOrder"]!));
        Assert.Equal("EXEMPLE\\chloe", (string?)lanes[0]!["actor"]);
        Assert.Matches("^#[0-9A-F]{6}$", (string?)lanes[0]!["color"]);
        var detailLane = Map("CONGES-E002")["lanes"]!.AsArray().Single()!;
        Assert.Equal("CONGES-C01", (string?)detailLane["sourceLane"]);
        Assert.Equal("CONGES-C05", (string?)detailLane["code"]);
    }

    [Fact]
    public void The_steps_are_in_reading_order_column_then_lane_then_row_with_their_lane_cell_type_automation_and_assignee()
    {
        var root = Map(null);
        var steps = root["steps"]!.AsArray();

        Assert.Equal(["CONGES-E001", "CONGES-E002", "CONGES-E003", "CONGES-E008", "CONGES-E004", "CONGES-E013", "CONGES-E014", "CONGES-E006", "CONGES-E007"],
            steps.Select(step => (string?)step!["code"]));
        var correct = StepOf(root, "CONGES-E008");
        Assert.Equal("CONGES-C01", (string?)correct["lane"]);
        Assert.Equal(3, (int)correct["column"]!);
        Assert.Equal(1, (int)correct["row"]!);
        Assert.Equal("decision", (string?)StepOf(root, "CONGES-E003")["stepType"]);
        Assert.Equal("automated", (string?)StepOf(root, "CONGES-E006")["automationStatus"]);
        Assert.Equal("EXEMPLE\\bruno", (string?)StepOf(root, "CONGES-E004")["assignee"]);
        Assert.Equal("Valider la demande", (string?)StepOf(root, "CONGES-E004")["label"]);
    }

    [Fact]
    public void The_flows_are_named_by_their_ends_in_reading_order_with_their_labels()
    {
        var flows = Map(null)["flows"]!.AsArray();

        Assert.Equal(["E001>E002", "E002>E003", "E003>E008", "E003>E004", "E008>E002", "E004>E013", "E013>E014", "E014>E006", "E006>E007"],
            flows.Select(flow => ((string)flow!["source"]!)[7..] + ">" + ((string)flow["target"]!)[7..]));
        Assert.Equal(["non", "oui"], flows.Where(flow => flow!["label"] is not null).Select(flow => (string?)flow!["label"]));
    }

    [Fact]
    public void The_raci_roles_of_a_step_are_all_exported_the_hidden_s_included_by_lane_code()
    {
        var raci = StepOf(Map(null), "CONGES-E004")["raci"]!.AsArray();

        Assert.Equal(["CONGES-C01:I", "CONGES-C03:C", "CONGES-C04:S"], raci.Select(role => (string?)role!["lane"] + ":" + (string?)role["role"]));
        Assert.Equal("EXEMPLE\\alice", (string?)raci[0]!["updatedBy"]);
        Assert.Equal("EXEMPLE\\denis", (string?)raci[2]!["createdBy"]);
        Assert.Equal("CONGES-C03:A", StepOf(Map(null), "CONGES-E006")["raci"]!.AsArray().Select(role => (string?)role!["lane"] + ":" + (string?)role["role"]).Single());
    }

    [Fact]
    public void An_open_frame_carries_its_dissolved_step_its_live_steps_and_the_flows_and_lanes_it_created()
    {
        var frame = Map(null)["dissolution"]!.AsObject();

        Assert.Equal("CONGES-E012", (string?)frame["step"]!["code"]);
        Assert.Equal("Contrôler les soldes", (string?)frame["step"]!["label"]);
        Assert.Equal(["automationStatus", "code", "createdAt", "createdBy", "label", "sourceLanguage", "stepType", "updatedAt", "updatedBy"],
            frame["step"]!.AsObject().Select(property => property.Key));
        Assert.Equal(["CONGES-E013", "CONGES-E014"], frame["frameSteps"]!.AsArray().Select(code => (string?)code));
        Assert.Equal(["E004>E013", "E013>E014", "E014>E006"],
            frame["createdFlows"]!.AsArray().Select(flow => ((string)flow!["source"]!)[7..] + ">" + ((string)flow["target"]!)[7..]));
        Assert.Equal(["CONGES-C06"], frame["createdLanes"]!.AsArray().Select(code => (string?)code));
        Assert.Equal("2026-09-01T08:31:00.0000000Z", (string?)frame["updatedAt"]);
    }

    [Fact]
    public void A_closed_frame_is_not_exported()
    {
        Assert.False(Map("CONGES-E002").ContainsKey("dissolution"));
        Assert.DoesNotContain("CONGES-E016", Entry("maps.json").ToJsonString(), StringComparison.Ordinal);
    }
}
