// SamplePackageTests.cs
// The fictional package shipped in samples/ (spec 3.1, P5-T01): it follows the schema and is what the writer gives today.

using Topogenia.Interchange.Packages;
using Topogenia.Interchange.Tests.Support;

namespace Topogenia.Interchange.Tests.Packages;

/// <summary>
/// samples/conges.topogenia is the export, without the people (the default, AR-34), of the fictional project of
/// <see cref="SampleProject"/>. To write it again after a change of the format: set SAMPLES_WRITE=1 and run this class.
/// </summary>
public sealed class SamplePackageTests
{
    private static string SamplePath => Path.Combine(RepositoryRoot.Path, "samples", "conges" + PackageFormat.Extension);

    private static byte[] Expected()
    {
        var sample = new SampleProject();
        return PackageWriter.Write(sample.Workspace(), sample.Logins, new PackageOptions(WithoutPeople: true), new PackageStamp(SampleProject.At(300), null));
    }

    [Fact]
    public void The_sample_package_holds_exactly_what_the_writer_gives_for_the_fictional_project()
    {
        if (Environment.GetEnvironmentVariable("SAMPLES_WRITE") == "1")
            File.WriteAllBytes(SamplePath, Expected());
        var shipped = new PackageFiles(File.ReadAllBytes(SamplePath));
        var expected = new PackageFiles(Expected());

        Assert.Equal(expected.Paths, shipped.Paths);
        Assert.All(expected.Paths, path => Assert.Equal(expected.Text(path), shipped.Text(path)));
    }

    [Fact]
    public void The_sample_package_follows_the_schema_and_its_manifest_matches_its_entries()
    {
        var shipped = new PackageFiles(File.ReadAllBytes(SamplePath));

        foreach (var path in shipped.Paths)
            Assert.Empty(PackageDefinition.Current.Validate(path, shipped.Element(path)));
        foreach (var entry in shipped.Json("manifest.json")["entries"]!.AsArray())
            Assert.Equal((string?)entry!["sha256"], PackageWriter.Sha256(shipped.Bytes[(string)entry["path"]!]));
        Assert.DoesNotContain("EXEMPLE", string.Join('\n', shipped.Paths.Select(shipped.Text)), StringComparison.Ordinal);
    }
}
