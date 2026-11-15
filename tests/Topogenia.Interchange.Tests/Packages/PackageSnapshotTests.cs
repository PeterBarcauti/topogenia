// PackageSnapshotTests.cs
// The photos of the indicators and the source of a target state in a package (Makigami M6, M8, ADR 095, P3d): the format 2.3 as soon as one is carried, read back exactly as written, in an order that does not depend on the identifiers, checked by the schema, left out of the description given to the assistant.

using System.Text;
using System.Text.Json.Nodes;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Interchange.Json;
using Topogenia.Interchange.Packages;
using Topogenia.Interchange.Tests.Support;

namespace Topogenia.Interchange.Tests.Packages;

public sealed class PackageSnapshotTests
{
    /// <summary>The names of the format 2.3 (docs/format-topogenia.md): the photos and the source of a target state in the object project.</summary>
    private const string Snapshots = "snapshots";

    private const string Source = "baselineProject";

    private static readonly Guid Importer = Guid.Parse("00000000-0000-0000-0000-0000000000f4");

    private static readonly Guid First = Guid.Parse("41000000-0000-0000-0000-000000000001");

    private static readonly Guid Second = Guid.Parse("41000000-0000-0000-0000-000000000002");

    private static long next;

    private static OperationContext Context() =>
        new(Importer, SampleProject.At(1000), "fr", AppSettings.Default, () => new Guid($"32000000-0000-0000-0000-{Interlocked.Increment(ref next):x12}"));

    /// <summary>A photo of the sample whose indicators no decimal of 15 digits keeps exactly (a third, a tenth plus two tenths...).</summary>
    private static StreamSnapshot Photo(SampleProject sample, Guid id, string label, SnapshotOrigin origin, int minute) =>
        new(id, sample.Project, label, origin, SampleProject.At(minute), sample.Alice, null, null, 4)
        {
            LeadHours = 1d / 3,
            HandoffWaitHours = 0.1 + 0.7,
            OtherWaitHours = 1e-7 / 3,
            WorkMinutes = 0.1 + 0.2,
            ValueAddedShare = 2d / 3,
            Coverage = 1,
            Handoffs = 7,
            HandoffsPerCase = 10d / 7,
            MediaBreaks = 2,
            RolledCompleteAccurate = 0.123456789012345678,
            LostHoursPerMonth = 12.5,
            OpenPainPoints = 3,
        };

    /// <summary>The sample (which has its photo « Avant l'atelier ») with another photo, and a reference with unknown indicators, updated by another person.</summary>
    private static SampleProject WithPhotos(SampleProject? sample = null)
    {
        sample ??= new SampleProject();
        return sample.Add(Photo(sample, First, "Atelier du matin", SnapshotOrigin.Photo, 100))
            .Add(Photo(sample, Second, "État actuel", SnapshotOrigin.Baseline, 50) with
            {
                LeadHours = null,
                HandoffWaitHours = null,
                Coverage = null,
                RolledCompleteAccurate = null,
                UpdatedAt = SampleProject.At(200),
                UpdatedBy = sample.Bruno,
            });
    }

    private static byte[] Written(SampleProject sample, string? source = null, bool withoutPeople = false) =>
        PackageWriter.Write(sample.Workspace(), sample.Logins, new PackageOptions(withoutPeople) { BaselineProjectCode = source },
            new PackageStamp(SampleProject.At(500), withoutPeople ? null : "EXEMPLE\\alice"));

    private static string VersionOf(byte[] package) => new PackageFiles(package).Json(PackageFormat.Manifest)["formatVersion"]!.GetValue<string>();

    private static IReadOnlyList<IRecord> Rebuilt(SampleProject sample, byte[] package, bool people = true)
    {
        var importer = new PackageImporter();
        var users = sample.Records.OfType<AppUser>().ToDictionary(user => user.LoginNormalized, user => user.Id);
        return importer.Build(importer.Read(package), new ImportPlan(Context(), "CONGES",
            login => people && users.TryGetValue(Login.Normalize(login), out var id) ? id : null, false));
    }

    /// <summary>A photo without what the import renews: its identifier, its project and its version.</summary>
    private static StreamSnapshot Content(StreamSnapshot snapshot) => snapshot with { Id = Guid.Empty, ProjectId = Guid.Empty, Version = 0 };

