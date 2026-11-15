// DesktopDataFileTests.cs
// The default data file of the desktop mode, and the refusal of a file on a network share or in a synchronised folder (spec 5.2).

using System.Runtime.InteropServices;
using Topogenia.Application;
using Topogenia.Persistence.Sqlite.Files;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Files;

/// <summary>
/// A pure function on fictitious paths: no machine, share, organisation or user of the tests is real. The environment
/// gives the synchronised folders and the network drives that the host reads from the machine (P4-T05).
/// </summary>
public sealed class DesktopDataFileTests
{
    private static readonly DataFileEnvironment Plain = new([], _ => false);

    private static DataFileEnvironment Variables(Dictionary<string, string?> values, params char[] networkDrives) =>
        DataFileEnvironment.From(name => values.GetValueOrDefault(name), letter => networkDrives.Contains(letter));

    /// <summary>The rule that the host calls for the data file when the configuration does not move it (`DesktopPaths.Of`).</summary>
    [Fact]
    public void The_default_data_file_is_in_the_data_folder_of_the_folder_of_the_product()
    {
        var path = DesktopDataFile.DefaultPathIn($@"C:\Users\fictitious.user\AppData\Local\{ProductIdentity.Name}");

        Assert.Equal($@"C:\Users\fictitious.user\AppData\Local\{ProductIdentity.Name}\data\{ProductIdentity.Name.ToLowerInvariant()}.db", path);
        Assert.Null(DesktopDataFile.Check(path, Plain));
        Assert.Throws<ArgumentException>(() => DesktopDataFile.DefaultPathIn(" "));
    }

    [Theory]
    [InlineData(@"\\fileserver\projects\maps\data.db", @"\\fileserver\projects")]
    [InlineData(@"//fileserver/projects/data.db", @"\\fileserver\projects")]
    [InlineData(@"\\?\UNC\fileserver\projects\data.db", @"\\fileserver\projects")]
    [InlineData(@"\\.\UNC\fileserver\projects\data.db", @"\\fileserver\projects")]
    [InlineData(@"\\localhost\c$\data\data.db", @"\\localhost\c$")]
    [InlineData(@"\\192.0.2.10\share\data.db", @"\\192.0.2.10\share")]
    [InlineData(@"\\?\GLOBALROOT\Device\Mup\fileserver\projects\data.db", @"\\fileserver\projects")]
    [InlineData(@"\\.\globalroot\device\mup\fileserver\projects\data.db", @"\\fileserver\projects")]
    [InlineData(@"\??\GLOBALROOT\Device\LanmanRedirector\fileserver\projects\data.db", @"\\fileserver\projects")]
    public void A_file_reached_by_a_network_path_is_refused_with_its_share(string path, string share)
    {
        var refusal = DesktopDataFile.Check(path, Plain);

        Assert.Equal(new DataFileRefusal(DataFileRefusals.NetworkShare, path, share), refusal);
    }

    [Theory]
    [InlineData(@"N:\maps\data.db")]
    [InlineData(@"n:\maps\data.db")]
    [InlineData(@"\\?\N:\maps\data.db")]
    public void A_file_on_a_drive_mapped_to_the_network_is_refused(string path)
    {
        var refusal = DesktopDataFile.Check(path, new DataFileEnvironment([], letter => letter == 'N'));

        Assert.Equal(DataFileRefusals.NetworkShare, refusal?.Code);
        Assert.Equal("N:", refusal?.Place, StringComparer.OrdinalIgnoreCase);
        Assert.Null(DesktopDataFile.Check(@"C:\maps\data.db", new DataFileEnvironment([], letter => letter == 'N')));
    }

    [Theory]
    [InlineData("OneDrive", @"C:\Users\fictitious.user\OneDrive")]
    [InlineData("OneDriveCommercial", @"C:\Users\fictitious.user\Fictitious Org Files")]
    [InlineData("OneDriveConsumer", @"D:\Sync\Personal")]
    public void A_file_under_a_folder_named_by_a_variable_of_the_synchronisation_client_is_refused(string variable, string folder)
    {
        var environment = Variables(new Dictionary<string, string?> { [variable] = folder });

        var refusal = DesktopDataFile.Check(folder.ToUpperInvariant() + @"\Maps\data.db", environment);

        Assert.Equal(DataFileRefusals.SynchronisedFolder, refusal?.Code);
        Assert.Equal(folder, refusal?.Place);
        Assert.Null(DesktopDataFile.Check(folder + @"-copy\data.db", environment));
    }

