// DesktopDataFile.cs
// The data file of the desktop mode: its default place, and the refusal of a file on a network share or in a synchronised folder (spec 5.2).

using Topogenia.Application;

namespace Topogenia.Persistence.Sqlite.Files;

/// <summary>Stable codes of the refusal of a data file (spec 5.2); the host gives the explanation in the language of the user.</summary>
public static class DataFileRefusals
{
    /// <summary>The file is on a network share (UNC path or mapped network drive): SQLite may corrupt it there.</summary>
    public const string NetworkShare = "FICHIER_SUR_PARTAGE";

    /// <summary>The file is in a folder synchronised by OneDrive or SharePoint: the synchronisation may corrupt it.</summary>
    public const string SynchronisedFolder = "FICHIER_DOSSIER_SYNCHRONISE";

    /// <summary>
    /// The path is not an absolute path of a drive: relative (`data\file.db`, `C:file.db`, `\data\file.db`, the host
    /// resolves it first) or a form that the check cannot read (a device or a volume by its name, `\\?\Volume{...}\`,
    /// `\\?\GLOBALROOT\...`): refused, never opened.
    /// </summary>
    public const string UnsupportedPath = "FICHIER_CHEMIN_NON_PRIS_EN_CHARGE";

    /// <summary>
    /// The file has more than one name (a hard link, `mklink /H`): its other name may lie in a synchronised folder or on
    /// a path that the check never sees, so it is refused whatever its names (security m2).
    /// </summary>
    public const string MultipleNames = "FICHIER_LIENS_PHYSIQUES";
}

/// <summary>A data file refused, with its code and the share or synchronised folder that holds it.</summary>
public sealed record DataFileRefusal(string Code, string Path, string Place);

/// <summary>
/// The data file of the desktop mode (spec 5.2). By default `data\&lt;product&gt;.db` in the folder of the product
/// (<see cref="DefaultPathIn"/>), which is by default `%LOCALAPPDATA%\&lt;product&gt;`: in the profile of the user and under
/// its NTFS rights. A file on a network share or in a synchronised folder is refused (risk
/// of corruption of SQLite, whose locks and journal need a local disk): projects are exchanged by package. The check
/// works on the path that Windows will open (<see cref="DataFilePath"/>): `.` and `..` resolved, short names expanded,
/// links and junctions followed, a share never touched. The host calls it before opening the file (P4-T05); the backup
/// checks its folder with it. A file with more than one name (hard link) is refused: its other names are not read. It
/// cannot see a drive made by `subst`, a volume mounted in a folder, nor a folder synchronised by another tool that the
/// host does not name (ADR 035).
/// </summary>
public static class DesktopDataFile
{
    /// <summary>The folder of the data file under the folder of the product.</summary>
    private const string DataFolder = "data";

    /// <summary>The links followed at most for one path: a loop of links stops there (Windows refuses to open it anyway).</summary>
    public const int MaximumLinks = 31;

    private static readonly string[] SynchronisedNames = ["OneDrive", "SharePoint"];

    /// <summary>`&lt;product&gt;.db`, the product name in lower case (D13).</summary>
    private static readonly string FileName = ProductIdentity.Name.ToLowerInvariant() + ".db";

    /// <summary>The data file in the folder of the product when the configuration does not move it: `data\&lt;product&gt;.db`.</summary>
    public static string DefaultPathIn(string productFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productFolder);
        return Path.Combine(productFolder, DataFolder, FileName);
    }

    /// <summary>
    /// The refusal of a data file (or of a folder), or null when it may be opened. The path is absolute (the host resolves
    /// a relative path first; any other form is refused <see cref="DataFileRefusals.UnsupportedPath"/>, the path never
    /// copied into an exception): a UNC path (`\\server\share`, `\\?\UNC\...`, `\\?\GLOBALROOT\Device\Mup\...`) or a drive
    /// that the machine maps to the network is a share; a path under a synchronised folder of the environment, or with a folder named as OneDrive or SharePoint
    /// name theirs (`OneDrive`, `OneDrive - name`, `SharePoint`, `SharePoint - name`), is in a synchronised folder. The
    /// rules apply to the full path, then to the full path behind each link it goes through: a refusal of any of them
    /// refuses the file.
    /// </summary>
    public static DataFileRefusal? Check(string path, DataFileEnvironment environment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(environment);
        try
        {
            return Checked(path, environment);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // A character that no path may hold (NUL among them) or a path too long: refused, never opened (security m3).
            return new DataFileRefusal(DataFileRefusals.UnsupportedPath, path, string.Empty);
        }
    }

    private static DataFileRefusal? Checked(string path, DataFileEnvironment environment)
    {
        if (path.Contains(char.MinValue, StringComparison.Ordinal))
            return new DataFileRefusal(DataFileRefusals.UnsupportedPath, path, string.Empty);
        var local = DataFilePath.Normalised(path);
        if (local is null)
            return new DataFileRefusal(DataFileRefusals.NetworkShare, path, DataFilePath.Share(path));
        if (!DataFilePath.IsDrivePath(local))
            return new DataFileRefusal(DataFileRefusals.UnsupportedPath, path, string.Empty);
        var roots = environment.SynchronisedFolders.Select(DataFilePath.Normalised).OfType<string>()
            .Where(DataFilePath.IsDrivePath).Select(root => new SynchronisedRoot(root, DataFilePath.Expanded(root))).ToList();
        for (var links = 0; ; links++)
        {
            if (environment.IsNetworkDrive(char.ToUpperInvariant(local[0])))
                return new DataFileRefusal(DataFileRefusals.NetworkShare, path, local[..2]);
            var full = DataFilePath.Expanded(local);
            if (RuleRefusal(path, full, roots) is { } refusal)
                return refusal;
            if (environment.LinkCountOf(full) > 1)
                return new DataFileRefusal(DataFileRefusals.MultipleNames, path, string.Empty);
            if (links == MaximumLinks || DataFilePath.BehindFirstLink(full, environment.LinkTargetOf) is not { } target)
                return null;
            var next = DataFilePath.Normalised(target);
            if (next is null)
                return new DataFileRefusal(DataFileRefusals.NetworkShare, path, DataFilePath.Share(target));
            if (!DataFilePath.IsDrivePath(next))
                return null;
            local = next;
        }
    }

    /// <summary>A folder named by OneDrive (`OneDrive`, `OneDrive - name` for an organisation) or by a SharePoint library.</summary>
    public static bool IsSynchronisedName(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        return SynchronisedNames.Any(name => string.Equals(folder, name, StringComparison.OrdinalIgnoreCase)
            || folder.StartsWith(name + " - ", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A synchronised folder of the environment, as given and as its full path.</summary>
    private sealed record SynchronisedRoot(string Given, string Full);

    /// <summary>The refusal of a full path by the synchronised folders, then by the names of its folders.</summary>
    private static DataFileRefusal? RuleRefusal(string path, string full, List<SynchronisedRoot> roots)
    {
        if (roots.Find(root => DataFilePath.IsUnder(full, root.Given) || DataFilePath.IsUnder(full, root.Full)) is { } synchronised)
            return new DataFileRefusal(DataFileRefusals.SynchronisedFolder, path, synchronised.Given);
        if (full.Split('\\').FirstOrDefault(IsSynchronisedName) is { } folder)
            return new DataFileRefusal(DataFileRefusals.SynchronisedFolder, path, folder);
        return null;
    }
}
