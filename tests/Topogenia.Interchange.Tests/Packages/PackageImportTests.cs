// PackageImportTests.cs
// The records rebuilt from a package (spec 7.3, 7.4, ADR 052): exact round trip, new identifiers, codes rewritten, people, open frame, references.

using System.Text;
using System.Text.Json.Nodes;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Interchange.Packages;
using Topogenia.Interchange.Tests.Support;

namespace Topogenia.Interchange.Tests.Packages;

public sealed class PackageImportTests
{
    private static readonly Guid Importer = Guid.Parse("00000000-0000-0000-0000-0000000000f1");

    private static int next;

    private static OperationContext Context(Guid? actor = null) =>
        new(actor ?? Importer, SampleProject.At(1000), "fr", AppSettings.Default, () => new Guid($"10000000-0000-0000-0000-{Interlocked.Increment(ref next):x12}"));

    private static IReadOnlyList<IRecord> Build(PackageForge forge, string? code = null, bool withMembers = true, Guid? actor = null)
    {
        var importer = new PackageImporter();
        var read = importer.Read(forge.Bytes());
        var users = forge.Sample.Records.OfType<AppUser>().ToDictionary(user => user.LoginNormalized, user => user.Id);
        return importer.Build(read, new ImportPlan(Context(actor), code ?? read.ProjectCode,
            login => users.TryGetValue(Login.Normalize(login), out var id) ? id : null, withMembers));
    }

    private static ProjectWorkspace Workspace(IReadOnlyList<IRecord> records, SampleProject sample) =>
        new(((Project)records[0]).Id, [.. records, .. sample.Records.OfType<AppUser>()]);

    private static string Entries(ProjectWorkspace workspace, IReadOnlyDictionary<Guid, string> logins, bool withoutPeople) =>
        string.Join("\n---\n", PackageWriter.Entries(workspace, logins, new PackageOptions(withoutPeople))
            .Select(entry => entry.Path + "\n" + Encoding.UTF8.GetString(entry.Content)));

    private static DomainException Refused(PackageForge forge) => Assert.Throws<DomainException>(() => Build(forge));

    [Fact]
    public void Export_of_import_of_export_gives_the_same_entries_byte_for_byte()
    {
        var forge = new PackageForge();
        var sample = forge.Sample;

        var rebuilt = Workspace(Build(forge, actor: sample.Alice), sample);

        Assert.Equal(Entries(sample.Workspace(), sample.Logins, withoutPeople: false), Entries(rebuilt, sample.Logins, withoutPeople: false));
    }

    [Fact]
    public void Without_the_people_the_round_trip_is_exact_too()
    {
        var forge = new PackageForge(withoutPeople: true);
        var sample = forge.Sample;

        var rebuilt = Workspace(Build(forge), sample);

        Assert.Equal(Entries(sample.Workspace(), sample.Logins, withoutPeople: true), Entries(rebuilt, new Dictionary<Guid, string>(), withoutPeople: true));
    }

    [Fact]
    public void Every_record_takes_a_new_identifier_and_the_project_comes_first()
    {
        var forge = new PackageForge();
        var old = forge.Sample.Records.SelectMany(Identifiers).ToHashSet();

        var records = Build(forge);

        var project = Assert.IsType<Project>(records[0]);
        Assert.Equal(("CONGES", 0, 1, (DateTime?)null), (project.Code, project.StructureVersion, project.Version, project.DeletedAt));
        Assert.Empty(records.SelectMany(Identifiers).Where(old.Contains).Except(forge.Sample.Records.OfType<AppUser>().Select(user => user.Id)));
        Assert.All(records.OfType<ProcessMap>(), map => Assert.Equal(project.Id, map.ProjectId));
    }

    private static IEnumerable<Guid> Identifiers(IRecord record) => record switch
    {
        Project project => [project.Id],
        ProcessMap map => [map.Id],
        Lane lane => [lane.Id],
        Step step => [step.Id],
        Flow flow => [flow.Id],
        StepSheet sheet => [sheet.Id],
        StepSheetVersion version => [version.Id],
        Document document => [document.Id],
        PainPoint painPoint => [painPoint.Id],
        PainPointStatusChange change => [change.Id],
        Dissolution dissolution => [dissolution.Id],
        DeletionBatch batch => [batch.Id],
        _ => [],
    };

    [Fact]
    public void Under_another_code_every_readable_code_is_rewritten_with_its_number()
    {
        var records = Build(new PackageForge(), code: "CONGE");

        var codes = records.OfType<Lane>().Select(lane => lane.Code).Concat(records.OfType<Step>().Select(step => step.Code))
            .Concat(records.OfType<PainPoint>().Select(painPoint => painPoint.Code)).ToList();
        Assert.Equal("CONGE", ((Project)records[0]).Code);
        Assert.All(codes, code => Assert.StartsWith("CONGE-", code, StringComparison.Ordinal));
        Assert.Contains("CONGE-E014", codes);
        Assert.Contains("CONGE-C06", codes);
        Assert.Contains("CONGE-I003", codes);
        Assert.Equal(23, records.OfType<ProjectCounter>().Single(counter => counter.Counter == CounterKind.Step).LastValue);
    }

