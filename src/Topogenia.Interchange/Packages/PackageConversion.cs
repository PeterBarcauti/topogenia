// PackageConversion.cs
// The chain of converters that brings a package of an earlier format version up to the current one (spec 7.3, ADR 053).

using System.Text.Json.Nodes;

namespace Topogenia.Interchange.Packages;

/// <summary>
/// One link of the chain (spec 7.3): rewrites the content of a package of the format <see cref="From"/>, already checked
/// against the schema of that format, into the content of the format <see cref="To"/>. It changes the entries and the
/// fields of the manifest that the next format changes; the reader then sets <c>formatVersion</c> and
/// <c>schemaVersion</c>, announces the entries again and checks them against the schema of <see cref="To"/>, so that what
/// a converter writes is proven, never trusted.
/// </summary>
public interface IPackageConverter
{
    PackageDefinition From { get; }

    string To { get; }

    /// <summary>Rewrites the manifest and the entries in place; an entry may be added or removed.</summary>
    void Convert(JsonObject manifest, IDictionary<string, JsonObject> content);
}

/// <summary>
/// The converters of the application, one per earlier format, each leading to the next one, the last to the current
/// format. The format 2.0 is the first one; each later format adds the converter from the format before it (ADR 053):
/// 2.0 to 2.1 (ADR 085). A chain is checked when it is built: each earlier format once, none equal to
/// or later than the current one, and every link reaches the current format without a loop.
/// </summary>
public sealed class PackageChain
{
    private readonly Dictionary<string, IPackageConverter> byVersion;

    public PackageChain(IEnumerable<IPackageConverter> converters)
    {
        ArgumentNullException.ThrowIfNull(converters);
        byVersion = new Dictionary<string, IPackageConverter>(StringComparer.Ordinal);
        var current = Version.Parse(PackageFormat.Version);
        foreach (var converter in converters)
        {
            if (Version.Parse(converter.From.Version) >= current)
                throw new ArgumentException($"The converter from {converter.From.Version} does not start at an earlier format.", nameof(converters));
            if (!byVersion.TryAdd(converter.From.Version, converter))
                throw new ArgumentException($"Two converters start at the format {converter.From.Version}.", nameof(converters));
        }
        foreach (var version in byVersion.Keys)
            _ = Links(version) ?? throw new ArgumentException($"The converter from {version} does not reach the format {PackageFormat.Version}.", nameof(converters));
        KnownEntries = [.. PackageFormat.Entries.Concat(byVersion.Values.SelectMany(converter => converter.From.Entries)).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>The chain of the application: 2.0 to 2.1.</summary>
    public static PackageChain Application { get; } = new([new Package20To21()]);

    /// <summary>The names of content entries that a package of the current format or of a format of the chain may hold.</summary>
    public IReadOnlyList<string> KnownEntries { get; }

    /// <summary>The converters that bring <paramref name="version"/> to the current format, in order; null when none does.</summary>
    internal IReadOnlyList<IPackageConverter>? Links(string version)
    {
        var links = new List<IPackageConverter>();
        while (!string.Equals(version, PackageFormat.Version, StringComparison.Ordinal))
        {
            if (!byVersion.TryGetValue(version, out var converter) || links.Contains(converter))
                return null;
            links.Add(converter);
            version = converter.To;
        }
        return links;
    }

    /// <summary>The definition a format is checked against before its conversion: its own, or the current one.</summary>
    internal PackageDefinition DefinitionOf(string version) =>
        byVersion.TryGetValue(version, out var converter) ? converter.From : PackageDefinition.Current;
}
