// SqliteStartupErrorTests.cs
// The failures of a SQLite file met by the check or the migration of the start: busy, not opened, not written (ADR 036, annex A 5).

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Topogenia.Application.Ports;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Sqlite;
using Topogenia.Persistence.Sqlite.Backup;
using Topogenia.Persistence.Sqlite.Connection;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Sqlite;

namespace Topogenia.Persistence.Tests.Startup;

/// <summary>
/// SQLite only (the numbers of SQL Server are proven in <see cref="TransientStartupTests"/> and
/// <see cref="StartupErrorTests"/>). A file has no account, but the engine fails for the same causes as a server: the
/// file is held by another connection (`SQLITE_BUSY` 5, `SQLITE_LOCKED` 6, extended codes included), read as
/// `BASE_OCCUPEE`, the only transient code; the file cannot be opened or is not a database (`SQLITE_CANTOPEN` 14,
/// `SQLITE_NOTADB` 26), read as `BASE_INACCESSIBLE`; the account cannot write it (`SQLITE_READONLY` 8, `SQLITE_PERM` 3),
/// read as `DROITS_INSUFFISANTS`. Any other failure stays the one of the engine (ADR 036, revision of the desktop mode).
/// The files are real: a restored backup (rollback journal, as the backup leaves it, ADR 029) is accepted and taken to the
/// write-ahead log; the same file whose reserved lock another connection holds is busy, the start cannot set its journal
/// mode; a text file is not a database; a folder in place of the file cannot be opened; a file marked read-only cannot be
/// written.
/// </summary>
public sealed class SqliteStartupErrorTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SchemaCatalog Catalog => new SqliteEngine("Data Source=data.db").Catalog;

    /// <summary>
    /// The result code of the failure of SQLite behind the refusal, kept as its inner exception without the message of the
    /// engine (ADR 036): the result code, then the extended code.
    /// </summary>
    private static int Engine(SchemaRefusedException refusal) =>
        Assert.IsType<EngineFailureException>(refusal.InnerException).Numbers[0] & 0xFF;

    private static SqliteEngine EngineOn(string file) =>
        new(new SqliteConnectionStringBuilder { DataSource = file, Pooling = false }.ConnectionString);

    [Theory]
    [InlineData(5, 5, SchemaCodes.DatabaseBusy)]
    [InlineData(5, 261, SchemaCodes.DatabaseBusy)]
    [InlineData(5, 517, SchemaCodes.DatabaseBusy)]
    [InlineData(5, 773, SchemaCodes.DatabaseBusy)]
    [InlineData(261, 261, SchemaCodes.DatabaseBusy)]
    [InlineData(6, 6, SchemaCodes.DatabaseBusy)]
    [InlineData(6, 262, SchemaCodes.DatabaseBusy)]
    [InlineData(6, 518, SchemaCodes.DatabaseBusy)]
    [InlineData(14, 14, SchemaCodes.DatabaseUnreachable)]
    [InlineData(14, 526, SchemaCodes.DatabaseUnreachable)]
    [InlineData(26, 26, SchemaCodes.DatabaseUnreachable)]
    [InlineData(8, 8, SchemaCodes.InsufficientRights)]
    [InlineData(8, 1032, SchemaCodes.InsufficientRights)]
    [InlineData(3, 3, SchemaCodes.InsufficientRights)]
    [InlineData(11, 11, null)]
    [InlineData(13, 13, null)]
    [InlineData(10, 3850, null)]
    [InlineData(19, 2067, null)]
    [InlineData(1, 1, null)]
    // The result code says nothing (`SQLITE_ERROR`), the extended code says everything: each is read.
    [InlineData(1, 261, SchemaCodes.DatabaseBusy)]
    [InlineData(1, 526, SchemaCodes.DatabaseUnreachable)]
    [InlineData(1, 1032, SchemaCodes.InsufficientRights)]
    public void The_result_code_of_sqlite_and_its_extended_code_are_read_as_one_refusal(int code, int extended, string? refusal)
    {
        var failure = new SqliteException($"SQLite Error {code}.", code, extended);
        Assert.Equal(refusal, Catalog.RefusalOf(failure));
        Assert.Equal(refusal, Catalog.RefusalOf(new InvalidOperationException("Wrapped by EF Core.", failure)));
    }

    [Fact]
    public void A_failure_that_is_not_one_of_sqlite_is_no_refusal()
    {
        Assert.Null(Catalog.RefusalOf(new InvalidOperationException("Not of the engine.")));
        Assert.Null(Catalog.RefusalOf(new TimeoutException("Not of the engine.", new IOException("Disk."))));
    }

    /// <summary>
    /// A backup of a migrated file, made by the backup of the application: a self-contained file in the rollback journal,
    /// which a restore copies in place of the data file (ADR 029).
    /// </summary>
    private static async Task<string> BackupAsync(TestDatabase database)
    {
        await new SchemaMigrations(database.Engine, new FixedClock()).MigrateAsync(Token);
        var folder = Path.Combine(Path.GetDirectoryName(database.Name)!, "backups");
        var backup = await new SqliteBackupService((SqliteEngine)database.Engine, folder, new FixedClock(), SqliteFiles.LocalMachine)
            .BackupAsync(BackupReason.Daily, Token);
        Assert.True(backup.Done, backup.Failure);
        await using var copy = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backup.File, Pooling = false }.ConnectionString);
        await copy.OpenAsync(Token);
        Assert.Equal(SqliteJournalMode.Delete, (await SqliteSettingsProbe.ReadAsync(copy, Token)).JournalMode);
        return backup.File!;
    }

    [Fact]
    public async Task A_restored_backup_in_the_rollback_journal_is_accepted_by_the_check_and_taken_to_the_write_ahead_log()
    {
        await using var database = TestDatabase.Create(EngineKind.Sqlite);
        var engine = EngineOn(await BackupAsync(database));

        var state = await new SchemaMigrations(engine, new FixedClock()).CheckAsync(Token);

        Assert.Equal(SchemaStatus.UpToDate, state.Status);
        await using var context = engine.CreateContext();
        await context.Database.OpenConnectionAsync(Token);
        Assert.Equal(SqliteConnectionSettings.Default, await SqliteSettingsProbe.ReadAsync(context.Database.GetDbConnection(), Token));
    }

    [Fact]
    public async Task A_file_held_by_another_connection_is_refused_as_busy_by_the_check_and_the_migration_and_a_new_run_succeeds()
    {
        await using var database = TestDatabase.Create(EngineKind.Sqlite);
        var file = await BackupAsync(database);
        var migrations = new SchemaMigrations(EngineOn(file), new FixedClock());

        var refusals = new List<SchemaRefusedException>();
        // Another connection, without the settings of the application, takes the reserved lock of the file (`BEGIN
        // IMMEDIATE`): the file stays readable, but its journal mode cannot change until the lock is released.
        await using (var holder = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, Pooling = false }.ConnectionString))
        {
            await holder.OpenAsync(Token);
            await using var held = holder.BeginTransaction(deferred: false);
            refusals.Add(await Assert.ThrowsAsync<SchemaRefusedException>(() => migrations.CheckAsync(Token)));
            refusals.Add(await Assert.ThrowsAsync<SchemaRefusedException>(() => migrations.MigrateAsync(Token)));
        }

        Assert.All(refusals, refusal =>
        {
            Assert.Equal(SchemaCodes.DatabaseBusy, refusal.Code);
            Assert.True(refusal.IsTransient);
            Assert.Equal(SqliteDialect.Busy, Engine(refusal));
        });
        Assert.Equal(SchemaStatus.UpToDate, (await migrations.CheckAsync(Token)).Status);
        Assert.Equal(SchemaStatus.UpToDate, (await migrations.MigrateAsync(Token)).Status);
    }

    [Fact]
    public async Task A_file_that_is_not_a_database_is_refused_as_unreachable_by_the_check_and_the_migration()
    {
        await using var database = TestDatabase.Create(EngineKind.Sqlite);
        await File.WriteAllTextAsync(database.Name, new string('x', 4096), Token);
        var migrations = new SchemaMigrations(database.Engine, new FixedClock());

        var check = await Assert.ThrowsAsync<SchemaRefusedException>(() => migrations.CheckAsync(Token));
        var migration = await Assert.ThrowsAsync<SchemaRefusedException>(() => migrations.MigrateAsync(Token));

        Assert.All(new[] { check, migration }, refusal =>
        {
            Assert.Equal(SchemaCodes.DatabaseUnreachable, refusal.Code);
            Assert.False(refusal.IsTransient);
            Assert.Equal(26, Engine(refusal));
        });
        Assert.Equal(new string('x', 4096), await File.ReadAllTextAsync(database.Name, Token));
    }

    [Fact]
    public async Task A_data_file_that_cannot_be_opened_is_refused_as_unreachable_by_the_migration()
    {
        await using var database = TestDatabase.Create(EngineKind.Sqlite);
        // A folder in place of the file: SQLite cannot open it, as a file the account may not open.
        Directory.CreateDirectory(database.Name);
        var migrations = new SchemaMigrations(database.Engine, new FixedClock());

        var migration = await Assert.ThrowsAsync<SchemaRefusedException>(() => migrations.MigrateAsync(Token));

        Assert.Equal(SchemaCodes.DatabaseUnreachable, migration.Code);
        Assert.Equal(14, Engine(migration));
        Assert.Empty(Directory.GetFileSystemEntries(database.Name));
    }

    [Fact]
    public async Task A_data_file_that_the_account_may_not_write_is_refused_for_its_rights_by_the_check_and_the_migration()
    {
        await using var database = TestDatabase.Create(EngineKind.Sqlite);
        await new SchemaMigrations(database.Engine, new FixedClock()).MigrateAsync(Token);
        SqliteConnection.ClearAllPools();
        var migrations = new SchemaMigrations(database.Engine, new FixedClock());
        File.SetAttributes(database.Name, FileAttributes.ReadOnly);
        try
        {
            var check = await Assert.ThrowsAsync<SchemaRefusedException>(() => migrations.CheckAsync(Token));
            var migration = await Assert.ThrowsAsync<SchemaRefusedException>(() => migrations.MigrateAsync(Token));

            Assert.All(new[] { check, migration }, refusal =>
            {
                Assert.Equal(SchemaCodes.InsufficientRights, refusal.Code);
                Assert.False(refusal.IsTransient);
                Assert.Equal(8, Engine(refusal));
            });
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.SetAttributes(database.Name, FileAttributes.Normal);
        }
        Assert.Equal(SchemaStatus.UpToDate, (await migrations.CheckAsync(Token)).Status);
    }
}
