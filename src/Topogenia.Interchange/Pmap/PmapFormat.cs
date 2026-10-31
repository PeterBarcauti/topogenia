// PmapFormat.cs
// Fixed names and limits of the .pmap format 1.2, which reads the format 1.1 too (annex A 2.17, ADR 054, ADR 085).

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

    /// <summary>The version written (1.2: the entries of the value stream, ADR 085).</summary>
    public const string Version = "1.2";

    /// <summary>The version before, still read (AR-93: no 1.0 file was ever published); a property of 1.2 in it is unknown.</summary>
    public const string PreviousVersion = "1.1";

    /// <summary>The versions read.</summary>
    public static IReadOnlyList<string> Versions { get; } = [PreviousVersion, Version];

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
