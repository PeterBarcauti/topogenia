// PackageReader.cs
// Opens a received package with every guard of spec 5.4, before anything is written: archive, names, sizes, hashes, schema.

using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Topogenia.Domain.Errors;
using Topogenia.Interchange.Json;

namespace Topogenia.Interchange.Packages;

/// <summary>The entries of a package that passed the guards: the manifest and each content entry, parsed.</summary>
internal sealed record PackageEntries(JsonObject Manifest, IReadOnlyDictionary<string, JsonObject> Content)
{
    public bool WithPeople => Content.ContainsKey(PackageFormat.Members);
}

/// <summary>
/// The limits of a received package (spec 5.4, ADR 052): entries, manifest, each entry and the whole, uncompressed. The
/// application keeps <see cref="Default"/>; a test gives smaller ones to reach a limit without writing hundreds of megabytes.
/// </summary>
public sealed record PackageLimits(int MaxEntries, long Manifest, long Entry, long Total)
{
    public static PackageLimits Default { get; } = new(8, 1024 * 1024, 200L * 1024 * 1024, 500L * 1024 * 1024);
}

/// <summary>
/// The guards of a received package, in the order of ADR 052 (section 3), the size of the body being the host's: a ZIP
/// archive of a limited number of entries, each of a known name; the manifest first, 1 MB at most, of this format and version,
/// following the schema; each entry announced and each announced entry present; the uncompressed size counted while it
/// is read, never beyond what the manifest announces nor the limits (<see cref="PackageLimits"/>); the SHA-256 of the manifest;
/// each entry following its definition of the schema. A package of an earlier format follows the schema of its own
/// format, then goes through the chain of converters (ADR 053), each output checked against the schema of the next format.
/// Every refusal is PAQUET_INVALIDE with its reason and entry, or PAQUET_TROP_RECENT, and nothing is read beyond the first
/// fault.
/// </summary>
internal static class PackageReader
{
    public static PackageEntries Read(byte[] package, PackageLimits limits, PackageChain chain)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(chain);
        using var archive = Open(package);
        var entries = Entries(archive, limits.MaxEntries, chain.KnownEntries);
        var total = new Total(limits.Total);
        var manifestBytes = Content(entries[PackageFormat.Manifest], limits.Manifest, total);
        var manifest = Parse(PackageFormat.Manifest, manifestBytes);
        var (definition, links) = EnsureFormat(manifest, chain);
        Validate(definition, PackageFormat.Manifest, manifestBytes);
        var announced = Announced(manifest);
        EnsureSameEntries(entries.Keys, announced.Keys, manifest, definition);
        var content = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var path in definition.Entries.Where(announced.ContainsKey))
        {
            var (size, sha256) = announced[path];
            var bytes = Content(entries[path], Math.Min(size, limits.Entry), total, path);
            if (bytes.LongLength != size)
                throw Invalid("size", path);
            if (!string.Equals(PackageWriter.Sha256(bytes), sha256, StringComparison.Ordinal))
                throw Invalid("hash", path);
            content[path] = Parse(path, bytes);
            Validate(definition, path, bytes);
        }
        foreach (var link in links)
            Convert(link, manifest, content, chain.DefinitionOf(link.To));
        return new PackageEntries(manifest, content);
    }

    /// <summary>
    /// One link of the chain: the converter rewrites the content, then the manifest takes the next version, the entries
    /// are announced again (size and SHA-256 of their canonical writing, in the order of the next format) and everything
    /// is checked against the schema of the next format, as a package written in it would be.
    /// </summary>
    private static void Convert(IPackageConverter link, JsonObject manifest, Dictionary<string, JsonObject> content, PackageDefinition next)
    {
        link.Convert(manifest, content);
        manifest["formatVersion"] = next.Version;
        manifest["schemaVersion"] = next.Version;
        if (content.Keys.FirstOrDefault(path => !next.Entries.Contains(path, StringComparer.Ordinal)) is { } unknown)
            throw Invalid("entries", unknown);
        var written = next.Entries.Where(content.ContainsKey).ToDictionary(path => path, path => CanonicalJson.Write(content[path]), StringComparer.Ordinal);
        manifest["entries"] = new JsonArray([.. written.Select(pair => (JsonNode)new JsonObject
        {
            ["path"] = pair.Key,
            ["size"] = pair.Value.LongLength,
            ["sha256"] = PackageWriter.Sha256(pair.Value),
        })]);
        Validate(next, PackageFormat.Manifest, CanonicalJson.Write(manifest));
        EnsureSameEntries(written.Keys, written.Keys, manifest, next);
        foreach (var (path, bytes) in written)
            Validate(next, path, bytes);
    }

    /// <summary>PAQUET_INVALIDE with its reason and, when there is one, the entry at fault.</summary>
    public static DomainException Invalid(string reason, string? entry = null, IReadOnlyList<string>? paths = null)
    {
        var details = new Dictionary<string, object?> { ["reason"] = reason };
        if (entry is not null)
            details["entry"] = entry;
        if (paths is not null)
            details["paths"] = paths;
        return new DomainException(ErrorCodes.InvalidPackage, RefusalKind.Invalid, details);
    }

    private static ZipArchive Open(byte[] package)
    {
        try
        {
            return new ZipArchive(new MemoryStream(package, writable: false), ZipArchiveMode.Read);
        }
        catch (InvalidDataException)
        {
            throw Invalid("archive");
        }
    }

    /// <summary>
    /// The entries by name: eight at most, each a known name once (of the current format or of a format of the chain),
    /// never a path that leaves the archive.
    /// </summary>
    private static Dictionary<string, ZipArchiveEntry> Entries(ZipArchive archive, int maxEntries, IReadOnlyList<string> known)
    {
        IReadOnlyList<ZipArchiveEntry> all;
        try
        {
            all = archive.Entries;
        }
        catch (InvalidDataException)
        {
            throw Invalid("archive");
        }
        if (all.Count > maxEntries)
            throw Invalid("entries");
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        foreach (var entry in all)
        {
            var name = entry.FullName;
            if (IsDangerous(name))
                throw Invalid("path", name);
            if ((name != PackageFormat.Manifest && !known.Contains(name, StringComparer.Ordinal)) || !entries.TryAdd(name, entry))
                throw Invalid("entries", name);
        }
        return entries.ContainsKey(PackageFormat.Manifest) ? entries : throw Invalid("entries", PackageFormat.Manifest);
    }

    /// <summary>A path that climbs (`..`), starts at a root (`/`, `\`) or names a drive (`C:`).</summary>
    internal static bool IsDangerous(string name) =>
        name.Length == 0 || name[0] is '/' or '\\' || name.Contains(':', StringComparison.Ordinal)
        || name.Replace('\\', '/').Split('/').Any(part => part == "..");

    /// <summary>The bytes of an entry, read in a stream that stops at the first byte beyond its limit or beyond the total.</summary>
    private static byte[] Content(ZipArchiveEntry entry, long limit, Total total, string? path = null)
    {
        var name = path ?? entry.FullName;
        try
        {
            using var stream = entry.Open();
            using var copy = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                total.Read += read;
                if (copy.Length + read > limit || total.Read > total.Limit)
                    throw Invalid("size", name);
                copy.Write(buffer, 0, read);
            }
            return copy.ToArray();
        }
        catch (InvalidDataException)
        {
            throw Invalid("archive", name);
        }
    }

    /// <summary>The uncompressed bytes read so far from the whole archive, against its limit.</summary>
    private sealed class Total(long limit)
    {
        public long Limit { get; } = limit;

        public long Read { get; set; }
    }

    private static JsonObject Parse(string path, byte[] bytes)
    {
        try
        {
            // A key written twice in an object is not JSON that the package can mean (the second one would be lost), nor
            // a text that does not read as one (half of a surrogate pair). The search of the keys written twice reads
            // each name while it parses: a name that does not read fails there, before the texts are read.
            var options = new JsonDocumentOptions { AllowDuplicateProperties = false };
            using (var document = JsonDocument.Parse(bytes, options))
            {
                if (!JsonText.Reads(document.RootElement))
                    throw Invalid("json", path);
            }
            return JsonNode.Parse(bytes, null, options) as JsonObject ?? throw Invalid("schema", path);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            throw Invalid("json", path);
        }
    }

    private static void Validate(PackageDefinition definition, string path, byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        var errors = definition.Validate(path, document.RootElement);
        if (errors.Count > 0)
            throw Invalid("schema", path, [.. errors.Take(10).Select(error => error.Path)]);
    }

    /// <summary>
    /// The format first, before the schema of the manifest: a package of a later version may well not follow the
    /// schema of this one, and must be told so (spec 7.3), not that it is invalid. Gives the definition the package
    /// follows and the links that convert it, none for the current format; an earlier version that no link converts is
    /// refused.
    /// </summary>
    private static (PackageDefinition Definition, IReadOnlyList<IPackageConverter> Links) EnsureFormat(JsonObject manifest, PackageChain chain)
    {
        var format = manifest["format"] is JsonValue name && name.TryGetValue<string>(out var text) ? text : null;
        if (!string.Equals(format, PackageFormat.Name, StringComparison.Ordinal))
            throw Invalid("format", PackageFormat.Manifest);
        var version = manifest["formatVersion"] is JsonValue value && value.TryGetValue<string>(out var given) ? given : null;
        if (version is not null && chain.Links(version) is { } links)
            return (chain.DefinitionOf(version), links);
        if (Version.TryParse(version, out var parsed) && parsed > Version.Parse(PackageFormat.Version))
            throw new DomainException(ErrorCodes.PackageTooRecent, RefusalKind.Invalid, new Dictionary<string, object?>
            {
                ["version"] = version,
                ["supported"] = PackageFormat.Version,
            });
        throw Invalid("format", PackageFormat.Manifest);
    }

    /// <summary>The size and SHA-256 that the manifest announces for each entry (the schema forbids a duplicate path).</summary>
    private static Dictionary<string, (long Size, string Sha256)> Announced(JsonObject manifest)
    {
        var announced = new Dictionary<string, (long, string)>(StringComparer.Ordinal);
        foreach (var item in manifest["entries"]!.AsArray().OfType<JsonObject>())
        {
            var path = item["path"]!.GetValue<string>();
            if (!announced.TryAdd(path, (item["size"]!.GetValue<long>(), item["sha256"]!.GetValue<string>())))
                throw Invalid("entries", path);
        }
        return announced;
    }

    /// <summary>
    /// The archive holds the manifest and exactly the announced entries of its format, every one but members.json being
    /// required; members.json is there exactly when the export kept the people.
    /// </summary>
    private static void EnsureSameEntries(IEnumerable<string> present, IEnumerable<string> announced, JsonObject manifest,
        PackageDefinition definition)
    {
        var inArchive = present.Where(name => name != PackageFormat.Manifest).ToHashSet(StringComparer.Ordinal);
        var named = announced.ToHashSet(StringComparer.Ordinal);
        foreach (var path in definition.Entries)
        {
            var required = path != PackageFormat.Members;
            if (inArchive.Contains(path) != named.Contains(path) || (required && !named.Contains(path)))
                throw Invalid("entries", path);
        }
        if (inArchive.Concat(named).Except(definition.Entries, StringComparer.Ordinal).FirstOrDefault() is { } unknown)
            throw Invalid("entries", unknown);
        var withoutPeople = manifest["options"]?["withoutPeople"]?.GetValue<bool>() ?? true;
        if (withoutPeople == named.Contains(PackageFormat.Members))
            throw Invalid("entries", PackageFormat.Members);
    }
}
