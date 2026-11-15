// Package23To24.cs
// The link of the chain from the format 2.3 to the format 2.4: nothing to rewrite, the 2.4 only adds the optional comment of presentation of a step or of a flow and the optional voices of the project.

using System.Text.Json.Nodes;
using Topogenia.Interchange.Schemas;

namespace Topogenia.Interchange.Packages;

/// <summary>
/// The format 2.4 adds optional values only: the comment of presentation of a step or of a flow (3.2, ADR 099) and the
/// voices a project keeps (3.2, ADR 103). A package 2.3 has none, so that its content is already a content of the format
/// 2.4; it is checked against its own schema first, as every earlier format.
/// </summary>
public sealed class Package23To24 : IPackageConverter
{
    public const string Version = "2.3";

    /// <summary>The format 2.3: its entries, which the 2.4 keeps, and its own schema, kept embedded.</summary>
    public PackageDefinition From { get; } = new(Version, PackageFormat.Entries, PackageSchema.For(Version));

    public string To => PackageFormat.Version;

    public void Convert(JsonObject manifest, IDictionary<string, JsonObject> content)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(content);
    }
}
