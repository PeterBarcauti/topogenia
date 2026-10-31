// PackageDefinition.cs
// One version of the package format: its number, its entries and its JSON schema, which a package of that version follows.

using System.Text.Json;
using Topogenia.Interchange.Schemas;

namespace Topogenia.Interchange.Packages;

/// <summary>
/// The shape of a version of the format (ADR 053): the <c>formatVersion</c>, the content entries in the order of the
/// archive (members.json, the people, being the optional one), and the schema published for it, whose root describes the
/// manifest and whose definition named after each entry describes that entry. A later format keeps the definition of
/// each earlier one, so that its packages are checked against their own schema before they are converted.
/// </summary>
public sealed class PackageDefinition
{
    private readonly SchemaValidator schema;

    public PackageDefinition(string version, IReadOnlyList<string> entries, SchemaValidator schema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(schema);
        if (!System.Version.TryParse(version, out _))
            throw new ArgumentException($"{version} is not a version number.", nameof(version));
        if (entries.Contains(PackageFormat.Manifest, StringComparer.Ordinal) || entries.Distinct(StringComparer.Ordinal).Count() != entries.Count)
            throw new ArgumentException("The entries are the content entries, each once, the manifest left out.", nameof(entries));
        Version = version;
        Entries = entries;
        this.schema = schema;
    }

    /// <summary>The format written and read by this application: <see cref="PackageFormat"/> and the schema embedded with it.</summary>
    public static PackageDefinition Current { get; } = new(PackageFormat.Version, PackageFormat.Entries, PackageSchema.Validator);

    public string Version { get; }

    public IReadOnlyList<string> Entries { get; }

    /// <summary>The errors of an entry of this version against its definition, the manifest against the root of the schema.</summary>
    public IReadOnlyList<SchemaError> Validate(string path, JsonElement content)
    {
        if (path == PackageFormat.Manifest)
            return schema.Validate(content);
        if (!Entries.Contains(path, StringComparer.Ordinal))
            throw new ArgumentException($"{path} is not an entry of the format {Version}.", nameof(path));
        return schema.Validate(content, path[..^".json".Length]);
    }
}
