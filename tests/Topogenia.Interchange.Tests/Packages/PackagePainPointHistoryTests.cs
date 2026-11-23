// PackagePainPointHistoryTests.cs
// A status chosen in the application (ADR 116) and the records it appends to the history go through the package without a change of the format: written in time order with the logins, read back as they were, written again the same.

using System.Text.Json;
using System.Text.Json.Nodes;
using Topogenia.Application.Ports;
using Topogenia.Domain.Documents;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Interchange.Packages;
using Topogenia.Interchange.Tests.Support;

namespace Topogenia.Interchange.Tests.Packages;

public sealed class PackagePainPointHistoryTests
{
    private static long next;

    private static readonly JsonSerializerOptions Relaxed = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static OperationContext Context(Guid actor, DateTime now) =>
        new(actor, now, "fr", AppSettings.Default, () => new Guid($"34000000-0000-0000-0000-{Interlocked.Increment(ref next):x12}"));

    private static byte[] Written(ProjectWorkspace workspace, SampleProject sample) =>
        PackageWriter.Write(workspace, sample.Logins, new PackageOptions(false), new PackageStamp(SampleProject.At(5000), "EXEMPLE\\alice"));

    /// <summary>The package rebuilt into new records, each login of the history given back the user it names in the sample.</summary>
    private static ProjectWorkspace Rebuilt(byte[] package, SampleProject sample)
    {
        var importer = new PackageImporter();
        var users = sample.Logins.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);
        var records = importer.Build(importer.Read(package), new ImportPlan(Context(sample.Alice, SampleProject.At(6000)), "MAKI",
            login => users.TryGetValue(login, out var id) ? id : null, false));
        return new ProjectWorkspace(((Project)records[0]).Id, records);
    }

    private static JsonNode PainPointOf(byte[] package, string description) =>
        new PackageFiles(package).Json(PackageFormat.PainPoints)["painPoints"]!.AsArray().Single(point => (string?)point!["description"] == description)!;

    private static List<(PainPointStatus? Before, PainPointStatus After, string? Comment, Guid By, DateTime At)> HistoryOf(ProjectWorkspace workspace, Guid painPoint) =>
        [.. workspace.All<PainPointStatusChange>().Where(change => change.PainPointId == painPoint).OrderBy(change => change.ChangedAt)
            .ThenBy(change => change.StatusBefore is not null)
            .Select(change => (change.StatusBefore, change.StatusAfter, change.Comment, change.ChangedBy, change.ChangedAt))];

    [Fact]
    public void A_status_changed_in_the_application_and_its_history_go_through_the_package_unchanged()
    {
        var sample = new SampleProject();
        var untouched = Written(sample.Workspace(), sample);
        var workspace = sample.Workspace();
        var point = workspace.All<PainPoint>().Single(candidate => candidate.Code == "CONGES-I001");
        var receiving = workspace.All<DocumentUse>().Single(use => use.PainPointId == point.Id).StepId;
        PainPointRules.ChangeStatus(workspace, Context(sample.Alice, SampleProject.At(2000)), receiving, point.Id, PainPointStatus.Implemented);
        PainPointRules.ChangeStatus(workspace, Context(sample.Chloe, SampleProject.At(2100)), receiving, point.Id, PainPointStatus.InProgress);

        var package = Written(workspace, sample);

        // The format does not change: the same version and the same entries as the package of the sample.
        var (before, after) = (new PackageFiles(untouched), new PackageFiles(package));
        Assert.Equal((string?)before.Json(PackageFormat.Manifest)["formatVersion"], (string?)after.Json(PackageFormat.Manifest)["formatVersion"]);
        Assert.Equal(before.Paths, after.Paths);
        var written = PainPointOf(package, point.Description);
        Assert.Equal("in_progress", (string?)written["status"]);
        Assert.Equal(
            """[{"changedAt":"2026-09-01T08:52:00.0000000Z","changedBy":"EXEMPLE\\chloe","statusAfter":"reported"},{"changedAt":"2026-09-01T08:53:00.0000000Z","changedBy":"EXEMPLE\\alice","comment":"Chiffré en atelier","statusAfter":"scored","statusBefore":"reported"},{"changedAt":"2026-09-02T16:50:00.0000000Z","changedBy":"EXEMPLE\\alice","statusAfter":"implemented","statusBefore":"scored"},{"changedAt":"2026-09-02T18:30:00.0000000Z","changedBy":"EXEMPLE\\chloe","statusAfter":"in_progress","statusBefore":"implemented"}]""",
            written["history"]!.ToJsonString(Relaxed));

        // Read back as it was written: the status, every record with its author and its instant, the latest change.
        var rebuilt = Rebuilt(package, sample);
        var again = rebuilt.All<PainPoint>().Single(candidate => candidate.Description == point.Description);
        Assert.Equal(PainPointStatus.InProgress, again.Status);
        Assert.Equal(HistoryOf(workspace, point.Id), HistoryOf(rebuilt, again.Id));
        var latest = PainPointRules.LatestChange(rebuilt, again.Id)!;
        Assert.Equal((PainPointStatus.Implemented, PainPointStatus.InProgress, sample.Chloe, SampleProject.At(2100)),
            (latest.StatusBefore!.Value, latest.StatusAfter, latest.ChangedBy, latest.ChangedAt));
        Assert.Empty(ImportedRecords.Violations(rebuilt, Pmap.PmapReaderTests.Languages));

        // Written again from the rebuilt project, the history is the same text.
        Assert.Equal(written["history"]!.ToJsonString(Relaxed), PainPointOf(Written(rebuilt, sample), point.Description)["history"]!.ToJsonString(Relaxed));
    }
}
