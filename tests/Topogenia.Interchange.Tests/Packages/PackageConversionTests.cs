// PackageConversionTests.cs
// The chain of converters of spec 7.3 (ADR 053), proven by fictitious earlier formats 1.8 and 1.9 that the application never ships.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Interchange.Packages;
using Topogenia.Interchange.Schemas;
using Topogenia.Interchange.Tests.Support;

namespace Topogenia.Interchange.Tests.Packages;

/// <summary>
/// Format 1.9 names the project <c>title</c> and has no translations.json; format 1.8 is 1.9 with painpoints.json named
/// irritants.json. Their schemas are the schema 2.0 changed accordingly, each converter does the opposite change.
/// </summary>
public sealed class PackageConversionTests
{
    private static readonly Guid Importer = Guid.Parse("00000000-0000-0000-0000-0000000000f1");

    private static int next;

    private static JsonObject Schema20() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(RepositoryRoot.Path, "schemas", PackageFormat.SchemaFile)))!.AsObject();

    private static JsonArray EntryNames(JsonObject schema) =>
        schema["$defs"]!["manifest"]!["properties"]!["entries"]!["items"]!["properties"]!["path"]!["enum"]!.AsArray();

    private static SchemaValidator Validator(JsonObject schema) => new(JsonSerializer.SerializeToElement(schema));

    private static JsonObject Schema19()
    {
        var schema = Schema20();
        var names = EntryNames(schema);
        names.Remove(names.Single(name => (string?)name == PackageFormat.Translations));
        var entries = schema["$defs"]!["manifest"]!["properties"]!["entries"]!.AsObject();
        entries["minItems"] = 5;
        entries["maxItems"] = 6;
        var project = schema["$defs"]!["project"]!["properties"]!["project"]!.AsObject();
        var properties = project["properties"]!.AsObject();
        var name = properties["name"]!.DeepClone();
        properties.Remove("name");
        properties["title"] = name;
        var required = project["required"]!.AsArray();
        required[required.IndexOf(required.Single(item => (string?)item == "name"))] = "title";
        return schema;
    }

    private static JsonObject Schema18()
    {
        var schema = Schema19();
        var names = EntryNames(schema);
        names[names.IndexOf(names.Single(name => (string?)name == PackageFormat.PainPoints))] = "irritants.json";
        schema["$defs"]!["irritants"] = schema["$defs"]!["painpoints"]!.DeepClone();
        return schema;
    }

    private static readonly IReadOnlyList<string> Entries19 = [.. PackageFormat.Entries.Where(path => path != PackageFormat.Translations)];

    private static readonly PackageDefinition Format19 = new("1.9", Entries19, Validator(Schema19()));

    private static readonly PackageDefinition Format18 =
        new("1.8", [.. Entries19.Select(path => path == PackageFormat.PainPoints ? "irritants.json" : path)], Validator(Schema18()));

    /// <summary>
    /// 1.9 to 2.0: the title becomes the name, and the translations come empty. A faulty one also leaves a property that the
    /// schema 2.0 does not admit and that the rebuild would ignore: only the check of its output refuses it.
    /// </summary>
    private sealed class From19(bool faulty = false) : IPackageConverter
    {
        public PackageDefinition From => Format19;

        public string To => PackageFormat.Version;

        public void Convert(JsonObject manifest, IDictionary<string, JsonObject> content)
        {
            var project = content[PackageFormat.Project]["project"]!.AsObject();
            var title = project["title"]!.DeepClone();
            project.Remove("title");
            project["name"] = title;
            if (faulty)
                project["colour"] = "bleu";
            content[PackageFormat.Translations] = new JsonObject { ["translations"] = new JsonArray() };
        }
    }

    /// <summary>1.8 to 1.9: irritants.json becomes painpoints.json.</summary>
    private sealed class From18(string to = "1.9") : IPackageConverter
    {
        public PackageDefinition From => Format18;

        public string To => to;

        public void Convert(JsonObject manifest, IDictionary<string, JsonObject> content)
        {
            content[PackageFormat.PainPoints] = content["irritants.json"];
            content.Remove("irritants.json");
        }
    }

    /// <summary>A converter that starts at <paramref name="from"/> and leads to <paramref name="to"/>, for the checks of the chain.</summary>
    private sealed class Link(string from, string to) : IPackageConverter
    {
        public PackageDefinition From { get; } = new(from, Entries19, Validator(Schema19()));

        public string To => to;

        public void Convert(JsonObject manifest, IDictionary<string, JsonObject> content)
        {
        }
    }

    private static PackageChain Chain() => new([new From19(), new From18()]);

    /// <summary>The package 2.0 of the sample with no translation: what a package of 1.9 or 1.8 holds once converted.</summary>
    private static PackageForge Current() => new PackageForge().With(PackageFormat.Translations, node => node["translations"] = new JsonArray());

    /// <summary>The same project written in the format 1.9.</summary>
    private static PackageForge Earlier19()
    {
        var forge = Current().Without(PackageFormat.Translations).With(PackageFormat.Project, node =>
        {
            var project = node["project"]!.AsObject();
            var name = project["name"]!.DeepClone();
            project.Remove("name");
            project["title"] = name;
        });
        return forge.Manifest(manifest =>
        {
            var entries = manifest["entries"]!.AsArray();
            entries.Remove(entries.Single(item => (string?)item!["path"] == PackageFormat.Translations));
            manifest["formatVersion"] = "1.9";
            manifest["schemaVersion"] = "1.9";
        });
    }

    /// <summary>The same project written in the format 1.8.</summary>
    private static PackageForge Earlier18() =>
        Earlier19().Rename(PackageFormat.PainPoints, "irritants.json").Manifest(manifest =>
        {
            var item = manifest["entries"]!.AsArray().Single(entry => (string?)entry!["path"] == PackageFormat.PainPoints)!;
            item["path"] = "irritants.json";
            manifest["formatVersion"] = "1.8";
            manifest["schemaVersion"] = "1.8";
        });

    private static OperationContext Context() =>
        new(Importer, SampleProject.At(1000), "fr", AppSettings.Default, () => new Guid($"20000000-0000-0000-0000-{Interlocked.Increment(ref next):x12}"));

    /// <summary>The entries that the export writes for the project rebuilt from <paramref name="forge"/>.</summary>
    private static string Rebuilt(PackageForge forge, PackageImporter importer)
    {
        var read = importer.Read(forge.Bytes());
        var users = forge.Sample.Records.OfType<AppUser>().ToDictionary(user => user.LoginNormalized, user => user.Id);
        var records = importer.Build(read, new ImportPlan(Context(), read.ProjectCode,
            login => users.TryGetValue(Login.Normalize(login), out var id) ? id : null, true));
        var workspace = new ProjectWorkspace(((Project)records[0]).Id, [.. records, .. forge.Sample.Records.OfType<AppUser>()]);
        return string.Join("\n---\n", PackageWriter.Entries(workspace, forge.Sample.Logins, new PackageOptions(false))
            .Select(entry => entry.Path + "\n" + Encoding.UTF8.GetString(entry.Content)));
    }

    private static DomainException Refused(PackageForge forge, PackageChain chain) =>
        Assert.Throws<DomainException>(() => new PackageImporter(PackageLimits.Default, chain).Read(forge.Bytes()));

    private static void AssertInvalid(DomainException refusal, string reason, string entry)
    {
        Assert.Equal((ErrorCodes.InvalidPackage, (object?)reason), (refusal.Code, refusal.Details["reason"]));
        Assert.Equal(entry, refusal.Details["entry"]);
    }

    [Fact]
    public void A_package_of_an_earlier_format_rebuilds_the_project_that_its_package_of_the_current_format_rebuilds()
    {
        var importer = new PackageImporter(PackageLimits.Default, Chain());

        var converted = importer.Read(Earlier19().Bytes());

        Assert.Equal(("CONGES", true, 0), (converted.ProjectCode, converted.WithPeople, converted.Counts.Translations));
        Assert.Equal(importer.Read(Current().Bytes()).Counts, converted.Counts);
        Assert.Equal(Rebuilt(Current(), new PackageImporter()), Rebuilt(Earlier19(), importer));
    }

    [Fact]
    public void Two_links_bring_a_package_two_formats_back_and_an_entry_renamed_by_a_format_is_read_under_its_old_name()
    {
        var importer = new PackageImporter(PackageLimits.Default, Chain());

        Assert.Equal(Rebuilt(Current(), new PackageImporter()), Rebuilt(Earlier18(), importer));
    }

    [Fact]
    public void A_package_of_an_earlier_format_follows_the_schema_of_its_own_format_before_its_conversion()
    {
        var written20 = Earlier19().With(PackageFormat.Project, node =>
        {
            var project = node["project"]!.AsObject();
            project["name"] = project["title"]!.DeepClone();
            project.Remove("title");
        });

        AssertInvalid(Refused(written20, Chain()), "schema", PackageFormat.Project);
    }

    [Fact]
    public void What_a_converter_writes_follows_the_schema_of_the_next_format_or_the_package_is_refused()
    {
        AssertInvalid(Refused(Earlier19(), new PackageChain([new From19(faulty: true)])), "schema", PackageFormat.Project);
    }

    [Fact]
    public void The_entries_of_an_earlier_format_are_checked_against_its_own_manifest_before_their_conversion()
    {
        var changed = Earlier19();
        var bytes = Encoding.UTF8.GetBytes(changed.Json(PackageFormat.Maps).ToJsonString().Replace("Salarié", "Salariée", StringComparison.Ordinal));
        changed.Raw(PackageFormat.Maps, bytes).Manifest(manifest =>
            manifest["entries"]!.AsArray().Single(item => (string?)item!["path"] == PackageFormat.Maps)!["size"] = bytes.LongLength);

        AssertInvalid(Refused(changed, Chain()), "hash", PackageFormat.Maps);
        AssertInvalid(Refused(Earlier19().Without(PackageFormat.Sheets), Chain()), "entries", PackageFormat.Sheets);
        AssertInvalid(Refused(Earlier19().Raw(PackageFormat.Translations, "{\"translations\":[]}"), Chain()), "entries", PackageFormat.Translations);
    }

    [Fact]
    public void An_entry_of_an_earlier_format_only_is_refused_in_a_package_of_the_current_format()
    {
        AssertInvalid(Refused(new PackageForge(withoutPeople: true).Raw("irritants.json", "{\"painPoints\":[]}"), Chain()), "entries", "irritants.json");
    }

    [Fact]
    public void An_earlier_format_that_no_link_converts_is_refused_and_a_later_one_is_still_too_recent()
    {
        var chain = new PackageChain([new From19()]);

        AssertInvalid(Refused(Earlier19().Manifest(manifest => manifest["formatVersion"] = "1.8"), chain), "format", PackageFormat.Manifest);
        Assert.Equal(ErrorCodes.PackageTooRecent, Refused(Current().Manifest(manifest => manifest["formatVersion"] = "2.2"), chain).Code);
    }

    [Fact]
    public void A_chain_is_refused_when_a_link_does_not_reach_the_current_format_or_a_format_is_converted_twice()
    {
        Assert.Throws<ArgumentException>(() => new PackageChain([new Link("1.9", "1.95")]));
        Assert.Throws<ArgumentException>(() => new PackageChain([new Link("1.9", "2.0"), new Link("1.9", "2.0")]));
        Assert.Throws<ArgumentException>(() => new PackageChain([new Link("2.0", "2.0")]));
        Assert.Throws<ArgumentException>(() => new PackageChain([new Link("2.1", "2.0")]));
        Assert.Throws<ArgumentException>(() => new PackageChain([new Link("2.2", "2.1")]));
        Assert.Throws<ArgumentException>(() => new PackageChain([new Link("1.8", "1.9"), new Link("1.9", "1.8")]));
        Assert.Contains("irritants.json", new PackageChain([new From18(), new From19()]).KnownEntries);
    }

    /// <summary>The package of the sample without the value stream, in the format 2.1: its work time is the default one.</summary>
    private static PackageForge Plain21() => new(sample: new SampleProject(valueStream: false));

    /// <summary>The same package written in the format 2.0 (ADR 085): no work time, the same entries.</summary>
    private static PackageForge Written20() =>
        Plain21().With(PackageFormat.Project, node =>
        {
            var project = node["project"]!.AsObject();
            project.Remove("workdayHours");
            project.Remove("fteHoursPerMonth");
        }).Manifest(manifest =>
        {
            manifest["formatVersion"] = "2.0";
            manifest["schemaVersion"] = "2.0";
        });

    [Fact]
    public void The_chain_of_the_application_brings_the_format_2_0_to_the_format_2_1_and_no_earlier_one()
    {
        Assert.Equal(PackageFormat.Entries, PackageChain.Application.KnownEntries);
        AssertInvalid(Refused(Earlier19(), PackageChain.Application), "format", PackageFormat.Manifest);
        Assert.Equal(Rebuilt(Current(), new PackageImporter()), Rebuilt(Current(), new PackageImporter(PackageLimits.Default, PackageChain.Application)));
    }

    [Fact]
    public void A_package_2_0_is_read_against_its_own_schema_and_gets_the_work_time_by_default()
    {
        var importer = new PackageImporter();
        var forge = Written20();
        var read = importer.Read(forge.Bytes());
        var users = forge.Sample.Records.OfType<AppUser>().ToDictionary(user => user.LoginNormalized, user => user.Id);
        var project = (Project)importer.Build(read, new ImportPlan(Context(), read.ProjectCode,
            login => users.TryGetValue(Login.Normalize(login), out var id) ? id : null, true))[0];

        Assert.Equal((7m, 151.67m), (project.WorkdayHours, project.FteHoursPerMonth));
        Assert.Equal(Rebuilt(Plain21(), importer), Rebuilt(Written20(), importer));
    }

    [Fact]
    public void A_package_2_0_that_carries_an_entry_of_the_value_stream_is_refused_by_the_schema_2_0()
    {
        var withShare = Written20().With(PackageFormat.Maps, node => node["maps"]![0]!["flows"]![0]!["share"] = 50);
        var withWorkTime = Written20().With(PackageFormat.Project, node => node["project"]!["workdayHours"] = 8);

        AssertInvalid(Refused(withShare, PackageChain.Application), "schema", PackageFormat.Maps);
        AssertInvalid(Refused(withWorkTime, PackageChain.Application), "schema", PackageFormat.Project);
    }
}
