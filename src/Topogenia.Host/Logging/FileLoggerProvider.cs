// FileLoggerProvider.cs
// Writes the logs of the host to one file per UTC day in a folder, keeps 30 days, never fails the application (spec 5.2, ADR 021).

using System.Globalization;
using System.Security;
using System.Text;
using Topogenia.Application;
using Topogenia.Application.Ports;

namespace Topogenia.Host.Logging;

/// <summary>
/// The file destination of <c>Microsoft.Extensions.Logging</c> (ADR 021): one file per UTC day (D08), named after the
/// product in lower case and the day (D13), in a folder created when it is missing. The files of the provider older than
/// <see cref="RetentionDays"/> days, by the day of their name, are deleted at the start and at each change of day; any other
/// file of the folder is left. The entries are written one at a time and flushed at once. A failure of the file (folder
/// that cannot be created, file locked or read-only) loses the entry and never reaches the caller: the log never stops the
/// application. The minimal level is the one of the <c>Logging</c> section, applied by the logging framework; the alias
/// <c>File</c> names the provider there.
/// </summary>
[ProviderAlias("File")]
public sealed class FileLoggerProvider : ILoggerProvider
{
    /// <summary>Days a file is kept after its own day (spec 5.2).</summary>
    public const int RetentionDays = 30;

    private const string DayFormat = "yyyyMMdd";
    private const string Extension = ".log";
    private static readonly string Prefix = ProductIdentity.Name.ToLowerInvariant() + "-";
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string folder;
    private readonly IClock clock;
    private readonly Lock gate = new();
    private DateTime day;
    private StreamWriter? writer;
    private bool disposed;

    public FileLoggerProvider(string folder, IClock clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(clock);
        this.folder = folder;
        this.clock = clock;
        lock (gate)
        {
            day = clock.UtcNow.Date;
            Purge(day);
        }
    }

    /// <summary>The name of the file of a UTC day: the product in lower case, a hyphen, the day as yyyymmdd, ".log".</summary>
    public static string FileNameOf(DateTime day) => Prefix + day.ToString(DayFormat, CultureInfo.InvariantCulture) + Extension;

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            Close();
        }
    }

    /// <summary>
    /// Writes one entry to the file of the current UTC day, stamped with the time of the clock read under the lock, so that
    /// the entries of several threads never mix and stay in the order of their stamps. The first entry of a new day closes
    /// the file of the previous day and purges the folder.
    /// </summary>
    internal void Write(string entry)
    {
        lock (gate)
        {
            if (disposed)
                return;
            var time = clock.UtcNow;
            if (time.Date != day)
            {
                Close();
                day = time.Date;
                Purge(day);
            }
            try
            {
                writer ??= Open(day);
                writer.Write(time.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
                writer.Write(' ');
                writer.Write(entry);
                writer.Write('\n');
                writer.Flush();
            }
            catch (Exception failure) when (IsFileFailure(failure))
            {
                Close();
            }
        }
    }

    private StreamWriter Open(DateTime fileDay)
    {
        Directory.CreateDirectory(folder);
        var stream = new FileStream(Path.Combine(folder, FileNameOf(fileDay)), FileMode.Append, FileAccess.Write, FileShare.Read);
        return new StreamWriter(stream, Utf8);
    }

    /// <summary>Closes the file of the day; the next entry opens it again, after a failure as well.</summary>
    private void Close()
    {
        var current = writer;
        writer = null;
        try
        {
            current?.Dispose();
        }
        catch (Exception failure) when (IsFileFailure(failure))
        {
            // The entries already flushed are in the file; a failure of the last flush loses nothing more.
        }
    }

    /// <summary>Deletes the files of the provider older than the retention, by the day of their name; each failure is skipped.</summary>
    private void Purge(DateTime today)
    {
        try
        {
            if (!Directory.Exists(folder))
                return;
            foreach (var path in Directory.EnumerateFiles(folder, Prefix + "*" + Extension))
            {
                if (DayOf(Path.GetFileName(path)) is { } fileDay && (today - fileDay).TotalDays > RetentionDays)
                    Delete(path);
            }
        }
        catch (Exception failure) when (IsFileFailure(failure))
        {
            // A folder that cannot be read keeps its files until the next purge.
        }
    }

    private static void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception failure) when (IsFileFailure(failure))
        {
            // A file locked or protected stays until the next purge.
        }
    }

    /// <summary>The UTC day of a file named exactly as the provider names its files, else null.</summary>
    private static DateTime? DayOf(string name)
    {
        if (name.Length != Prefix.Length + DayFormat.Length + Extension.Length
            || !name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            || !name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            return null;
        return DateTime.TryParseExact(name.AsSpan(Prefix.Length, DayFormat.Length), DayFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var fileDay)
            ? fileDay
            : null;
    }

    private static bool IsFileFailure(Exception failure) =>
        failure is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException or ArgumentException;
}
