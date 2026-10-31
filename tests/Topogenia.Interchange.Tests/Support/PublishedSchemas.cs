// PublishedSchemas.cs
// The name of the published schema of the .pmap format, under schemas/ at the root of the repository (ADR 054).

using Topogenia.Interchange.Pmap;

namespace Topogenia.Interchange.Tests.Support;

/// <summary>
/// The schema of the `.pmap` is published for the people and the tools that write a file (`docs/format-pmap.md`); the
/// application never reads it, its reader checks a file by its own rules. The tests read it where it lives.
/// </summary>
public static class PublishedSchemas
{
    /// <summary>The file of the schema of the version written, `pmap-1.2.schema.json` (the 1.1 one stays published beside it).</summary>
    public static string PmapFile { get; } = "pmap-" + PmapFormat.Version + ".schema.json";
}
