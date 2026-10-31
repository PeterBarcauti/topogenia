// PackageReaderTests.cs
// The guards of a received package (spec 5.4, 7.3, ADR 052): archive, entries, paths, sizes, hashes, format, schema.

using Topogenia.Domain.Errors;
using Topogenia.Interchange.Packages;
using Topogenia.Interchange.Tests.Support;

namespace Topogenia.Interchange.Tests.Packages;

public sealed class PackageReaderTests
{
    private static DomainException Refused(byte[] package, PackageLimits? limits = null) =>
        Assert.Throws<DomainException>(() => new PackageImporter(limits ?? PackageLimits.Default).Read(package));

    private static void AssertInvalid(DomainException refusal, string reason, string? entry = null)
    {
        Assert.Equal((ErrorCodes.InvalidPackage, RefusalKind.Invalid, (object?)reason), (refusal.Code, refusal.Kind, refusal.Details["reason"]));
        if (entry is not null)
            Assert.Equal(entry, refusal.Details["entry"]);
    }

    [Fact]
    public void A_package_written_by_the_export_is_read_with_its_code_its_people_and_what_it_holds()
    {
        var read = new PackageImporter().Read(new PackageForge().Bytes());

        Assert.Equal(("CONGES", true), (read.ProjectCode, read.WithPeople));
        Assert.Equal(["EXEMPLE\\alice", "EXEMPLE\\bruno", "EXEMPLE\\chloe", "EXEMPLE\\denis"], read.Logins.Order(StringComparer.Ordinal));
        Assert.Equal(["EXEMPLE\\alice", "EXEMPLE\\bruno", "EXEMPLE\\chloe"], read.Members.Select(member => member.Login));
        Assert.Equal("Alice Martin", read.Members[0].DisplayName);
        // Maps: root, detail, empty sub-map; the dissolved step of the open frame is not a live step.
        Assert.Equal(new Application.Ports.PackageCounts(3, 6, 19, 20, 3, 3, 3, 7), read.Counts);
    }

