// RepositoryFiles.cs
// Locates the repository root and enumerates the source files that the architecture rules inspect.

namespace Topogenia.Architecture.Tests;

internal static class RepositoryFiles
{
    /// <summary>Build output and tool caches, excluded at any depth (exact, case-sensitive names).</summary>
    private static readonly string[] ExcludedDirectories =
        ["bin", "obj", "node_modules", "test-results", "playwright-report", "TestResults", ".git", ".vs"];

    /// <summary>Generated or tool-owned folders, excluded only at these exact places.</summary>
    private static readonly string[] GeneratedFolders =
    [
        "src/Topogenia.Host/wwwroot/",
        "web/src/api/generated/",
        "docs/recette/maquette/captures/",
    ];

    public static string Root { get; } = FindRoot();

    /// <summary>
    /// The repository root: above the test output, or, when the output is elsewhere (--artifacts-path), the folder
    /// recorded at build time in the assembly metadata.
    /// </summary>
    private static string FindRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, RecordedRoot() })
        {
            var directory = start is null ? null : new DirectoryInfo(start);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Topogenia.sln")))
                directory = directory.Parent;
            if (directory is not null)
                return directory.FullName;
        }
        throw new InvalidOperationException("Topogenia.sln not found above the test output nor at the recorded root.");
    }

    private static string? RecordedRoot() =>
        typeof(RepositoryFiles).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "RepositoryRoot")?.Value;

    /// <summary>Files under the given folders (recursively) whose extension is in the list, outside build output.</summary>
    public static IEnumerable<string> Enumerate(IEnumerable<string> folders, params string[] extensions) =>
        folders
            .Select(folder => Path.Combine(Root, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            .Where(file => Matches(file, extensions))
            .Where(file => !IsExcluded(file))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

    /// <summary>Files directly in the given folders (not recursive).</summary>
    public static IEnumerable<string> Shallow(IEnumerable<string> folders, params string[] extensions) =>
        folders
            .Select(folder => Path.Combine(Root, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder))
            .Where(file => Matches(file, extensions))
            .Order(StringComparer.Ordinal);

    private static bool Matches(string file, string[] extensions)
    {
        var name = Path.GetFileName(file);
        return extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)
            || extensions.Contains(name, StringComparer.Ordinal);
    }

    /// <summary>C# files of the production code (src), generated files excluded.</summary>
    public static IEnumerable<string> ProductionCSharp() => Enumerate(["src"], ".cs").Where(file => !IsGenerated(file));

    /// <summary>Every C# file of the solution (src and tests), generated files excluded.</summary>
    public static IEnumerable<string> AllCSharp() => Enumerate(["src", "tests"], ".cs").Where(file => !IsGenerated(file));

    /// <summary>The folders of the migrations of the two providers.</summary>
    private static readonly string[] MigrationFolders = ["src/Topogenia.Persistence.SqlServer/Migrations/", "src/Topogenia.Persistence.Sqlite/Migrations/"];

    /// <summary>
    /// A file that a tool wrote and nobody changes: the tool-owned folders; in the migrations of a provider, a designer file
    /// (`*.Designer.cs`) or the snapshot of the model (`AppDbContextModelSnapshot.cs`) that starts with the header of a tool
    /// (`// &lt;auto-generated`); and a migration that `dotnet ef` wrote whole (it holds no SQL written by hand,
    /// `migrationBuilder.Sql`). Nothing else: a `*.g.cs` or a `*.Designer.cs` elsewhere, or one without that header, is a
    /// file written by hand and follows every rule. A migration with SQL written by hand is not generated: it follows the
    /// rules of the form (header, markers, comments in English), see <see cref="IsMigration"/> for the two rules it is
    /// spared (ADR 022, ADR 037).
    /// </summary>
    public static bool IsGenerated(string file)
    {
        var relative = Relative(file);
        return IsGenerated(relative, IsMigrationPath(relative) ? File.ReadAllText(file) : "");
    }

    /// <summary><see cref="IsGenerated(string)"/> on a path relative to the root and the text of the file.</summary>
    public static bool IsGenerated(string relative, string text)
    {
        ArgumentNullException.ThrowIfNull(relative);
        ArgumentNullException.ThrowIfNull(text);
        if (GeneratedFolders.Any(folder => relative.StartsWith(folder, StringComparison.Ordinal)))
            return true;
        if (!IsMigrationPath(relative))
            return false;
        return relative.EndsWith(".Designer.cs", StringComparison.Ordinal) || relative.EndsWith("/AppDbContextModelSnapshot.cs", StringComparison.Ordinal)
            ? text.StartsWith(ToolHeader, StringComparison.Ordinal)
            : !WritesSqlByHand(text);
    }

    /// <summary>The first line that `dotnet ef` writes in a designer file and in the snapshot of the model.</summary>
    private const string ToolHeader = "// <auto-generated";

    /// <summary>
    /// A file of the migrations of a provider. A migration may mix what `dotnet ef` generated from the model (one method
    /// per direction, as long as the change of the model) with SQL written by hand; it is never changed once published
    /// (spec 4.4) and freezes the names of the database (ADR 013, ADR 032 point 7). It is therefore spared the limits of
    /// size of a file and of a method and the rule of the product name, and only those (ADR 037).
    /// </summary>
    public static bool IsMigration(string file) => IsMigrationPath(Relative(file));

    private static bool IsMigrationPath(string relative) => MigrationFolders.Any(folder => relative.StartsWith(folder, StringComparison.Ordinal));

    /// <summary>A migration that holds SQL written by hand.</summary>
    public static bool WritesSqlByHand(string migration) => migration.Contains("migrationBuilder.Sql(", StringComparison.Ordinal);

    public static string Relative(string file) => Path.GetRelativePath(Root, file).Replace('\\', '/');

    private static bool IsExcluded(string file) =>
        Relative(file).Split('/').Any(part => ExcludedDirectories.Contains(part, StringComparer.Ordinal));
}
