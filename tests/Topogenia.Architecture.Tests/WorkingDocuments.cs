// WorkingDocuments.cs
// Whether the working documents of the holder are in this repository; the rule that reads the coverage is skipped where they are not (ADR 074, revision of 11/11/2026).

namespace Topogenia.Architecture.Tests;

internal static class WorkingDocuments
{
    /// <summary>
    /// True in the repository of the holder, which keeps the coverage and the journal of the construction; false in the
    /// public repository, which receives the code, the decisions, the formats and the guides only.
    /// </summary>
    public static bool ArePresent { get; } = File.Exists(Path.Combine(RepositoryFiles.Root, "docs", "couverture.md"));

    public const string Elsewhere =
        "The working documents are not in this repository: this rule runs in the repository of the holder (ADR 074)";

    /// <summary>Skips a rule that reads a working document where they are not; nowhere else.</summary>
    public static void Require() => Assert.SkipUnless(ArePresent, Elsewhere);
}
