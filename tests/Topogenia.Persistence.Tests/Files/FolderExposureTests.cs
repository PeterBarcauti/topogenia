// FolderExposureTests.cs
// A folder of the desktop mode readable by the broad groups of the machine, read in its real access control list (AR-98).

using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Topogenia.Persistence.Sqlite.Files;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Files;

/// <summary>
/// The folders are made under the temporary folder of the profile, which the broad groups cannot read; a rule given to
/// `Users` then exposes the folder, and a rule that denies it hides it again. The rules are those of the test folder only.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FolderExposureTests
{
    private static void Give(DirectoryInfo folder, SecurityIdentifier group, AccessControlType type)
    {
        var rules = folder.GetAccessControl();
        rules.AddAccessRule(new FileSystemAccessRule(group, FileSystemRights.ReadData, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, type));
        folder.SetAccessControl(rules);
    }

    /// <summary>The three broad groups of AR-98: everyone, the users of the machine, the authenticated users.</summary>
    public static TheoryData<WellKnownSidType> BroadGroups =>
        [WellKnownSidType.WorldSid, WellKnownSidType.BuiltinUsersSid, WellKnownSidType.AuthenticatedUserSid];

    [Theory]
    [MemberData(nameof(BroadGroups))]
    public void A_folder_of_the_profile_is_not_exposed_until_a_broad_group_may_read_it_and_not_when_it_is_denied(WellKnownSidType type)
    {
        var group = new SecurityIdentifier(type, null);
        var folder = Directory.CreateTempSubdirectory($"{TestDatabase.FolderPrefix}{Environment.ProcessId}-");
        try
        {
            Assert.False(FolderExposure.ReadableByOthers(folder.FullName));
            Give(folder, group, AccessControlType.Allow);
            Assert.True(FolderExposure.ReadableByOthers(folder.FullName));
            Give(folder, group, AccessControlType.Deny);
            Assert.False(FolderExposure.ReadableByOthers(folder.FullName));
        }
        finally
        {
            // The rule that denies a group the account of the test belongs to also denies it the listing the deletion needs.
            var rules = folder.GetAccessControl();
            rules.PurgeAccessRules(group);
            folder.SetAccessControl(rules);
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void A_group_outside_the_three_broad_ones_does_not_expose_the_folder()
    {
        var folder = Directory.CreateTempSubdirectory($"{TestDatabase.FolderPrefix}{Environment.ProcessId}-");
        try
        {
            Give(folder, new SecurityIdentifier(WellKnownSidType.BuiltinGuestsSid, null), AccessControlType.Allow);
            Assert.False(FolderExposure.ReadableByOthers(folder.FullName));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void A_missing_folder_is_not_exposed() =>
        Assert.False(FolderExposure.ReadableByOthers(Path.Combine(Path.GetTempPath(), $"{TestDatabase.FolderPrefix}missing-{Guid.NewGuid():N}")));
}
