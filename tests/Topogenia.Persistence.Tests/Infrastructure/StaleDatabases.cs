// StaleDatabases.cs
// The test bases left on the local instance by a process that no longer exists: found by their name, removed at the start of a run.

using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>
/// A run stopped before its end (killed, a crash, a debugger stopped) leaves its bases `Topogenia_Test_&lt;pid&gt;_&lt;n&gt;`.
/// Before its first creation, every process removes those whose name is exactly the prefix, a process number and a
/// counter (ASCII digits), possibly followed by one more part of ASCII letters and digits that a test adds to a base of its
/// own (`Topogenia_Test_&lt;pid&gt;_&lt;n&gt;_kept`), and whose process does not run on this machine; any other base is never touched, whatever its
/// name, and a base whose process runs is kept (another run, possibly of another suite). A process number names a
/// process of this machine only: the cleaning is done when the instance is local, never on a shared remote instance.
/// The list of the bases is read without waiting for the creations in progress
/// (<see cref="TestServer.DatabasesWithoutWaitingAsync"/>), and a cleaning that fails is left to the next run
/// (<see cref="TestServer.QuietlyAsync"/>). The temporary folders of the SQLite databases and files
/// (`topogenia-test-&lt;pid&gt;-&lt;random&gt;`) are cleaned the same way.
/// </summary>
public static class StaleDatabases
{
    /// <summary>The bases among <paramref name="names"/> that a process which no longer runs left (<paramref name="running"/>).</summary>
    public static IReadOnlyList<string> Of(IEnumerable<string> names, Func<int, bool> running)
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(running);
        return [.. names.Where(name => ProcessOf(name) is { } process && !running(process))];
    }

    /// <summary>The process number written in the name of a test base, or null for any other name.</summary>
    public static int? ProcessOf(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!name.StartsWith(TestDatabase.Prefix, StringComparison.Ordinal))
            return null;
        var parts = name[TestDatabase.Prefix.Length..].Split('_');
        if (parts.Length is < 2 or > 3 || !parts.Take(2).All(part => part.Length is > 0 and <= 9 && part.All(char.IsAsciiDigit)))
            return null;
        if (parts.Length == 3 && !(parts[2].Length is > 0 and <= 32 && parts[2].All(char.IsAsciiLetterOrDigit)))
            return null;
        return int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The process number written in the name of a folder of a SQLite test database, or null for any other name.</summary>
    public static int? ProcessOfFolder(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!name.StartsWith(TestDatabase.FolderPrefix, StringComparison.Ordinal))
            return null;
        var rest = name[TestDatabase.FolderPrefix.Length..];
        var dash = rest.IndexOf('-', StringComparison.Ordinal);
        if (dash is < 1 or > 9 || dash == rest.Length - 1 || !rest[..dash].All(char.IsAsciiDigit))
            return null;
        return int.Parse(rest[..dash], System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Removes the folders of SQLite test databases that a process which no longer runs left in <paramref name="temporary"/>
    /// and returns their names; any other folder (another prefix, no process number, a running process) is kept.
    /// </summary>
    public static IReadOnlyList<string> DropFolders(string temporary)
    {
        ArgumentNullException.ThrowIfNull(temporary);
        var running = Running();
        var stale = new DirectoryInfo(temporary).EnumerateDirectories(TestDatabase.FolderPrefix + "*")
            .Where(folder => ProcessOfFolder(folder.Name) is { } process && !running.Contains(process))
            .ToList();
        foreach (var folder in stale)
        {
            try
            {
                Writable(folder);
                folder.Delete(recursive: true);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                // Removed by another run that starts at the same time, or still open: the next run tries again.
            }
        }
        return [.. stale.Select(folder => folder.Name)];
    }

    /// <summary>Clears the read-only mark that a test may leave on a file (a backup made read-only), which blocks the removal.</summary>
    private static void Writable(DirectoryInfo folder)
    {
        foreach (var file in folder.EnumerateFiles("*", SearchOption.AllDirectories).Where(file => file.IsReadOnly))
            file.IsReadOnly = false;
    }

    /// <summary>True when the data source names this machine: `.`, `(local)`, `localhost`, the loopback or the machine name.</summary>
    public static bool IsLocal(string dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        var host = dataSource.Trim();
        var protocol = host.IndexOf(':', StringComparison.Ordinal);
        if (protocol >= 0 && host[..protocol] is "tcp" or "np" or "lpc" or "admin")
            host = host[(protocol + 1)..];
        host = host.Split('\\', ',')[0].Trim();
        return host is "." or "(local)" or "127.0.0.1" or "::1"
            || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Removes the stale bases of the local instance and returns their names (nothing on a remote instance).</summary>
    public static async Task<IReadOnlyList<string>> DropAsync(CancellationToken cancellationToken)
    {
        if (!IsLocal(new SqlConnectionStringBuilder(TestServer.Server).DataSource))
            return [];
        var running = Running();
        var stale = Of(await TestServer.DatabasesWithoutWaitingAsync(cancellationToken), running.Contains);
        foreach (var name in stale)
            await DropOneAsync(name, cancellationToken);
        return stale;
    }

    /// <summary>The numbers of the processes that run on this machine.</summary>
    private static HashSet<int> Running() => Process.GetProcesses().Select(process =>
    {
        using (process)
            return process.Id;
    }).ToHashSet();

    /// <summary>Another run that starts at the same time may remove the same base first: a base gone meanwhile is not a failure.</summary>
    private static async Task DropOneAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            await TestServer.DropAsync(name, cancellationToken);
        }
        catch (SqlException)
        {
            if (await TestServer.ExistsAsync(name, cancellationToken))
                throw;
        }
    }
}