    [Fact]
    public void Without_the_people_every_author_of_a_creation_is_the_importer_and_no_updater_is_named()
    {
        var records = Build(new PackageForge(withoutPeople: true));

        Assert.All(records.OfType<ICreationAudited>(), record => Assert.Equal(Importer, record.CreatedBy));
        Assert.All(records.OfType<IVersionedRecord>(), record => Assert.Null(record.UpdatedBy));
        Assert.All(records.OfType<StepSheetVersion>(), version => Assert.Equal(Importer, version.ValidatedBy));
        Assert.All(records.OfType<PainPointStatusChange>(), change => Assert.Equal(Importer, change.ChangedBy));
        Assert.Empty(records.OfType<ProjectMember>());
        Assert.All(records.OfType<Lane>(), lane => Assert.Null(lane.ActorUserId));
        Assert.All(records.OfType<Step>(), step => Assert.Null(step.AssigneeUserId));
    }

    [Fact]
    public void With_the_people_the_authors_are_found_by_login_and_the_members_are_taken_on_option()
    {
        var forge = new PackageForge();
        var sample = forge.Sample;

        var records = Build(forge);
        var without = Build(new PackageForge(), withMembers: false);

        Assert.Equal([sample.Alice, sample.Bruno, sample.Chloe], records.OfType<ProjectMember>().OrderBy(member => member.DisplayOrder).Select(member => member.UserId));
        Assert.Equal(MemberRole.Contributor, records.OfType<ProjectMember>().Single(member => member.UserId == sample.Bruno).Role);
        Assert.Equal(sample.Denis, records.OfType<PainPoint>().Single(painPoint => painPoint.Code == "CONGES-I003").CreatedBy);
        Assert.Equal(sample.Chloe, records.OfType<Lane>().Single(lane => lane.Code == "CONGES-C01").ActorUserId);
        Assert.Empty(without.OfType<ProjectMember>());
        Assert.Equal(sample.Chloe, without.OfType<Lane>().Single(lane => lane.Code == "CONGES-C01").ActorUserId);
    }

    [Fact]
    public void The_dissolved_step_of_an_open_frame_comes_back_deleted_in_a_new_batch_and_the_frame_takes_the_cells_of_now()
    {
        var records = Build(new PackageForge());

        var dissolution = Assert.Single(records.OfType<Dissolution>());
        var dissolved = records.OfType<Step>().Single(step => step.Id == dissolution.StepId);
        var batch = Assert.Single(records.OfType<DeletionBatch>());
        Assert.Equal(("CONGES-E012", SampleProject.At(60), (Guid?)batch.Id), (dissolved.Code, dissolved.DeletedAt, dissolved.DeletionBatchId));
        Assert.Equal((batch.Id, (DateTime?)null, SampleProject.At(60)), (dissolution.DeletionBatchId, dissolution.ClosedAt, batch.DeletedAt));
        var firstLane = records.OfType<Lane>().Where(lane => lane.MapId == dissolution.MapId).MinBy(lane => lane.SortOrder)!;
        Assert.Equal((firstLane.Id, 0, 0), (dissolved.LaneId, dissolved.Column, dissolved.Row));
        var frame = records.OfType<DissolutionStep>().ToList();
        Assert.Equal(2, frame.Count);
        Assert.All(frame, item =>
        {
            var step = records.OfType<Step>().Single(candidate => candidate.Id == item.StepId);
            Assert.Equal((step.LaneId, step.Column, step.Row), (item.OriginLaneId, item.OriginColumn, item.OriginRow));
        });
        Assert.Equal(3, records.OfType<DissolutionObject>().Count(item => item.ObjectType == Domain.Dissolutions.DissolutionObjectTypes.Flow));
        Assert.Single(records.OfType<DissolutionObject>(), item => item.ObjectType == Domain.Dissolutions.DissolutionObjectTypes.Lane);
    }

    private static void AssertUnresolved(DomainException refusal, string entry)
    {
        Assert.Equal((ErrorCodes.InvalidPackage, (object?)"reference", (object?)entry), (refusal.Code, refusal.Details["reason"], refusal.Details["entry"]));
    }

    private static JsonObject Map(JsonNode maps, int index) => maps["maps"]![index]!.AsObject();

    [Fact]
    public void A_sheet_of_a_step_the_package_does_not_carry_is_refused() =>
        AssertUnresolved(Refused(new PackageForge().With("sheets.json", node => node["sheets"]![0]!["step"] = "CONGES-E099")), "sheets.json");

    /// <summary>The pain point of the map, named by nothing else: only its prefix is at fault.</summary>
    [Fact]
    public void A_readable_code_under_another_project_code_is_refused() =>
        AssertUnresolved(Refused(new PackageForge().With("painpoints.json", node =>
            node["painPoints"]!.AsArray().Single(painPoint => (string?)painPoint!["code"] == "CONGES-I003")!["code"] = "VENTE-I003")), "painpoints.json");

    [Fact]
    public void A_readable_code_beyond_its_counter_is_refused() =>
        AssertUnresolved(Refused(new PackageForge().With("project.json", node => node["project"]!["counters"]!["lane"] = 5)), "maps.json");

