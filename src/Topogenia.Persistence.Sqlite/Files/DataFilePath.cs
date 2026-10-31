// DataFilePath.cs
// The forms of the path of a data file that its check compares: text normalised, full path with short names expanded, target of its links.

namespace Topogenia.Persistence.Sqlite.Files;

/// <summary>
/// The steps that turn the path given by the host into the path that Windows will open: the text with back slashes and
/// without the prefixes of the long paths; the full path (`.` and `..` removed, names 8.3 such as `ONEDRI~1` expanded to
/// their long names where the folders exist, by <see cref="Path.GetFullPath(string)"/>); and the path behind the first
/// link (symbolic link or junction) found from the root. Only a local drive is read, and only its metadata.
/// </summary>
internal static class DataFilePath
{
    private static readonly string[] LongPathPrefixes = [@"\\?\", @"\\.\", @"\??\"];

    /// <summary>
    /// The devices of Windows behind which a path is a share: the provider of the UNC paths (`Mup`) and the redirector of
    /// the network (`LanmanRedirector`), reached through `GLOBALROOT` (`\\?\GLOBALROOT\Device\Mup\server\share`).
    /// </summary>
    private static readonly string[] NetworkDevices = [@"GLOBALROOT\Device\Mup\", @"GLOBALROOT\Device\LanmanRedirector\"];

    /// <summary>
    /// The path with back slashes, without the prefix of the long paths (`\\?\`, `\\.\`, `\??\`) and without a final
    /// separator; null for a UNC path or a path through a network device of Windows, which is always a share.
    /// </summary>
    public static string? Normalised(string path)
    {
        var text = path.Trim().Replace('/', '\\');
        foreach (var prefix in LongPathPrefixes)
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal))
            {
                text = text[prefix.Length..];
                if (text.StartsWith(@"UNC\", StringComparison.OrdinalIgnoreCase) || NetworkDevice(text) is not null)
                    return null;
            }
        }
        if (text.StartsWith(@"\\", StringComparison.Ordinal))
            return null;
        return text.Length > 3 ? text.TrimEnd('\\') : text;
    }

    /// <summary>`\\server\share` of a UNC path or of a path through a network device, for the explanation.</summary>
    public static string Share(string path)
    {
        var text = path.Trim().Replace('/', '\\');
        foreach (var prefix in LongPathPrefixes)
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal) && NetworkDevice(text[prefix.Length..]) is { } device)
                text = text[(prefix.Length + device.Length)..];
        }
        var parts = text.Split('\\', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => part is not ("?" or "." or "??") && !string.Equals(part, "UNC", StringComparison.OrdinalIgnoreCase))
            .Take(2);
        return @"\\" + string.Join('\\', parts);
    }

    private static string? NetworkDevice(string text) =>
        NetworkDevices.FirstOrDefault(device => text.StartsWith(device, StringComparison.OrdinalIgnoreCase));

    public static bool IsDrivePath(string path) =>
        path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '\\';

    /// <summary>The full path of a drive path: `.` and `..` resolved, short names of existing folders expanded.</summary>
    public static string Expanded(string drivePath)
    {
        var full = Path.GetFullPath(drivePath);
        return full.Length > 3 ? full.TrimEnd('\\') : full;
    }

    /// <summary>
    /// The path behind the first link of <paramref name="drivePath"/>, from the root down (the target of the link, then
    /// the rest of the path), or null when no folder of the path, nor the file, is a link. A relative target is taken
    /// from the folder of the link.
    /// </summary>
    public static string? BehindFirstLink(string drivePath, Func<string, string?> linkTargetOf)
    {
        var parts = drivePath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        var prefix = parts[0] + '\\';
        for (var i = 1; i < parts.Length; i++)
        {
            var parent = prefix;
            prefix = Path.Join(prefix, parts[i]);
            if (linkTargetOf(prefix) is not { Length: > 0 } target)
                continue;
            var absolute = Absolute(target, parent);
            return i + 1 < parts.Length ? Path.Join(absolute, string.Join('\\', parts[(i + 1)..])) : absolute;
        }
        return null;
    }

    /// <summary>
    /// A target of a link as an absolute path: kept when fully qualified, UNC or long (`\\`, `\??\`); taken from the drive
    /// of the link when it starts at the root of a drive (`\folder`); else taken from the folder of the link.
    /// </summary>
    private static string Absolute(string target, string folderOfLink)
    {
        if (Path.IsPathFullyQualified(target) || target.StartsWith(@"\\", StringComparison.Ordinal) || target.StartsWith(@"\??\", StringComparison.Ordinal))
            return target;
        return Path.IsPathRooted(target) ? Path.Join(folderOfLink[..2], target) : Path.Join(folderOfLink, target);
    }

    /// <summary>True when <paramref name="path"/> is <paramref name="root"/> or under it, both full drive paths.</summary>
    public static bool IsUnder(string path, string root) =>
        IsDrivePath(root)
        && (string.Equals(path, root, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(root.TrimEnd('\\') + '\\', StringComparison.OrdinalIgnoreCase));
}
