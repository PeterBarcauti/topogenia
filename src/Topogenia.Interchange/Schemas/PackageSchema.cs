// PackageSchema.cs
// The published JSON schemas of the exchange package, embedded in the assembly and loaded once per format version.

using System.Text.Json;
using Topogenia.Interchange.Packages;

namespace Topogenia.Interchange.Schemas;

/// <summary>
/// The schema of <c>schemas/</c> (spec 7.2), embedded at the build: the root describes the manifest and each other entry
/// is described by the definition of <c>$defs</c> named after its file (project.json by <c>project</c>...).
/// <see cref="PackageDefinition.Current"/> holds it and checks the entries of a package against it.
/// </summary>
public static class PackageSchema
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<SchemaValidator>> Loaded = new(StringComparer.Ordinal);

    /// <summary>The validator of the schema of the current format, which <see cref="PackageDefinition.Current"/> holds.</summary>
    internal static SchemaValidator Validator => For(PackageFormat.SchemaVersion);

    /// <summary>
    /// The validator of the schema of a version, <c>schemas/&lt;name&gt;-&lt;version&gt;.schema.json</c>, embedded at the
    /// build: an earlier format keeps its schema so that its packages are checked against it before their conversion.
    /// </summary>
    internal static SchemaValidator For(string version) => Loaded.GetOrAdd(version, key => new Lazy<SchemaValidator>(() =>
    {
        var file = PackageFormat.Name + "-" + key + ".schema.json";
        using var stream = typeof(PackageSchema).Assembly.GetManifestResourceStream("Schemas/" + file)
            ?? throw new InvalidOperationException($"The schema {file} of the package is not embedded in the assembly.");
        using var document = JsonDocument.Parse(stream);
        return new SchemaValidator(document.RootElement);
    })).Value;
}
