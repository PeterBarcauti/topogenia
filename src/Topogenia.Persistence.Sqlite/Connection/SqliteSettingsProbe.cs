// SqliteSettingsProbe.cs
// Reads back from SQLite itself the settings of an open connection, for the tests of the settings and of the backup only.

using System.Data.Common;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(Topogenia.Persistence.Schema.TechnicalNames.PersistenceTests)]

namespace Topogenia.Persistence.Sqlite.Connection;

/// <summary>
/// What the tests of the settings (spec 4.3-6) and of the backup (journal of the copy) need from SQLite, kept here because
/// no SQL text lives outside the providers (spec 3.2, ADR 025): the four settings that SQLite holds for a connection,
/// read by its pragmas, never taken from a settings object. No type of the application uses it; visible to the
/// persistence tests only (Architecture m8).
/// </summary>
internal static class SqliteSettingsProbe
{
    /// <summary>The settings that SQLite holds for an open connection, read from SQLite itself.</summary>
    public static Task<SqliteConnectionSettings> ReadAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return SqlitePragmas.ReadAsync(connection, cancellationToken);
    }
}
