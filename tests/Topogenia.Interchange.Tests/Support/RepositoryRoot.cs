// RepositoryRoot.cs
// The root of the repository seen from the tests: above the test output, or the folder recorded at the build.

using System.Reflection;

namespace Topogenia.Interchange.Tests.Support;

public static class RepositoryRoot
{
    public static string Path { get; } = Find();

    /// <summary>The folder that holds the solution file, above the output or at the root recorded in the assembly metadata.</summary>
    private static string Find()
    {
        var recorded = typeof(RepositoryRoot).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "RepositoryRoot")?.Value;
        foreach (var start in new[] { AppContext.BaseDirectory, recorded })
        {
            for (var folder = start is null ? null : new DirectoryInfo(start); folder is not null; folder = folder.Parent)
            {
                if (folder.GetFiles("*.sln").Length > 0)
                    return folder.FullName;
            }
        }
        throw new InvalidOperationException("The solution file was not found above the test output nor at the recorded root.");
    }
}