    [Theory]
    [InlineData(@"C:\Users\fictitious.user\OneDrive\data.db", "OneDrive")]
    [InlineData(@"C:\Users\fictitious.user\OneDrive - Fictitious Org\Maps\data.db", "OneDrive - Fictitious Org")]
    [InlineData(@"E:\SharePoint - Process Library\data.db", "SharePoint - Process Library")]
    [InlineData(@"C:\sharepoint\data.db", "sharepoint")]
    public void A_file_in_a_folder_named_as_the_synchronisation_names_its_folders_is_refused(string path, string folder)
    {
        var refusal = DesktopDataFile.Check(path, Plain);

        Assert.Equal(new DataFileRefusal(DataFileRefusals.SynchronisedFolder, path, folder), refusal);
    }

    [Theory]
    [InlineData(@"C:\Users\fictitious.user\OneDriveBackup\data.db")]
    [InlineData(@"C:\Data\MyOneDrive\data.db")]
    [InlineData(@"D:\Topo\data.db")]
    public void A_local_folder_that_only_looks_like_a_synchronised_one_is_accepted(string path)
    {
        Assert.Null(DesktopDataFile.Check(path, Plain));
    }

    [Fact]
    public void A_synchronised_folder_found_by_the_host_elsewhere_is_refused_too()
    {
        var environment = DataFileEnvironment.From(_ => null, _ => false, @"C:\Users\fictitious.user\Fictitious Org\Site - Documents\");

        var refusal = DesktopDataFile.Check(@"C:\Users\fictitious.user\Fictitious Org\Site - Documents\data.db", environment);

        Assert.Equal(DataFileRefusals.SynchronisedFolder, refusal?.Code);
        Assert.Empty(DataFileEnvironment.From(_ => " ", _ => false).SynchronisedFolders);
    }

    [Theory]
    [InlineData(@"data\data.db")]
    [InlineData(@"C:data.db")]
    [InlineData(@"\data\data.db")]
    [InlineData(@"\\?\Volume{01234567-89ab-cdef-0123-456789abcdef}\data.db")]
    [InlineData(@"\\?\GLOBALROOT\Device\HarddiskVolume3\data.db")]
    [InlineData(@"\\.\PhysicalDrive0")]
    public void A_relative_path_or_a_form_the_check_cannot_read_is_refused_with_a_code(string path)
    {
        // A refusal, not an exception: the message of an exception would carry the path, so the login, into the logs.
        Assert.Equal(new DataFileRefusal(DataFileRefusals.UnsupportedPath, path, string.Empty), DesktopDataFile.Check(path, Plain));
    }

    [Theory]
    [InlineData("C:\\data\\da\0ta.db")]
    [InlineData("C:\\da\0ta\\data.db")]
    [InlineData("\0")]
    public void A_path_with_a_character_that_no_path_holds_is_refused_with_a_code(string path)
    {
        // Path.GetFullPath throws on a NUL: a refusal, never an exception whose message would carry the path (security m3).
        Assert.Equal(new DataFileRefusal(DataFileRefusals.UnsupportedPath, path, string.Empty), DesktopDataFile.Check(path, Plain));
    }

    [Fact]
    public void A_file_with_more_than_one_name_is_refused_whatever_its_names()
    {
        const string path = @"C:\Users\fictitious.user\AppData\Local\Maps\data.db";
        var linked = new DataFileEnvironment([], _ => false) { LinkCountOf = file => file.EndsWith("data.db", StringComparison.Ordinal) ? 2u : 1u };

        Assert.Equal(new DataFileRefusal(DataFileRefusals.MultipleNames, path, string.Empty), DesktopDataFile.Check(path, linked));
        Assert.Null(DesktopDataFile.Check(path, new DataFileEnvironment([], _ => false) { LinkCountOf = _ => 1 }));
    }

    [Fact]
    public void A_hard_link_made_on_the_disk_is_seen_by_the_check_of_the_machine()
    {
        var folder = Directory.CreateTempSubdirectory($"{TestDatabase.FolderPrefix}{Environment.ProcessId}-links-");
        try
        {
            var file = Path.Combine(folder.FullName, "data.db");
            File.WriteAllText(file, "data");
            var machine = new DataFileEnvironment([], _ => false);
            Assert.Null(DesktopDataFile.Check(file, machine));

            // The other name could lie in a synchronised folder: here, next to it.
            Assert.True(CreateHardLink(Path.Combine(folder.FullName, "other-name.db"), file, IntPtr.Zero), $"CreateHardLink failed: {Marshal.GetLastPInvokeError()}");

            Assert.Equal(DataFileRefusals.MultipleNames, DesktopDataFile.Check(file, machine)?.Code);
            Assert.Equal(2u, DataFileEnvironment.FileSystemLinkCount(file));
            File.Delete(Path.Combine(folder.FullName, "other-name.db"));
            Assert.Null(DesktopDataFile.Check(file, machine));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string newFile, string existingFile, IntPtr securityAttributes);
}
