// PackageArchiveTests.cs
// The archive and the manifest of the exchange package (spec 7.1, 7.2): a ZIP, one project, entries with size and SHA-256.

using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Topogenia.Interchange.Packages;
using Topogenia.Interchange.Tests.Support;

namespace Topogenia.Interchange.Tests.Packages;

public sealed class PackageArchiveTests
{
    private static readonly DateTime ExportedAt = new DateTime(2026, 10, 25, 18, 5, 30, 123, DateTimeKind.Utc).AddTicks(4567);

    private static PackageFiles Export(bool withoutPeople, string? exportedBy = "EXEMPLE\\alice")
    {
        var sample = new SampleProject();
        return new PackageFiles(PackageWriter.Write(sample.Workspace(), sample.Logins, new PackageOptions(withoutPeople),
            new PackageStamp(ExportedAt, exportedBy)));
    }

    [Fact]
    public void A_package_is_a_zip_with_the_manifest_first_then_the_entries_of_one_project_in_their_order()
    {
        var files = Export(withoutPeople: false);

        Assert.Equal(["manifest.json", "project.json", "members.json", "maps.json", "sheets.json", "documents.json", "painpoints.json", "translations.json"],
            files.Paths);
        Assert.Equal("CONGES", (string?)files.Json("project.json")["project"]!["code"]);
        Assert.IsType<JsonObject>(files.Json("project.json")["project"]);
    }

    [Fact]
    public void Without_the_people_the_package_has_no_members_entry()
    {
        var files = Export(withoutPeople: true);

        Assert.Equal(["manifest.json", "project.json", "maps.json", "sheets.json", "documents.json", "painpoints.json", "translations.json"], files.Paths);
    }

    [Fact]
    public void The_manifest_names_the_format_its_versions_the_export_and_the_project()
    {
        var manifest = Export(withoutPeople: false).Json("manifest.json");

        Assert.Equal("topogenia-package", (string?)manifest["format"]);
        Assert.Equal("2.1", (string?)manifest["formatVersion"]);
        Assert.Equal("3.0.0", (string?)manifest["appVersion"]);
        Assert.Equal("2.1", (string?)manifest["schemaVersion"]);
        Assert.Equal("2026-10-25T18:05:30.1234567Z", (string?)manifest["exportedAt"]);
        Assert.Equal("EXEMPLE\\alice", (string?)manifest["exportedBy"]);
        Assert.Equal("CONGES", (string?)manifest["projectCode"]);
        Assert.False((bool)manifest["options"]!["withoutPeople"]!);
        Assert.Equal(["appVersion", "entries", "exportedAt", "exportedBy", "format", "formatVersion", "options", "projectCode", "schemaVersion"],
            manifest.AsObject().Select(property => property.Key));
    }

    [Fact]
    public void Without_the_people_the_manifest_does_not_name_the_author_of_the_export()
    {
        var manifest = Export(withoutPeople: true).Json("manifest.json");

        Assert.Null(manifest["exportedBy"]);
        Assert.True((bool)manifest["options"]!["withoutPeople"]!);
    }

    [Fact]
    public void An_export_without_known_author_leaves_it_out()
    {
        var manifest = Export(withoutPeople: false, exportedBy: null).Json("manifest.json");

        Assert.False(manifest.AsObject().ContainsKey("exportedBy"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_manifest_gives_the_path_the_size_and_the_sha256_of_every_entry_in_the_order_of_the_archive(bool withoutPeople)
    {
        var files = Export(withoutPeople);
        var entries = files.Json("manifest.json")["entries"]!.AsArray();

        Assert.Equal(files.Paths.Skip(1), entries.Select(entry => (string?)entry!["path"]));
        foreach (var entry in entries)
        {
            var bytes = files.Bytes[(string)entry!["path"]!];
            Assert.Equal(bytes.LongLength, (long)entry["size"]!);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), (string?)entry["sha256"]);
            Assert.Equal(["path", "sha256", "size"], entry.AsObject().Select(property => property.Key));
        }
    }

    [Fact]
    public void The_manifest_is_not_one_of_its_own_entries()
    {
        var entries = Export(withoutPeople: false).Json("manifest.json")["entries"]!.AsArray();

        Assert.DoesNotContain(entries, entry => (string?)entry!["path"] == "manifest.json");
    }

    [Fact]
    public void The_format_names_are_technical_names_fixed_like_the_namespaces()
    {
        Assert.Equal("topogenia-package", PackageFormat.Name);
        Assert.Equal(".topogenia", PackageFormat.Extension);
        Assert.Equal("topogenia-package-2.1.schema.json", PackageFormat.SchemaFile);
        Assert.Equal(PackageFormat.Version, PackageFormat.SchemaVersion);
    }
}
