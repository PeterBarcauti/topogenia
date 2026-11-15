// JourneyFolders.cs
// The shell of « Open the folder » for the journeys: it opens no Explorer and writes each folder it is asked to show in a file of the folder of the run.

using Topogenia.Host.Desktop;

namespace Topogenia.EndToEnd;

/// <summary>
/// In the place of the shell of Windows in every run of the journeys (ADR 104): a journey that clicks « Open the folder »
/// opens nothing on the machine. Each folder asked is added to <see cref="ShownFile"/> in the folder of the run, one per
/// line, as it was given to the shell (with its final separator), so that a journey reads what the host asked.
/// </summary>
public sealed class JourneyFolders(string runFolder) : IFolderShell
{
    /// <summary>The file of the folder of the run that holds the folders asked, in their order.</summary>
    public const string ShownFile = "shown-folders.txt";

    private readonly Lock gate = new();

    public FolderOutcome Show(string folder)
    {
        lock (gate)
            File.AppendAllText(Path.Combine(runFolder, ShownFile), folder + "\n");
        return FolderOutcome.Shown;
    }
}