    [Fact]
    public void A_package_without_the_people_names_no_login_and_no_member()
    {
        var read = new PackageImporter().Read(new PackageForge(withoutPeople: true).Bytes());

        Assert.False(read.WithPeople);
        Assert.Empty(read.Logins);
        Assert.Empty(read.Members);
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0x50, 0x4b, 0x03, 0x04, 1, 2, 3 })]
    [InlineData(new byte[] { (byte)'{', (byte)'}' })]
    public void What_is_not_a_readable_zip_archive_is_refused(byte[] package) => AssertInvalid(Refused(package), "archive");

    [Fact]
    public void A_truncated_archive_is_refused()
    {
        var package = new PackageForge().Bytes();

        AssertInvalid(Refused(package[..(package.Length / 2)]), "archive");
    }

    [Fact]
    public void An_archive_of_more_entries_than_the_limit_is_refused_before_any_is_read()
    {
        var limits = PackageLimits.Default with { MaxEntries = 7 };

        AssertInvalid(Refused(new PackageForge().Bytes(), limits), "entries");
        Assert.Equal(8, PackageLimits.Default.MaxEntries);
    }

    [Theory]
    [InlineData("../maps.json")]
    [InlineData("a/../../maps.json")]
    [InlineData("/maps.json")]
    [InlineData("\\maps.json")]
    [InlineData("C:/maps.json")]
    [InlineData("..\\maps.json")]
    public void An_entry_whose_path_leaves_the_archive_is_refused(string path) =>
        AssertInvalid(Refused(new PackageForge().Rename("maps.json", path).Bytes()), "path", path);

    [Theory]
    [InlineData("readme.txt")]
    [InlineData("maps/maps.json")]
    [InlineData("MAPS.json")]
    public void An_entry_of_an_unknown_name_is_refused(string path) =>
        AssertInvalid(Refused(new PackageForge().Rename("translations.json", path).Bytes()), "entries", path);

    [Fact]
    public void A_package_without_manifest_is_refused() =>
        AssertInvalid(Refused(new PackageForge().Without("manifest.json").Bytes()), "entries", "manifest.json");

    [Fact]
    public void An_entry_announced_by_the_manifest_but_absent_from_the_archive_is_refused() =>
        AssertInvalid(Refused(new PackageForge().Without("sheets.json").Bytes()), "entries", "sheets.json");

    [Fact]
    public void An_entry_of_the_archive_that_the_manifest_does_not_announce_is_refused()
    {
        var forge = new PackageForge().Manifest(manifest =>
            manifest["entries"]!.AsArray().Remove(manifest["entries"]!.AsArray().Single(item => (string?)item!["path"] == "documents.json")));

        AssertInvalid(Refused(forge.Bytes()), "entries", "documents.json");
    }

    [Fact]
    public void Members_in_a_package_that_says_it_left_out_the_people_are_refused()
    {
        var forge = new PackageForge().Manifest(manifest => manifest["options"]!["withoutPeople"] = true);

        AssertInvalid(Refused(forge.Bytes()), "entries", "members.json");
    }

    [Fact]
    public void An_entry_whose_content_does_not_give_the_hash_of_the_manifest_is_refused()
    {
        var forge = new PackageForge();
        var maps = forge.Json("maps.json").ToJsonString().Replace("Salarié", "Salariée", StringComparison.Ordinal);
        var bytes = System.Text.Encoding.UTF8.GetBytes(maps);
        forge.Raw("maps.json", bytes).Manifest(manifest =>
            manifest["entries"]!.AsArray().Single(item => (string?)item!["path"] == "maps.json")!["size"] = bytes.LongLength);

        AssertInvalid(Refused(forge.Bytes()), "hash", "maps.json");
    }

    [Fact]
    public void An_entry_longer_than_the_manifest_announces_is_refused_at_the_first_byte_beyond()
    {
        var forge = new PackageForge();
        forge.Manifest(manifest => manifest["entries"]!.AsArray().Single(item => (string?)item!["path"] == "maps.json")!["size"] = 10);

        AssertInvalid(Refused(forge.Bytes()), "size", "maps.json");
    }

    /// <summary>
    /// The reading stops at the size the manifest announces: an entry of 4 MB announced at 10 bytes, whose compressed data
    /// are broken far beyond its first 10 bytes, is refused for its size; a reading that went on to the end would meet the
    /// broken data and refuse the archive instead.
    /// </summary>
    [Fact]
    public void An_entry_longer_than_the_manifest_announces_is_not_read_beyond_the_announced_size()
    {
        var random = new Random(7);
        var content = new byte[4 * 1024 * 1024];
        for (var i = 0; i < content.Length; i++)
            content[i] = (byte)"0123456789abcdef"[random.Next(16)];
        byte[] Broken(long announced)
        {
            var forge = new PackageForge().Raw("maps.json", content);
            forge.Manifest(manifest => manifest["entries"]!.AsArray().Single(item => (string?)item!["path"] == "maps.json")!["size"] = announced);
            var package = forge.Bytes();
            var name = System.Text.Encoding.ASCII.GetBytes("maps.json");
            var header = package.AsSpan().IndexOf(name) - 30;
            var data = header + 30 + name.Length + BitConverter.ToUInt16(package, header + 28);
            var compressed = BitConverter.ToInt32(package, header + 18);
            package.AsSpan(data + (compressed * 3 / 4), 4096).Fill(0xFF);
            return package;
        }

        AssertInvalid(Refused(Broken(10)), "size", "maps.json");
        // The same broken data, announced at their whole size: read to the end, they are refused as a damaged archive.
        AssertInvalid(Refused(Broken(content.Length)), "archive", "maps.json");
    }

    [Fact]
    public void An_entry_shorter_than_the_manifest_announces_is_refused()
    {
        var forge = new PackageForge();
        forge.Manifest(manifest =>
        {
            var item = manifest["entries"]!.AsArray().Single(candidate => (string?)candidate!["path"] == "maps.json")!;
            item["size"] = item["size"]!.GetValue<long>() + 1;
        });

        AssertInvalid(Refused(forge.Bytes()), "size", "maps.json");
    }

    [Fact]
    public void An_entry_beyond_the_limit_of_one_entry_is_refused_whatever_the_manifest_announces()
    {
        var limits = PackageLimits.Default with { Entry = 2000 };

        AssertInvalid(Refused(new PackageForge().Bytes(), limits), "size", "maps.json");
        Assert.Equal(200L * 1024 * 1024, PackageLimits.Default.Entry);
    }

    [Fact]
    public void A_package_beyond_the_total_uncompressed_limit_is_refused()
    {
        var files = new PackageFiles(new PackageForge().Bytes());
        var total = files.Bytes.Values.Sum(content => content.LongLength);
        var limits = PackageLimits.Default with { Total = total - 1 };

        AssertInvalid(Refused(new PackageForge().Bytes(), limits), "size");
        Assert.Null(Record.Exception(() => new PackageImporter(PackageLimits.Default with { Total = total }).Read(new PackageForge().Bytes())));
        Assert.Equal(500L * 1024 * 1024, PackageLimits.Default.Total);
    }

    [Fact]
    public void A_manifest_beyond_its_limit_is_refused()
    {
        var limits = PackageLimits.Default with { Manifest = 100 };

        AssertInvalid(Refused(new PackageForge().Bytes(), limits), "size", "manifest.json");
        Assert.Equal(1024 * 1024, PackageLimits.Default.Manifest);
    }

    [Theory]
    [InlineData("3.0")]
    [InlineData("2.2")]
    [InlineData("10.0")]
    public void A_package_of_a_later_format_is_refused_with_its_version(string version)
    {
        var refusal = Refused(new PackageForge().Manifest(manifest => manifest["formatVersion"] = version).Bytes());

        Assert.Equal((ErrorCodes.PackageTooRecent, (object?)version, (object?)"2.1"), (refusal.Code, refusal.Details["version"], refusal.Details["supported"]));
    }

    [Theory]
    [InlineData("format", "autre-paquet")]
    [InlineData("formatVersion", "1.0")]
    [InlineData("formatVersion", "deux")]
    public void A_package_of_another_format_or_an_earlier_version_is_refused(string property, string value) =>
        AssertInvalid(Refused(new PackageForge().Manifest(manifest => manifest[property] = value).Bytes()), "format", "manifest.json");

    [Fact]
    public void A_manifest_that_breaks_the_schema_is_refused() =>
        AssertInvalid(Refused(new PackageForge().Manifest(manifest => manifest["projectCode"] = "conges").Bytes()), "schema", "manifest.json");

    [Fact]
    public void An_entry_that_breaks_the_schema_is_refused_with_the_paths_at_fault()
    {
        var forge = new PackageForge().With("project.json", node => node["project"]!["name"] = "<b>Congés</b>");

        var refusal = Refused(forge.Bytes());

        AssertInvalid(refusal, "schema", "project.json");
        Assert.NotEmpty((IReadOnlyList<string>)refusal.Details["paths"]!);
    }

    [Fact]
    public void An_entry_that_is_not_json_is_refused() =>
        AssertInvalid(Refused(new PackageForge().Raw("sheets.json", "{\"sheets\": [").Announce("sheets.json").Bytes()), "json", "sheets.json");

    [Fact]
    public void An_entry_that_is_json_but_not_an_object_is_refused() =>
        AssertInvalid(Refused(new PackageForge().Raw("sheets.json", "[]").Announce("sheets.json").Bytes()), "schema", "sheets.json");
}
