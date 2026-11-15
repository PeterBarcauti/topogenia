// ZipEntryTime.cs
// The fixed time of every entry of a package archive: the archive holds no clock time and is the same for the same content.

using System.IO.Compression;

namespace Topogenia.Interchange.Packages;

/// <summary>
/// A ZIP entry carries a local modification time, typed as an offset date by the framework. The package sets every entry
/// to the first time a ZIP can hold (1980-01-01, offset zero): the archive reveals no clock time of the machine that
/// wrote it, and the same entries give the same archive (spec 7.2). The only use of an offset date in the shipped code
/// (D08 exception, ADR 051); the time of the export is in the manifest, in UTC.
/// </summary>
internal static class ZipEntryTime
{
    public static void Fix(ZipArchiveEntry entry) => entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
}
