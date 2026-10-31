// PackageFormat.cs
// Fixed names of the exchange package (spec 7.2, ADR 051): format, version, extension, entries and their order.

namespace Topogenia.Interchange.Packages;

/// <summary>
/// The technical names of the exchange package. Like the namespaces and the SQL Server schema (ADR 013), they derive
/// from the root identifier of the solution and not from the displayed product name: renaming the product must not make
/// the packages already written unreadable (ADR 051).
/// </summary>
public static class PackageFormat
{
    private static readonly string Root = nameof(Topogenia).ToLowerInvariant();

    /// <summary>The <c>format</c> of the manifest.</summary>
    public static string Name { get; } = Root + "-package";

    /// <summary>The <c>formatVersion</c> written by this application (spec 7.2): 2.1 since the value stream (ADR 085).</summary>
    public const string Version = "2.1";

    /// <summary>
    /// The <c>schemaVersion</c>: the version of the published JSON schema that the entries follow, the one of the
    /// schema file named after it (ADR 051).
    /// </summary>
    public const string SchemaVersion = Version;

    /// <summary>The extension of a package file, with its dot.</summary>
    public static string Extension { get; } = "." + Root;

    /// <summary>The name of the published schema file, under <c>schemas/</c> at the root of the repository.</summary>
    public static string SchemaFile { get; } = Name + "-" + SchemaVersion + ".schema.json";

    public const string Manifest = "manifest.json";
    public const string Project = "project.json";
    public const string Members = "members.json";
    public const string Maps = "maps.json";
    public const string Sheets = "sheets.json";
    public const string Documents = "documents.json";
    public const string PainPoints = "painpoints.json";
    public const string Translations = "translations.json";

    /// <summary>The content entries in the order of the archive, after the manifest; members.json is optional.</summary>
    public static IReadOnlyList<string> Entries { get; } = [Project, Members, Maps, Sheets, Documents, PainPoints, Translations];
}
