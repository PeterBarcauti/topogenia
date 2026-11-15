// UsesSystemData.cs
// Deliberately faulty type that uses System.Data, System.IO and the console, so that the forbidden dependency rule is proven to fire.

namespace Topogenia.Architecture.Tests.Probes;

/// <summary>Probe for <c>DependencyRulesTests</c>: never used by production code.</summary>
public static class UsesSystemData
{
    public static System.Data.IsolationLevel Level => System.Data.IsolationLevel.Snapshot;

    public static bool Exists(string path) => System.IO.File.Exists(path);

    public static void Write(string text) => System.Console.WriteLine(text);
}
