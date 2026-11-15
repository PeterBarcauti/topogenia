// DataFileResolutionTests.cs
// The check of the data file on the path that Windows opens: `..` resolved, short names expanded, links and junctions followed (security m3).

using System.Diagnostics;
using Topogenia.Persistence.Sqlite.Files;

namespace Topogenia.Persistence.Tests.Files;

/// <summary>
/// A path written to look local may lead to a share or a synchronised folder: through `..`, through the short name 8.3
/// of a folder (`ONEDRI~1`), through a link or a junction. The rules apply to the path behind them. Fictitious paths
/// and fictitious links (a symbolic link to a share needs a privilege that the tests do not have); the short names and
/// the junction are real, in a folder created under the temporary folder by the test and deleted by it.
/// </summary>
public sealed class DataFileResolutionTests
{
    private static readonly DataFileEnvironment Plain = new([], _ => false);

    /// <summary>An environment without synchronised folder whose links are the given ones, and whose drive N: is on the network.</summary>
    private static DataFileEnvironment Linked(params (string Link, string Target)[] links) =>
        new([], letter => letter == 'N')
        {
            LinkTargetOf = path => links.FirstOrDefault(link => string.Equals(link.Link, path, StringComparison.OrdinalIgnoreCase)).Target,
        };

    [Theory]
    [InlineData(@"D:\Other\..\Sync\Personal\data.db")]
    [InlineData(@"D:\Sync\Other\..\Personal\.\data.db")]
    [InlineData(@"D:\Sync\Personal\Maps\..\data.db")]
    public void A_path_that_reaches_a_synchronised_folder_through_dots_is_refused(string path)
    {
        var environment = DataFileEnvironment.From(name => name == "OneDriveConsumer" ? @"D:\Sync\Personal" : null, _ => false);

        Assert.Equal(new DataFileRefusal(DataFileRefusals.SynchronisedFolder, path, @"D:\Sync\Personal"), DesktopDataFile.Check(path, environment));
    }

    [Theory]
    [InlineData(@"C:\Users\fictitious.user\OneDrive\..\Maps\data.db")]
    [InlineData(@"D:\Sync\Personal\..\Other\data.db")]
    public void A_path_that_leaves_a_synchronised_folder_through_dots_is_accepted(string path)
    {
        var environment = DataFileEnvironment.From(name => name == "OneDriveConsumer" ? @"D:\Sync\Personal" : null, _ => false);

        Assert.Null(DesktopDataFile.Check(path, environment));
    }

    [Theory]
    [InlineData(@"C:\Data\Maps\data.db", @"C:\Data\Maps", @"\\fileserver\projects\maps", @"\\fileserver\projects")]
    [InlineData(@"C:\Data\Maps\data.db", @"C:\Data", @"\\?\UNC\fileserver\projects", @"\\fileserver\projects")]
    [InlineData(@"C:\Data\data.db", @"C:\Data\data.db", @"//fileserver/projects/data.db", @"\\fileserver\projects")]
    [InlineData(@"C:\Data\Maps\data.db", @"C:\Data", @"N:\shared", "N:")]
    public void A_link_to_a_share_or_to_a_network_drive_is_refused(string path, string link, string target, string place)
    {
        var refusal = DesktopDataFile.Check(path, Linked((link, target)));

        Assert.Equal(new DataFileRefusal(DataFileRefusals.NetworkShare, path, place), refusal);
    }

    [Theory]
    [InlineData(@"C:\Data\Maps", @"..\Users\fictitious.user\OneDrive\Maps", "OneDrive")]
    [InlineData(@"C:\Data\Maps", @"\??\C:\Users\fictitious.user\OneDrive - Fictitious Org", "OneDrive - Fictitious Org")]
    [InlineData(@"C:\Data\Maps", @"\Sync\SharePoint - Library", "SharePoint - Library")]
    public void A_link_to_a_synchronised_folder_is_refused_whatever_the_form_of_its_target(string link, string target, string folder)
    {
        var refusal = DesktopDataFile.Check(@"C:\Data\Maps\data.db", Linked((link, target)));

        Assert.Equal(new DataFileRefusal(DataFileRefusals.SynchronisedFolder, @"C:\Data\Maps\data.db", folder), refusal);
    }

    [Fact]
    public void A_chain_of_links_is_followed_to_its_end()
    {
        var environment = Linked((@"C:\Data", @"E:\Store"), (@"E:\Store\Maps", @"F:\Deep"), (@"F:\Deep\Last", @"\\fileserver\projects"));

        Assert.Equal(DataFileRefusals.NetworkShare, DesktopDataFile.Check(@"C:\Data\Maps\Last\data.db", environment)?.Code);
        Assert.Null(DesktopDataFile.Check(@"C:\Data\Maps\Other\data.db", environment));
    }

