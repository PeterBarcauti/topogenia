// ConnectionSettingsTests.cs
// The settings of spec 4.3-6 read on real SQLite connections: a new one, one reused from the pool, one opened by EF Core itself.

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Persistence.Sqlite.Connection;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Sqlite;

/// <summary>
/// Every connection that the context opens has `foreign_keys = ON`, `journal_mode = WAL`, `busy_timeout = 5000` and
/// `synchronous = NORMAL` (spec 4.3-6, A-5-28), whatever the connection string says and whatever a previous user of a
/// pooled connection left on it. The values are read back from SQLite itself, never from the settings object. The class
/// runs alone: another test that clears the pools would take away the pooled connection that one test reuses.
/// </summary>
[Collection(nameof(SqlitePoolCollection))]
public sealed class ConnectionSettingsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<SqliteConnectionSettings> ReadOpenedAsync(AppDbContext context)
    {
        await context.Database.OpenConnectionAsync(Token);
        return await SqliteSettingsProbe.ReadAsync(context.Database.GetDbConnection(), Token);
    }

    private static void AssertSpecSettings(SqliteConnectionSettings read)
    {
        Assert.True(read.ForeignKeys);
        Assert.Equal(SqliteJournalMode.Wal, read.JournalMode);
        Assert.Equal(5000, read.BusyTimeoutMilliseconds);
        Assert.Equal(SqliteSynchronous.Normal, read.Synchronous);
    }

    [Fact]
    public async Task Every_connection_opened_by_the_context_has_the_settings_of_spec_4_3()
    {
        await using var files = new SqliteFiles();
        var engine = await files.MigratedAsync();
        await using var context = engine.CreateContext();
        AssertSpecSettings(await ReadOpenedAsync(context));
    }

    [Fact]
    public async Task A_connection_given_back_by_the_pool_gets_the_settings_again()
    {
        await using var files = new SqliteFiles();
        var engine = await files.MigratedAsync();
        var foreign = new SqliteConnectionSettings(false, SqliteJournalMode.Wal, 1, SqliteSynchronous.Full);
        IntPtr first;
        await using (var context = engine.CreateContext())
        {
            await ReadOpenedAsync(context);
            var connection = (SqliteConnection)context.Database.GetDbConnection();
            first = connection.Handle!.DangerousGetHandle();
            // A previous user of the pooled connection leaves other settings on it.
            await foreign.ApplyAsync(connection, Token);
            Assert.Equal(foreign, await SqliteSettingsProbe.ReadAsync(connection, Token));
        }
        await using (var context = engine.CreateContext())
        {
            var read = await ReadOpenedAsync(context);
            var connection = (SqliteConnection)context.Database.GetDbConnection();
            Assert.Equal(first, connection.Handle!.DangerousGetHandle());
            AssertSpecSettings(read);
        }
    }

    [Fact]
    public async Task Foreign_keys_are_on_even_when_the_connection_string_turns_them_off_and_ef_core_opens_the_connection()
    {
        await using var files = new SqliteFiles();
        await files.MigratedAsync();
        var off = new SqliteConnectionStringBuilder(files.ConnectionString) { ForeignKeys = false }.ConnectionString;
        await using var context = new Topogenia.Persistence.Sqlite.SqliteEngine(off).CreateContext();
        // The connection is opened by SaveChanges itself: the reference to a missing project and user is refused.
        context.Add(new ProjectMember(Guid.CreateVersion7(), Guid.CreateVersion7(), MemberRole.Reader, false, false, 0,
            FixedClock.Time, Guid.CreateVersion7(), null, null));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Token));
    }

    [Fact]
    public async Task The_command_timeout_is_the_one_of_the_engine_whatever_the_connection_string_says()
    {
        await using var files = new SqliteFiles();
        await files.MigratedAsync();
        var thirty = new SqliteConnectionStringBuilder(files.ConnectionString) { DefaultTimeout = 30 }.ConnectionString;
        await using var context = new Topogenia.Persistence.Sqlite.SqliteEngine(thirty).CreateContext();
        await context.Database.OpenConnectionAsync(Token);
        var connection = (SqliteConnection)context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();

        Assert.Equal(SqliteConnectionSettings.CommandTimeoutSeconds, context.Database.GetCommandTimeout());
        Assert.Equal(SqliteConnectionSettings.CommandTimeoutSeconds, connection.DefaultTimeout);
        Assert.Equal(1, command.CommandTimeout);
        Assert.Equal(TimeSpan.FromMilliseconds(6150), SqliteConnectionSettings.Default.MaximumBusyWait);
    }

    [Fact]
    public async Task A_connection_opened_read_only_has_every_setting_but_the_journal_mode_which_stays_the_one_of_the_file()
    {
        await using var files = new SqliteFiles();
        await files.MigratedAsync();
        SqliteFiles.CloseConnections();
        var readOnly = new SqliteConnectionStringBuilder(files.ConnectionString) { Mode = SqliteOpenMode.ReadOnly, ForeignKeys = false, DefaultTimeout = 30 };
        await using var context = new Topogenia.Persistence.Sqlite.SqliteEngine(readOnly.ConnectionString).CreateContext();

        // The read-only connection that EF Core opens to see whether the file exists gets the same settings (ADR 036).
        AssertSpecSettings(await ReadOpenedAsync(context));
        Assert.Equal(SqliteConnectionSettings.CommandTimeoutSeconds, ((SqliteConnection)context.Database.GetDbConnection()).DefaultTimeout);
        Assert.NotEqual(0, await context.Set<Topogenia.Persistence.Schema.SchemaHistoryEntry>().CountAsync(Token));
    }

    [Fact]
    public async Task The_settings_read_from_a_raw_connection_follow_what_sqlite_holds()
    {
        await using var files = new SqliteFiles();
        var raw = new SqliteConnectionStringBuilder(files.ConnectionString) { Pooling = false, ForeignKeys = false };
        await using var connection = new SqliteConnection(raw.ConnectionString);
        await connection.OpenAsync(Token);
        // A new file opened without the engine: rollback journal, synchronous FULL, no busy timeout, foreign keys as asked.
        Assert.Equal(new SqliteConnectionSettings(false, SqliteJournalMode.Delete, 0, SqliteSynchronous.Full),
            await SqliteSettingsProbe.ReadAsync(connection, Token));
        await SqliteConnectionSettings.Default.ApplyAsync(connection, Token);
        AssertSpecSettings(await SqliteSettingsProbe.ReadAsync(connection, Token));
        Assert.Equal(SqliteConnectionSettings.CommandTimeoutSeconds, connection.DefaultTimeout);
    }
}

/// <summary>The tests that rely on a connection kept by the pool: run after the others, alone.</summary>
[CollectionDefinition(nameof(SqlitePoolCollection), DisableParallelization = true)]
public sealed class SqlitePoolCollection;
