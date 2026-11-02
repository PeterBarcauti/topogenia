// SamplePackageTests.cs
// The fictional packages shipped in samples/ (spec 3.1, P5-T01), written in French and in English: they follow the schema and are what the writer gives today.

using Topogenia.Interchange.Packages;
using Topogenia.Interchange.Tests.Support;

namespace Topogenia.Interchange.Tests.Packages;

/// <summary>
/// samples/conges.topogenia and samples/leave.topogenia are the exports, without the people (the default, AR-34), of the
/// fictional project of <see cref="SampleProject"/>, written in French and in English. To write them again after a change
/// of the format: set SAMPLES_WRITE=1 and run this class.
/// </summary>
public sealed class SamplePackageTests
{
    public static TheoryData<string, bool> Samples => new() { { "conges", false }, { "leave", true } };

    private static string SamplePath(string name) => Path.Combine(RepositoryRoot.Path, "samples", name + PackageFormat.Extension);

    private static byte[] Expected(bool english)
    {
        var sample = new SampleProject(english: english);
        return PackageWriter.Write(sample.Workspace(), sample.Logins, new PackageOptions(WithoutPeople: true), new PackageStamp(SampleProject.At(300), null));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void The_sample_package_holds_exactly_what_the_writer_gives_for_the_fictional_project(string name, bool english)
    {
        if (Environment.GetEnvironmentVariable("SAMPLES_WRITE") == "1")
            File.WriteAllBytes(SamplePath(name), Expected(english));
        var shipped = new PackageFiles(File.ReadAllBytes(SamplePath(name)));
        var expected = new PackageFiles(Expected(english));

        Assert.Equal(expected.Paths, shipped.Paths);
        Assert.All(expected.Paths, path => Assert.Equal(expected.Text(path), shipped.Text(path)));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void The_sample_package_follows_the_schema_and_its_manifest_matches_its_entries(string name, bool english)
    {
        var shipped = new PackageFiles(File.ReadAllBytes(SamplePath(name)));

        foreach (var path in shipped.Paths)
            Assert.Empty(PackageDefinition.Current.Validate(path, shipped.Element(path)));
        foreach (var entry in shipped.Json("manifest.json")["entries"]!.AsArray())
            Assert.Equal((string?)entry!["sha256"], PackageWriter.Sha256(shipped.Bytes[(string)entry["path"]!]));
        Assert.DoesNotContain("EXEMPLE", string.Join('\n', shipped.Paths.Select(shipped.Text)), StringComparison.Ordinal);
        Assert.Equal((english ? "LEAVE" : "CONGES", english ? "en" : "fr"),
            ((string?)shipped.Json("project.json")["project"]!["code"], (string?)shipped.Json("project.json")["project"]!["sourceLanguage"]));
    }

    [Fact]
    public void The_sample_written_in_english_has_no_french_text_but_its_translations_and_its_glossary()
    {
        var shipped = new PackageFiles(File.ReadAllBytes(SamplePath("leave")));
        var french = new[] { "é", "è", "à", "ç", " la ", " le ", " de " };
        foreach (var path in shipped.Paths.Where(path => path is not "translations.json" and not "project.json" and not "manifest.json"))
            Assert.DoesNotContain(french, word => shipped.Text(path).Contains(word, StringComparison.Ordinal));
    }
}