    [Fact]
    public void A_loop_of_links_or_a_volume_mounted_in_a_folder_ends_the_check_without_a_refusal()
    {
        var loop = Linked((@"C:\A", @"C:\B"), (@"C:\B", @"C:\A"));
        var volume = Linked((@"C:\Mounted", @"\\?\Volume{00000000-0000-0000-0000-000000000000}\"));

        Assert.Null(DesktopDataFile.Check(@"C:\A\data.db", loop));
        Assert.Null(DesktopDataFile.Check(@"C:\Mounted\data.db", volume));
    }

    [Fact]
    public void The_short_name_of_a_folder_named_as_onedrive_names_its_folders_is_refused()
    {
        using var folder = new TemporaryFolder();
        folder.Create("OneDrive - Fictitious Org");
        var shortName = folder.ShortNameOf("ONEDRI~1");

        var refusal = DesktopDataFile.Check(Path.Combine(shortName, "data.db"), Plain);

        Assert.Equal(DataFileRefusals.SynchronisedFolder, refusal?.Code);
        Assert.Equal("OneDrive - Fictitious Org", refusal?.Place);
    }

    [Fact]
    public void The_short_name_of_a_synchronised_folder_of_the_environment_is_refused()
    {
        using var folder = new TemporaryFolder();
        var root = folder.Create("Synchronised Root");
        var shortName = folder.ShortNameOf("SYNCHR~1");
        var environment = DataFileEnvironment.From(_ => null, _ => false, root);

        var refusal = DesktopDataFile.Check(Path.Combine(shortName, "Maps", "data.db"), environment);

        Assert.Equal(new DataFileRefusal(DataFileRefusals.SynchronisedFolder, Path.Combine(shortName, "Maps", "data.db"), root), refusal);
    }

    [Fact]
    public void A_junction_to_a_synchronised_folder_is_refused_and_a_junction_to_a_local_folder_is_accepted()
    {
        using var folder = new TemporaryFolder();
        var synchronised = folder.Create(Path.Combine("OneDrive - Fictitious Org", "Maps"));
        var local = folder.Create("Local Maps");
        var toSynchronised = folder.Junction("maps", synchronised);
        var toLocal = folder.Junction("other", local);

        var refusal = DesktopDataFile.Check(Path.Combine(toSynchronised, "data.db"), Plain);

        Assert.Equal(DataFileRefusals.SynchronisedFolder, refusal?.Code);
        Assert.Equal("OneDrive - Fictitious Org", refusal?.Place);
        Assert.Null(DesktopDataFile.Check(Path.Combine(toLocal, "data.db"), Plain));
        Assert.Equal(synchronised, DataFileEnvironment.FileSystemLinkTarget(toSynchronised), StringComparer.OrdinalIgnoreCase);
        Assert.Null(DataFileEnvironment.FileSystemLinkTarget(local));
        Assert.Null(DataFileEnvironment.FileSystemLinkTarget(Path.Combine(local, "missing")));
    }

    /// <summary>A folder under the temporary folder of the user, created for one test and deleted by it with what it holds.</summary>
    private sealed class TemporaryFolder : IDisposable
    {
        private readonly DirectoryInfo folder = Directory.CreateTempSubdirectory($"{Infrastructure.TestDatabase.FolderPrefix}{Environment.ProcessId}-files-");
        private readonly List<string> junctions = [];

        public string Create(string relative) => Directory.CreateDirectory(Path.Combine(folder.FullName, relative)).FullName;

        /// <summary>The path of a folder by its short name, which the volume must have generated (the temporary folder of the profile does).</summary>
        public string ShortNameOf(string shortName)
        {
            var path = Path.Combine(folder.FullName, shortName);
            Assert.True(Directory.Exists(path), $"The volume of the temporary folder gives no short name {shortName}.");
            return path;
        }

        /// <summary>A junction (no privilege needed, unlike a symbolic link) made by `mklink /J` of the command interpreter.</summary>
        public string Junction(string name, string target)
        {
            var link = Path.Combine(folder.FullName, name);
            var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            foreach (var argument in new[] { "/c", "mklink", "/J", link, target })
                start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            Assert.Equal(0, process.ExitCode);
            junctions.Add(link);
            return link;
        }

        public void Dispose()
        {
            // A junction is removed as a link first, so that nothing is ever deleted through it.
            foreach (var junction in junctions.Where(Directory.Exists))
                Directory.Delete(junction);
            folder.Refresh();
            if (folder.Exists)
                folder.Delete(recursive: true);
        }
    }
}
