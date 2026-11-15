// PmapFormat.cs
// Fixed names and limits of the .pmap format 1.3, which reads the formats 1.1 and 1.2 too (annex A 2.17, ADR 054, ADR 085, ADR 091).

using Topogenia.Domain.Projects;
using Topogenia.Domain.Structure;

namespace Topogenia.Interchange.Pmap;

/// <summary>
/// The names and limits of the `.pmap` format. The marker <see cref="Name"/> derives from the root identifier of the
/// solution, like the names of the package (ADR 013, 051): renaming the displayed product does not make the files
/// already written unreadable.
/// </summary>
public static class PmapFormat
{
    /// <summary>The <c>format</c> of a file.</summary>
    public static string Name { get; } = nameof(Topogenia).ToLowerInvariant();

    /// <summary>
    /// The current version (1.4: the summary of a map, ADR 094, and the properties of the Makigami, ADR 095: value class of a
    /// step, medium of a document, bounds of the duration of a sheet, action plan of a pain point), written when a map
    /// carries a summary or an element one of these properties.
    /// </summary>
    public const string Version = "1.4";

    /// <summary>
    /// The version written when no map carries a summary nor any element a property of the Makigami, but a flow carries a
    /// route chosen by the user (1.3, ADR 091): its content is then the one of the 1.3, which the version 3.1 before the
    /// summaries reads.
    /// </summary>
    public const string VersionWithoutSummaries = "1.3";

    /// <summary>
    /// The version written when no flow carries a route chosen by the user (1.2: the entries of the value stream, ADR 085):
    /// its content is then the one of the 1.2, which the version 3.0 of the application reads (ADR 091).
    /// </summary>
    public const string VersionWithoutRoutes = "1.2";

    /// <summary>The version before the value stream, still read (AR-93: no 1.0 file was ever published); a property of 1.2 in it is unknown.</summary>
    public const string PreviousVersion = "1.1";

    /// <summary>The versions read.</summary>
    public static IReadOnlyList<string> Versions { get; } = [PreviousVersion, VersionWithoutRoutes, VersionWithoutSummaries, Version];

    public const string Extension = ".pmap";

    public const int StepsMax = 5000;
    public const int LanesMax = 200;
    public const int DepthMax = 12;
    public const int ErrorsMax = 30;
    public const int IdMax = 64;
    public const int UncertaintiesKept = 50;
    public const int UncertaintyMax = 500;
    // The lengths of the texts are those of the domain, a file writing nothing that the interface could not (A-2.18-08).
    public const int ProjectNameMax = StructureEdits.ProjectNameMax;
    public const int DescriptionMax = ProjectCreation.DescriptionMax;
    public const int LaneNameMax = StructureEdits.LaneNameMax;
    public const int StepLabelMax = StructureEdits.StepLabelMax;
    public const int FlowLabelMax = StructureEdits.FlowLabelMax;

    /// <summary>The deepest JSON read (A-4-24).</summary>
    public const int JsonDepthMax = 256;
}
