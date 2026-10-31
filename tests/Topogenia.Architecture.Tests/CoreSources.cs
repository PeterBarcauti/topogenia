// CoreSources.cs
// Whether the sources of the closed core are in this repository; the rules that read them, or read its compiled assembly by the names of its generated code, are skipped where they are not (ADR 048, ADR 074).

namespace Topogenia.Architecture.Tests;

internal static class CoreSources
{
    /// <summary>
    /// True in the repository of the holder, which has the domain and its tests; false in the public repository, where
    /// the domain is the obfuscated assembly of its package and the rules of its sources run before it is packed.
    /// </summary>
    public static bool ArePresent { get; } = Directory.Exists(Path.Combine(RepositoryFiles.Root, "src", "Topogenia.Domain"));

    public const string Elsewhere =
        "The sources of the core are not in this repository: this rule runs in the repository of the holder before the core is packed (ADR 074)";

    /// <summary>Skips a rule of the sources of the core where they are not; nowhere else.</summary>
    public static void Require() => Assert.SkipUnless(ArePresent, Elsewhere);
}
