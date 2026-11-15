// Package22To23.cs
// The link of the chain from the format 2.2 to the format 2.3: nothing to rewrite, the 2.3 only adds optional values (summaries, Makigami).

using System.Text.Json.Nodes;
using Topogenia.Interchange.Schemas;

namespace Topogenia.Interchange.Packages;

/// <summary>
/// The format 2.3 adds optional values only: the summary of a map and the translations of a summary (plan 3.1, lot R),
/// the value class of a step and the medium of a document (Makigami M1, M3), the photos of the indicators of the project
/// and the code of the source of a target state (Makigami M6, M8, ADR 095). A package 2.2 has none, so that its content is
/// already a content of the format 2.3; it is checked against its own schema first, as every earlier format.
/// </summary>
public sealed class Package22To23 : IPackageConverter
{
    public const string Version = "2.2";

    /// <summary>The format 2.2: its entries, which the 2.3 keeps, and its own schema, kept embedded.</summary>
    public PackageDefinition From { get; } = new(Version, PackageFormat.Entries, PackageSchema.For(Version));

    public string To => Package23To24.Version;

    public void Convert(JsonObject manifest, IDictionary<string, JsonObject> content)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(content);
    }
}