    private static void AssertSchemaRefusal(Action<JsonNode> change)
    {
        var forge = new PackageForge(sample: WithPhotos()).With(PackageFormat.Project, change);
        var refusal = Assert.Throws<DomainException>(() => new PackageImporter().Read(forge.Bytes()));
        Assert.Equal((ErrorCodes.InvalidPackage, (object?)"schema", (object?)PackageFormat.Project),
            (refusal.Code, refusal.Details["reason"], refusal.Details["entry"]));
    }

    [Fact]
    public void A_photo_alone_or_a_source_alone_is_written_in_the_format_2_3_and_a_project_without_them_keeps_its_format()
    {
        var photoOnly = new SampleProject().Before23();
        photoOnly.Add(Photo(photoOnly, First, "Atelier", SnapshotOrigin.Photo, 100));

        Assert.Equal("2.3", VersionOf(Written(photoOnly)));
        Assert.Equal("2.3", VersionOf(Written(new SampleProject().Before23(), source: "ORIG")));
        Assert.Equal("2.1", VersionOf(Written(new SampleProject().Before23())));
        var project = new PackageFiles(Written(new SampleProject().Before23())).Json(PackageFormat.Project)["project"]!.AsObject();
        Assert.False(project.ContainsKey(Snapshots) || project.ContainsKey(Source));
    }

    [Fact]
    public void The_photos_are_read_back_exactly_as_written_with_their_authors_and_the_source_is_given_by_its_code_unresolved()
    {
        var sample = WithPhotos();
        var package = Written(sample, source: "ORIG");

        var read = new PackageImporter().Read(package);
        var records = Rebuilt(sample, package);

        Assert.Equal("ORIG", read.BaselineProjectCode);
        var project = (Project)records[0];
        Assert.Null(project.BaselineProjectId);
        var rebuilt = records.OfType<StreamSnapshot>().OrderBy(snapshot => snapshot.CreatedAt).ToList();
        Assert.Equal(sample.Records.OfType<StreamSnapshot>().OrderBy(snapshot => snapshot.CreatedAt).Select(Content), rebuilt.Select(Content));
        Assert.All(rebuilt, snapshot => Assert.Equal((project.Id, 1), (snapshot.ProjectId, snapshot.Version)));
        Assert.DoesNotContain(rebuilt, snapshot => snapshot.Id == First || snapshot.Id == Second);
        Assert.Null(new PackageImporter().Read(Written(new SampleProject())).BaselineProjectCode);
    }

    [Fact]
    public void Two_exports_give_the_same_bytes_whatever_the_order_of_the_records_and_the_identifiers_of_the_photos()
    {
        var sample = WithPhotos();
        // Two photos taken at the same time: their order is their content, not their identifiers.
        sample.Add(Photo(sample, Guid.Parse("41000000-0000-0000-0000-000000000003"), "B", SnapshotOrigin.Photo, 300))
            .Add(Photo(sample, Guid.Parse("41000000-0000-0000-0000-000000000004"), "A", SnapshotOrigin.Photo, 300));
        var swapped = WithPhotos();
        swapped.Add(Photo(swapped, Guid.Parse("41000000-0000-0000-0000-000000000004"), "B", SnapshotOrigin.Photo, 300))
            .Add(Photo(swapped, Guid.Parse("41000000-0000-0000-0000-000000000003"), "A", SnapshotOrigin.Photo, 300));
        string Project(ProjectWorkspace workspace) => Encoding.UTF8.GetString(PackageWriter.Entries(workspace, sample.Logins, new PackageOptions(false))
            .Single(entry => entry.Path == PackageFormat.Project).Content);

        var written = Project(sample.Workspace());
        var labels = JsonNode.Parse(written)!["project"]![Snapshots]!.AsArray().Select(node => node!["label"]!.GetValue<string>());

        Assert.Equal(["État actuel", "Avant l'atelier", "Atelier du matin", "A", "B"], labels);
        Assert.Equal(written, Project(sample.ShuffledWorkspace()));
        Assert.Equal(written, Project(swapped.Workspace()));
        var records = Rebuilt(sample, Written(sample));
        Assert.Equal(written, Project(new ProjectWorkspace(((Project)records[0]).Id, [.. records, .. sample.Records.OfType<AppUser>()])));
    }

