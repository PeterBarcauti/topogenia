// MigrationSources.cs
// Sources of the migrations of a provider, embedded in its assembly, and their fingerprints (spec 4.4: never changed once published).

using System.Reflection;

namespace Topogenia.Persistence.Schema;

/// <summary>
/// Each provider embeds the source of its migrations (`Migrations/&lt;identifier&gt;.cs`, the Designer and snapshot files
/// aside) as resources named `Migrations/&lt;identifier&gt;.cs`. The fingerprint of a migration is the one of that source:
/// a published migration changed afterwards no longer matches the fingerprint recorded when it was applied (1.x rule of
/// the scripts, spec 4.4).
/// </summary>
public sealed class MigrationSources
{
    /// <summary>Prefix of the resource names of the migration sources.</summary>
    public const string ResourcePrefix = "Migrations/";

    private readonly Assembly assembly;

    public MigrationSources(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        this.assembly = assembly;
    }

    /// <summary>The identifiers of the migrations whose source is embedded.</summary>
    public IReadOnlyList<string> Embedded =>
        assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(".cs", StringComparison.Ordinal))
            .Select(name => name[ResourcePrefix.Length..^".cs".Length])
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>The fingerprint of the source of a migration; a migration without its source is a packaging error.</summary>
    public string FingerprintOf(string migrationId)
    {
        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + migrationId + ".cs")
            ?? throw new InvalidOperationException($"The source of the migration {migrationId} is not embedded in {assembly.GetName().Name}.");
        using var reader = new StreamReader(stream);
        return Fingerprints.Of(reader.ReadToEnd());
    }
}
