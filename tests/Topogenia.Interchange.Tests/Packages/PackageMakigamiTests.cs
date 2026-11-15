// PackageMakigamiTests.cs
// The value classes of the steps and the media of the documents in a package (Makigami M1, M3, ADR 095): the format 2.3 as soon as one is carried, read back as written (a class on a step without content is refused by `ImportedRecordsTests`).

using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Interchange.Packages;
using Topogenia.Interchange.Tests.Support;

namespace Topogenia.Interchange.Tests.Packages;

public sealed class PackageMakigamiTests
{
    private static long next;

    private static OperationContext Context() =>
        new(Guid.Parse("00000000-0000-0000-0000-0000000000f3"), SampleProject.At(1000), "fr", AppSettings.Default,
            () => new Guid($"31000000-0000-0000-0000-{Interlocked.Increment(ref next):x12}"));

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
    public void A_value_class_alone_or_a_medium_alone_is_written_in_the_format_2_3()
    {
        var classOnly = new SampleProject().Before23().With(record => record is Step { Code: "CONGES-E004" } step ? step with { ValueClass = ValueClass.ValueAdded } : record);
        var mediumOnly = new SampleProject().Before23().With(record => record is Document { Name: "Formulaire de demande" } document
            ? document with { Medium = DocumentMedium.Software } : record);
        Assert.Equal(("2.3", "2.3"), (VersionOf(classOnly), VersionOf(mediumOnly)));
        Assert.Equal("2.1", VersionOf(new SampleProject().Before23()));
    }

    [Fact]
    public void The_classes_and_the_media_are_read_back_as_they_were_written()
    {
        var sample = new SampleProject();
        var workspace = Rebuilt(Written(sample));
        Assert.Equal(
            sample.Records.OfType<Step>().Where(step => step.ValueClass is not null).Select(step => (step.Code[7..], step.ValueClass)).Order(),
            workspace.All<Step>().Where(step => step.ValueClass is not null).Select(step => (step.Code[(step.Code.IndexOf('-', StringComparison.Ordinal) + 1)..], step.ValueClass)).Order());
        var media = sample.Records.OfType<Document>().ToDictionary(document => document.Name, document => document.Medium, StringComparer.Ordinal);
        Assert.All(workspace.All<Document>(), document => Assert.Equal(media[document.Name], document.Medium));
        Assert.Equal(5, workspace.All<Document>().Count(document => document.Medium is not null));
    }
}