    [Fact]
    public void Without_the_people_a_photo_names_no_author_and_takes_the_importer_at_the_import()
    {
        var sample = WithPhotos();
        var package = Written(sample, withoutPeople: true);

        var nodes = new PackageFiles(package).Json(PackageFormat.Project)["project"]![Snapshots]!.AsArray();
        var rebuilt = Rebuilt(sample, package, people: false).OfType<StreamSnapshot>().ToList();

        Assert.All(nodes, node => Assert.False(node!.AsObject().ContainsKey("createdBy") || node.AsObject().ContainsKey("updatedBy")));
        Assert.Equal(3, rebuilt.Count);
        Assert.All(rebuilt, snapshot => Assert.Equal((Importer, (Guid?)null), (snapshot.CreatedBy, snapshot.UpdatedBy)));
        Assert.Contains(rebuilt, snapshot => snapshot.UpdatedAt == SampleProject.At(200));
    }

    [Fact]
    public void The_schema_refuses_a_share_beyond_one_a_negative_count_an_unknown_origin_an_empty_list_a_label_too_long_and_a_source_that_is_no_code()
    {
        static JsonNode PhotoNode(JsonNode root) => root["project"]![Snapshots]![0]!;
        AssertSchemaRefusal(root => PhotoNode(root)["coverage"] = 1.5);
        AssertSchemaRefusal(root => PhotoNode(root)["valueAddedShare"] = -0.1);
        AssertSchemaRefusal(root => PhotoNode(root)["handoffs"] = -1);
        AssertSchemaRefusal(root => PhotoNode(root)["openPainPoints"] = 1.5);
        AssertSchemaRefusal(root => PhotoNode(root)["leadHours"] = -2);
        AssertSchemaRefusal(root => PhotoNode(root)["handoffWaitHours"] = -0.5);
        AssertSchemaRefusal(root => PhotoNode(root)["otherWaitHours"] = "1");
        AssertSchemaRefusal(root => PhotoNode(root)["origin"] = "draft");
        AssertSchemaRefusal(root => PhotoNode(root)["label"] = new string('x', 101));
        AssertSchemaRefusal(root => PhotoNode(root).AsObject().Remove("lostHoursPerMonth"));
        AssertSchemaRefusal(root => PhotoNode(root)["id"] = "41000000-0000-0000-0000-000000000001");
        AssertSchemaRefusal(root => root["project"]![Snapshots] = new JsonArray());
        AssertSchemaRefusal(root => root["project"]![Source] = "orig");
    }

    [Fact]
    public void A_package_of_the_format_2_2_that_carries_photos_is_refused_by_its_own_schema()
    {
        var forge = new PackageForge(sample: WithPhotos(new SampleProject().Before23())).Manifest(manifest =>
        {
            manifest["formatVersion"] = "2.2";
            manifest["schemaVersion"] = "2.2";
        });

        var refusal = Assert.Throws<DomainException>(() => new PackageImporter().Read(forge.Bytes()));

        Assert.Equal(("schema", (object?)PackageFormat.Project), (refusal.Details["reason"], refusal.Details["entry"]));
    }

    [Fact]
    public void The_description_given_to_the_assistant_leaves_out_the_photos()
    {
        var with = new PackageExporter().Describe(WithPhotos().Workspace());
        var without = new PackageExporter().Describe(new SampleProject().Workspace());

        Assert.Equal(without.Select(entry => (entry.Path, entry.Content)), with.Select(entry => (entry.Path, entry.Content)));
        Assert.Contains(Snapshots, new PackageFiles(Written(WithPhotos())).Text(PackageFormat.Project), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1d / 3)]
    [InlineData(0.1 + 0.2)]
    [InlineData(double.Epsilon)]
    [InlineData(double.MaxValue)]
    [InlineData(1e21)]
    [InlineData(123456.789)]
    [InlineData(2)]
    [InlineData(0)]
    public void A_real_is_written_in_its_shortest_form_that_reads_back_to_the_same_value(double value)
    {
        var text = Encoding.UTF8.GetString(CanonicalJson.Write(new JsonObject { ["x"] = CanonicalJson.Real(value) }));

        Assert.Equal(value, JsonNode.Parse(text)!["x"]!.GetValue<double>());
    }

    [Fact]
    public void A_real_keeps_the_digits_a_decimal_would_lose_and_an_integer_value_has_no_fraction()
    {
        string Text(double value) => Encoding.UTF8.GetString(CanonicalJson.Write(new JsonObject { ["x"] = CanonicalJson.Real(value) }));

        Assert.Equal("{\n  \"x\": 0.30000000000000004\n}\n", Text(0.1 + 0.2));
        Assert.Equal("{\n  \"x\": 2\n}\n", Text(2));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void A_real_that_is_not_finite_has_no_form_in_the_exchange_formats(double value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CanonicalJson.Real(value));
}
