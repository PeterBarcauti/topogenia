// FolderExposure.cs
// Whether a folder of the desktop mode is readable by the broad groups of the machine, read in its access control list (AR-98).

using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Topogenia.Persistence.Sqlite.Files;

/// <summary>
/// A folder of the data file or of the backups placed outside the profile keeps the rights of its parent (a second
/// disk, a shared folder of the machine): the other accounts of the machine may then read the data and the backups
/// (ADR 035). The rules of the folder are read, never changed: a folder is exposed when `Everyone`, `Users` or
/// `Authenticated Users` is allowed to read its data and not denied it. The start only warns of it (AR-98).
/// </summary>
public static class FolderExposure
{
    /// <summary>True when one of the broad groups may read the folder; false elsewhere than on Windows, or when the rules cannot be read.</summary>
    public static bool ReadableByOthers(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        if (!OperatingSystem.IsWindows() || !Directory.Exists(folder))
            return false;
        try
        {
            return ReadableByBroadGroups(folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool ReadableByBroadGroups(string folder)
    {
        var rules = new DirectoryInfo(folder).GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>().Where(rule => (rule.FileSystemRights & FileSystemRights.ReadData) != 0).ToList();
        var groups = new[] { WellKnownSidType.WorldSid, WellKnownSidType.BuiltinUsersSid, WellKnownSidType.AuthenticatedUserSid }
            .Select(type => new SecurityIdentifier(type, null));
        return groups.Any(group =>
            rules.Any(rule => rule.IdentityReference.Equals(group) && rule.AccessControlType == AccessControlType.Allow)
            && !rules.Any(rule => rule.IdentityReference.Equals(group) && rule.AccessControlType == AccessControlType.Deny));
    }
}
