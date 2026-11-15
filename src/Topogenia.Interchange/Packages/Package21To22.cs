// Package21To22.cs
// The link of the chain from the format 2.1 to the format 2.2: nothing to rewrite, the 2.2 only adds the route a user chooses for a flow (ADR 091).

using System.Text.Json.Nodes;
using Topogenia.Interchange.Schemas;

namespace Topogenia.Interchange.Packages;

/// <summary>
/// The format 2.2 adds the optional route of a flow (ADR 091): its sides and its passage. A package 2.1 has none, so that
/// its content is already a content of the format 2.2; it is checked against its own schema first, as every earlier format.
/// </summary>
public sealed class Package21To22 : IPackageConverter
{
    public const string Version = "2.1";

    /// <summary>The format 2.1: its entries, which the 2.2 keeps, and its own schema, kept embedded.</summary>
    public PackageDefinition From { get; } = new(Version, PackageFormat.Entries, PackageSchema.For(Version));

    public string To => Package22To23.Version;

    public void Convert(JsonObject manifest, IDictionary<string, JsonObject> content)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(content);
    }
}
