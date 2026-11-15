// PackageRangesAndActionsTests.cs
// The bounds of the duration of a sheet and the action plan of a pain point in a package (Makigami M5, M7, ADR 095): the format 2.3 as soon as one is carried, in a draft or in the frozen content of a version, read back as written, refused out of order.

using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Interchange.Packages;
using Topogenia.Interchange.Tests.Support;

namespace Topogenia.Interchange.Tests.Packages;

public sealed class PackageRangesAndActionsTests
{
    private static long next;

    private static OperationContext Context() =>
        new(Guid.Parse("00000000-0000-0000-0000-0000000000f4"), SampleProject.At(1000), "fr", AppSettings.Default,
            () => new Guid($"32000000-0000-0000-0000-{Interlocked.Increment(ref next):x12}"));

    private static byte[] Written(SampleProject sample) =>
        PackageWriter.Write(sample.Workspace(), sample.Logins, new PackageOptions(false), new PackageStamp(SampleProject.At(500), "EXEMPLE\\alice"));

    private static ProjectWorkspace Rebuilt(byte[] package)
    {
        var importer = new PackageImporter();
        var records = importer.Build(importer.Read(package), new ImportPlan(Context(), "MAKI", _ => null, false));
        return new ProjectWorkspace(((Project)records[0]).Id, records);
    }

    private static string VersionOf(SampleProject sample) => new PackageFiles(Written(sample)).Json(PackageFormat.Manifest)["formatVersion"]!.GetValue<string>();

    [Fact]
    public void A_bound_alone_on_a_draft_or_in_a_version_or_a_plan_alone_is_written_in_the_format_2_3()
    {
        var drafted = new SampleProject().Before23().With(record => record is StepSheet { Status: SheetStatus.Validated } sheet ? sheet with { DurationMaxMinutes = 40m } : record);
        var frozen = new SampleProject().Before23().With(record => record is StepSheetVersion { VersionNumber: 2 } version
            ? version with { ContentJson = SheetJson.Freeze(SheetJson.Thaw(version.ContentJson).Content with { DurationMinMinutes = 5m }) } : record);
        var planned = new SampleProject().Before23().With(record => record is PainPoint { Code: "CONGES-I001" } painPoint ? painPoint with { Owner = "Paie" } : record);
        var dated = new SampleProject().Before23().With(record => record is PainPoint { Code: "CONGES-I001" } painPoint
            ? painPoint with { DueDate = new DateOnly(2027, 1, 31) } : record);
        Assert.Equal(("2.3", "2.3", "2.3", "2.3"), (VersionOf(drafted), VersionOf(frozen), VersionOf(planned), VersionOf(dated)));
        Assert.Equal("2.1", VersionOf(new SampleProject().Before23()));
    }

    [Fact]
    public void The_bounds_and_the_plans_are_read_back_as_they_were_written()
    {
        var sample = new SampleProject();
        var workspace = Rebuilt(Written(sample));
        var sheet = workspace.All<StepSheet>().Single(candidate => candidate.Status == SheetStatus.Validated && candidate.DurationMinMinutes is not null);
        Assert.Equal((10m, 15.5m, 30m), (sheet.DurationMinMinutes!.Value, sheet.DurationMinutes!.Value, sheet.DurationMaxMinutes!.Value));
        var version = SheetJson.Thaw(workspace.All<StepSheetVersion>().Single(candidate => candidate.SheetId == sheet.Id && candidate.VersionNumber == 2).ContentJson).Content;
        Assert.Equal((10m, 25m), (version.DurationMinMinutes!.Value, version.DurationMaxMinutes!.Value));
        var painPoint = workspace.All<PainPoint>().Single(candidate => candidate.Action is not null);
        Assert.Equal(("Rappel automatique au bout de 48 h", "Service RH", (DateOnly?)new DateOnly(2026, 12, 15)), (painPoint.Action, painPoint.Owner, painPoint.DueDate));
        Assert.Empty(ImportedRecords.Violations(workspace, Pmap.PmapReaderTests.Languages));
    }

    [Fact]
    public void A_package_whose_bounds_are_out_of_order_or_whose_due_date_is_not_a_day_is_refused()
    {
        var disordered = new SampleProject().With(record => record is StepSheet { Status: SheetStatus.Validated } sheet ? sheet with { DurationMaxMinutes = 12m } : record);
        Assert.Contains("sheet.durationBounds", ImportedRecords.Violations(Rebuilt(Written(disordered)), Pmap.PmapReaderTests.Languages));
        var forged = new PackageForge().With(PackageFormat.PainPoints, node => node["painPoints"]![0]!["dueDate"] = "15/12/2026").Bytes();
        Assert.Throws<DomainException>(() => new PackageImporter().Read(forged));
    }
}
