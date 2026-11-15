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

    /// <summary>
    /// The current <c>formatVersion</c> (spec 7.2): 2.4 since the comment of presentation of a step or of a flow (3.2, ADR
    /// 099) and the voices a project keeps (3.2, ADR 103), after 2.3 and the summaries of the maps (plan 3.1, lot R) and
    /// Makigami (value classes, media, bounds of the duration of a sheet, action plans of the pain points, photos of the
    /// indicators and source of a target state, ADR 095), 2.2 and the route a user chooses for a flow (ADR 091), 2.1 and
    /// the value stream (ADR 085). It is written when a step or a flow carries a comment or the project a voice, and it
    /// is the last one read.
    /// </summary>
    public const string Version = "2.4";

    /// <summary>
    /// The <c>formatVersion</c> written when no step nor flow carries a comment of presentation and the project no voice
    /// but the content carries a summary or a value of Makigami: the content is then the one of the format 2.3, which the
    /// version 3.1 reads (ADR 099, ADR 103).
    /// </summary>
    public const string VersionWithoutComments = "2.3";

    /// <summary>
    /// The <c>formatVersion</c> written when no map carries a summary but a flow carries a route chosen by the user: the
    /// content is then the one of the format 2.2, which the version 3.1 before the summaries reads (plan 3.1, lot R).
    /// </summary>
    public const string VersionWithoutSummaries = "2.2";

    /// <summary>
    /// The <c>formatVersion</c> written when no flow carries a route chosen by the user: the content is then the one of the
    /// format 2.1, which the version 3.0 of the application reads (ADR 091).
    /// </summary>
    public const string VersionWithoutRoutes = "2.1";

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
