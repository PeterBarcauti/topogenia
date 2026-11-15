// SqliteSettingsInterceptor.cs
// Gives the settings of spec 4.3-6 to every connection that EF Core opens on SQLite, a connection reused from the pool included; keeps the data file with its own log.

using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Topogenia.Persistence.Sqlite.Connection;

/// <summary>
/// Added by <see cref="SqliteEngine"/> before any other interceptor. EF Core calls it each time it opens the connection
/// of a context, explicitly or for a query or a save, after the connection string was applied: the settings win over a
/// connection string that says otherwise and over what a previous user left on a pooled connection. Before the opening,
/// it refuses a data file next to the write-ahead log of another file; once open, the file is checked at its first
/// opening by the process before its journal mode is set, and written back from its log by the engine alone, when a
/// connection closes (<see cref="SqliteDataFileGuard"/>, data B1).
/// </summary>
internal sealed class SqliteSettingsInterceptor(SqliteConnectionSettings settings) : DbConnectionInterceptor
{
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        SqliteDataFileGuard.Opening(connection);
        return result;
    }

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        SqliteDataFileGuard.Opening(connection);
        return ValueTask.FromResult(result);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        try
        {
            settings.Apply(connection, SqliteDataFileGuard.Opened);
        }
        catch (SqliteException error) when (SqliteDataFileGuard.IsUnreadableAtFirstOpening(connection, error))
        {
            throw SqliteDataFileGuard.Damaged();
        }
        SqliteDataFileGuard.Ready(connection);
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        try
        {
            await settings.ApplyAsync(connection, SqliteDataFileGuard.Opened, cancellationToken);
        }
        catch (SqliteException error) when (SqliteDataFileGuard.IsUnreadableAtFirstOpening(connection, error))
        {
            throw SqliteDataFileGuard.Damaged();
        }
        SqliteDataFileGuard.Ready(connection);
    }

    public override InterceptionResult ConnectionClosing(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        SqliteDataFileGuard.Closing(connection);
        return result;
    }

    public override ValueTask<InterceptionResult> ConnectionClosingAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        SqliteDataFileGuard.Closing(connection);
        return ValueTask.FromResult(result);
    }
}
