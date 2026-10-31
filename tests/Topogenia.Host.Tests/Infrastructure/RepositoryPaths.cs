// RepositoryPaths.cs
// The files of the repository seen from the tests of the host: the samples and the documents, above the output of the tests.

using System.Reflection;

namespace Topogenia.Host.Tests.Infrastructure;

public static class RepositoryPaths
{
    /// <summary>The folder that holds the solution file, above the output of the tests or at the root recorded at the build.</summary>
    public static string Root { get; } = Find();

    public static string Of(params string[] path) => Path.Combine([Root, .. path]);

    /// <summary>The text of a file of the repository, its line ends written LF.</summary>
    public static string Text(params string[] path) => File.ReadAllText(Of(path)).ReplaceLineEndings("\n");

    private static string Find()
    {
        var recorded = typeof(RepositoryPaths).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "RepositoryRoot")?.Value;
        foreach (var start in new[] { AppContext.BaseDirectory, recorded })
        {
            for (var folder = start is null ? null : new DirectoryInfo(start); folder is not null; folder = folder.Parent)
            {
                if (folder.GetFiles("*.sln").Length > 0)
                    return folder.FullName;
            }
        }
        throw new InvalidOperationException("The solution file was not found above the output of the tests nor at the recorded root.");
    }
}
