// PackageWriter.cs
// Writes the exchange package of a project (spec 7.2): canonical JSON entries, the manifest with their SHA-256, a ZIP file.

using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Topogenia.Domain.Workspace;
using Topogenia.Interchange.Json;

namespace Topogenia.Interchange.Packages;

/// <summary>An entry of a package: its path in the archive and its bytes.</summary>
public sealed record PackageEntry(string Path, byte[] Content);

/// <summary>
/// The package of one project (spec 7.1: a package holds one project). The workspace must hold every group of the
/// project (structure, sheets, documents, translations, members) and <c>logins</c> the login of every user that the
/// exported records name (<see cref="UsersNamedBy"/>). Two exports of the same state give the same entries byte for byte
/// (spec 7.2); only the manifest carries the time and the author of the export.
/// </summary>
public static class PackageWriter
{
    /// <summary>The users named by the records that an export with people would write: the logins it needs.</summary>
    public static IReadOnlySet<Guid> UsersNamedBy(ProjectWorkspace workspace)
    {
        var collector = new HashSet<Guid>();
        _ = Entries(workspace, new People(id =>
        {
            collector.Add(id);
            return "";
        }, hidden: false), withMembers: true);
        return collector;
    }

    /// <summary>The content entries, in the order of the archive (members.json only with the people).</summary>
    public static IReadOnlyList<PackageEntry> Entries(ProjectWorkspace workspace, IReadOnlyDictionary<Guid, string> logins, PackageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Entries(workspace, new People(logins, options.WithoutPeople), withMembers: !options.WithoutPeople);
    }

    /// <summary>The whole package: the manifest first, then the entries, in a ZIP archive.</summary>
    public static byte[] Write(ProjectWorkspace workspace, IReadOnlyDictionary<Guid, string> logins, PackageOptions options, PackageStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(stamp);
        var entries = Entries(workspace, logins, options);
        var manifest = new PackageEntry(PackageFormat.Manifest, CanonicalJson.Write(Manifest(workspace, options, stamp, entries)));
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in new[] { manifest }.Concat(entries))
            {
                var item = archive.CreateEntry(entry.Path, CompressionLevel.Optimal);
                ZipEntryTime.Fix(item);
                using var content = item.Open();
                content.Write(entry.Content);
            }
        }
        return stream.ToArray();
    }

    /// <summary>The lower-case hexadecimal SHA-256 of an entry, as the manifest gives it.</summary>
    public static string Sha256(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    /// <summary>The version of the application, without the build metadata.</summary>
    public static string ApplicationVersion { get; } =
        (typeof(PackageWriter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "").Split('+')[0];

    private static List<PackageEntry> Entries(ProjectWorkspace workspace, People people, bool withMembers)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var scope = new ExportScope(workspace);
        var content = new List<(string Path, JsonObject Node)>
        {
            (PackageFormat.Project, StructureEntries.Project(scope, people)),
        };
        if (withMembers)
            content.Add((PackageFormat.Members, StructureEntries.Members(scope, people)));
        content.Add((PackageFormat.Maps, StructureEntries.Maps(scope, people)));
        content.Add((PackageFormat.Sheets, ContentEntries.Sheets(scope, people)));
        content.Add((PackageFormat.Documents, ContentEntries.Documents(scope, people)));
        content.Add((PackageFormat.PainPoints, ContentEntries.PainPoints(scope, people)));
        content.Add((PackageFormat.Translations, TranslationEntries.Translations(scope, people)));
        return content.Select(item => new PackageEntry(item.Path, CanonicalJson.Write(item.Node))).ToList();
    }

    private static JsonObject Manifest(ProjectWorkspace workspace, PackageOptions options, PackageStamp stamp, IReadOnlyList<PackageEntry> entries)
    {
        var manifest = new JsonObject
        {
            ["format"] = PackageFormat.Name,
            ["formatVersion"] = PackageFormat.Version,
            ["appVersion"] = ApplicationVersion,
            ["schemaVersion"] = PackageFormat.SchemaVersion,
            ["exportedAt"] = CanonicalJson.Date(stamp.ExportedAt),
            ["projectCode"] = workspace.Project.Code,
            ["options"] = new JsonObject { ["withoutPeople"] = options.WithoutPeople },
            ["entries"] = new JsonArray([.. entries.Select(entry => (JsonNode)new JsonObject
            {
                ["path"] = entry.Path,
                ["size"] = entry.Content.LongLength,
                ["sha256"] = Sha256(entry.Content),
            })]),
        };
        if (!options.WithoutPeople && stamp.ExportedBy is { } author)
            manifest["exportedBy"] = author;
        return manifest;
    }
}