    [Fact]
    public void A_readable_code_carried_twice_is_refused() =>
        AssertUnresolved(Refused(new PackageForge().With("maps.json", node => Map(node, 0)["steps"]![1]!["code"] = "CONGES-E001")), "maps.json");

    [Fact]
    public void A_step_on_a_lane_of_another_map_is_refused() =>
        AssertUnresolved(Refused(new PackageForge().With("maps.json", node => Map(node, 0)["steps"]![1]!["lane"] = "CONGES-C05")), "maps.json");

    [Fact]
    public void A_flow_whose_end_is_on_another_map_is_refused() =>
        AssertUnresolved(Refused(new PackageForge().With("maps.json", node => Map(node, 0)["flows"]![0]!["target"] = "CONGES-E010")), "maps.json");

    [Fact]
    public void The_same_flow_carried_twice_is_refused() =>
        AssertUnresolved(Refused(new PackageForge().With("maps.json", node =>
            Map(node, 0)["flows"]!.AsArray().Add(Map(node, 0)["flows"]![0]!.DeepClone()))), "maps.json");

    /// <summary>
    /// A record whose key the package carries twice: once rebuilt, the second would replace the first in the project
    /// without a word (a use, a translation, a role lost), so each one is refused as a reference named twice.
    /// </summary>
    [Theory]
    [InlineData("sheet", "sheets.json")]
    [InlineData("procedure-position", "sheets.json")]
    [InlineData("version-number", "sheets.json")]
    [InlineData("use", "documents.json")]
    [InlineData("translation", "translations.json")]
    [InlineData("raci-role", "maps.json")]
    [InlineData("frame-step", "maps.json")]
    [InlineData("created-flow", "maps.json")]
    [InlineData("created-lane", "maps.json")]
    public void A_record_carried_twice_is_refused(string record, string entry)
    {
        static void Twice(JsonNode? items) => items!.AsArray().Add(items[0]!.DeepClone());
        static JsonNode Frame(JsonNode maps) => maps["maps"]!.AsArray().First(map => map!["dissolution"] is not null)!["dissolution"]!;
        var forge = new PackageForge().With(entry, node =>
        {
            switch (record)
            {
                case "sheet":
                    Twice(node["sheets"]);
                    break;
                case "procedure-position":
                    Twice(node["sheets"]!.AsArray().First(sheet => sheet!["procedure"]!.AsArray().Count > 0)!["procedure"]);
                    break;
                case "version-number":
                    Twice(node["sheets"]!.AsArray().First(sheet => sheet!["versions"]!.AsArray().Count > 0)!["versions"]);
                    break;
                case "use":
                    Twice(node["documents"]!.AsArray().First(document => document!["uses"]!.AsArray().Count > 0)!["uses"]);
                    break;
                case "translation":
                    Twice(node["translations"]);
                    break;
                case "raci-role":
                    Twice(Map(node, 0)["steps"]!.AsArray().First(step => step!["raci"]!.AsArray().Count > 0)!["raci"]);
                    break;
                case "frame-step":
                    Twice(Frame(node)["frameSteps"]);
                    break;
                case "created-flow":
                    Twice(Frame(node)["createdFlows"]);
                    break;
                default:
                    Twice(Frame(node)["createdLanes"]);
                    break;
            }
        });

        AssertUnresolved(Refused(forge), entry);
    }

    [Fact]
    public void A_use_that_names_an_unknown_pain_point_is_refused() =>
        AssertUnresolved(Refused(new PackageForge().With("documents.json", node =>
            node["documents"]!.AsArray().Single(document => (string?)document!["name"] == "Formulaire de demande")!["uses"]!.AsArray()
                .Single(use => use!["painPoint"] is not null)!["painPoint"] = "CONGES-I004")), "documents.json");

    [Fact]
    public void A_translation_of_an_object_the_package_does_not_carry_is_refused() =>
        AssertUnresolved(Refused(new PackageForge().With("translations.json", node =>
            node["translations"]!.AsArray().First(translation => translation!["step"] is not null)!["step"] = "CONGES-E015")), "translations.json");

    [Fact]
    public void A_project_entry_of_another_code_than_the_manifest_is_refused() =>
        AssertUnresolved(Refused(new PackageForge().With("project.json", node => node["project"]!["code"] = "VENTE")), "project.json");

    [Fact]
    public void A_package_without_its_root_map_is_refused() =>
        AssertUnresolved(Refused(new PackageForge().With("maps.json", node => Map(node, 0)["parentStep"] = "CONGES-E011")), "maps.json");

    [Fact]
    public void A_member_named_twice_case_ignored_is_refused_from_the_reading_on()
    {
        var forge = new PackageForge().With("members.json", node =>
        {
            var twice = node["members"]![1]!.DeepClone();
            twice["login"] = "exemple\\ALICE";
            twice["displayOrder"] = 9;
            node["members"]!.AsArray().Add(twice);
        });

        var refusal = Assert.Throws<DomainException>(() => new PackageImporter().Read(forge.Bytes()));

        AssertUnresolved(refusal, "members.json");
    }
}
