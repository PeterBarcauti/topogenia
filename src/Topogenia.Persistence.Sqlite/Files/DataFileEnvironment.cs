// DataFileEnvironment.cs
// What the check of the data file needs to know of the machine: its synchronised folders, its network drives, its links and hard links (spec 5.2).

namespace Topogenia.Persistence.Sqlite.Files;

/// <summary>
/// The synchronised folders of the user (absolute paths), the test of a drive letter mapped to the network, the reader
/// of the links of the local disks (<see cref="LinkTargetOf"/>) and of the names of a file (<see cref="LinkCountOf"/>). The tests give fictitious values; the host builds it
/// from the machine with <see cref="Current"/>, and may add the synchronised folders it finds elsewhere (the libraries of
/// SharePoint mounted by the OneDrive client, P4-T05).
/// </summary>
public sealed record DataFileEnvironment(IReadOnlyList<string> SynchronisedFolders, Func<char, bool> IsNetworkDrive)
{
    /// <summary>
    /// The target written in the link (symbolic link or junction) at a local path, absolute or relative to the folder of
    /// the link; null when the path is no link or does not exist. By default <see cref="FileSystemLinkTarget"/>; the tests
    /// give fictitious links, a symbolic link to a share among them.
    /// </summary>
    public Func<string, string?> LinkTargetOf { get; init; } = FileSystemLinkTarget;

    /// <summary>
    /// The target of a link of the local disk, read in the link itself: the attributes of the path, then its reparse
    /// data, never the target (a link to a share is never followed on the network). A placeholder of OneDrive, a reparse
    /// point that is no link, has no target.
    /// </summary>
    public static string? FileSystemLinkTarget(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            var attributes = File.GetAttributes(path);
            if (!attributes.HasFlag(FileAttributes.ReparsePoint))
                return null;
            FileSystemInfo link = attributes.HasFlag(FileAttributes.Directory) ? new DirectoryInfo(path) : new FileInfo(path);
            return link.LinkTarget;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// The number of names (hard links) of the file at a local path; 1 when the path is no file. By default
    /// <see cref="FileSystemLinkCount"/>; the tests give fictitious counts.
    /// </summary>
    public Func<string, uint> LinkCountOf { get; init; } = FileSystemLinkCount;

    /// <summary>The names of a file of the local disk, read on its handle (`nNumberOfLinks`); 1 when it is no file that opens.</summary>
    public static uint FileSystemLinkCount(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return FileIdentity.Of(path)?.Links ?? 1;
    }

    /// <summary>
    /// Whether a folder is readable by the broad groups of the machine (AR-98). By default
    /// <see cref="FolderExposure.ReadableByOthers"/>; the tests give fictitious answers.
    /// </summary>
    public Func<string, bool> IsReadableByOthers { get; init; } = FolderExposure.ReadableByOthers;

    /// <summary>The variables that the OneDrive client sets to its folders (personal, organisation, either).</summary>
    public static IReadOnlyList<string> SynchronisedFolderVariables { get; } = ["OneDrive", "OneDriveCommercial", "OneDriveConsumer"];

    /// <summary>The environment read through a reader of variables and a test of the drives, both given (pure).</summary>
    public static DataFileEnvironment From(Func<string, string?> variable, Func<char, bool> isNetworkDrive, params string[] otherSynchronisedFolders)
    {
        ArgumentNullException.ThrowIfNull(variable);
        ArgumentNullException.ThrowIfNull(isNetworkDrive);
        ArgumentNullException.ThrowIfNull(otherSynchronisedFolders);
        var folders = SynchronisedFolderVariables.Select(variable).Concat(otherSynchronisedFolders)
            .Where(folder => !string.IsNullOrWhiteSpace(folder)).Select(folder => folder!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return new DataFileEnvironment(folders, isNetworkDrive);
    }

    /// <summary>The environment of this machine: the variables of the process and the type of each drive.</summary>
    public static DataFileEnvironment Current(params string[] otherSynchronisedFolders) =>
        From(Environment.GetEnvironmentVariable, IsNetworkDriveOfThisMachine, otherSynchronisedFolders);

    private static bool IsNetworkDriveOfThisMachine(char letter)
    {
        try
        {
            return new DriveInfo(letter.ToString()).DriveType == DriveType.Network;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
