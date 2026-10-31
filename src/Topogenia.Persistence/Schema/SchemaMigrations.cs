// SchemaMigrations.cs
// Adapter of the schema port: state of the database against the migrations, migration recorded with fingerprints (spec 4.4).

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Topogenia.Application.Ports;

namespace Topogenia.Persistence.Schema;

/// <summary>
/// <see cref="ISchemaMigrations"/> on one database engine. The check reads the migrations applied by EF Core, the history
/// with fingerprints and the catalogue under a lock that keeps the migrations out (a migration running elsewhere is seen
/// entirely or not at all: <see cref="SchemaCatalog.CheckLockAsync"/>, else the lock of the migrations), and refuses a database it must not use (<see cref="SchemaVerification"/>). The
/// migration applies the pending migrations of the provider with EF Core, which creates the database when needed and
/// takes its own lock so that two processes never migrate at once (A-4-35); each migration is recorded with its
/// fingerprints in the transaction that applies it (<see cref="SchemaSealInterceptor"/>). An account that cannot read the
/// histories or the data is refused as such, never taken for a new database; a failure of the engine that stands for
/// missing rights or an unreachable database is turned into its refusal by the catalogue (A-5-10). The contexts of the
/// check and of the migration are those of the migrations (<see cref="DatabaseEngine.CreateMigrationContext"/>): their
/// statements, the catalogue and the wait for the lock have the delay of the migrations, never the ordinary command
/// timeout; the wait for the lock in the check is bounded by that delay on every engine (ADR 041).
/// </summary>
public sealed class SchemaMigrations : ISchemaMigrations
{
    private readonly DatabaseEngine engine;
    private readonly IClock clock;
    private readonly MigrationSources sources;

    private readonly IInterceptor[] interceptors;

    /// <param name="engine">The database.</param>
    /// <param name="clock">The time written with each applied migration.</param>
    /// <param name="interceptors">Added to every context of the check and of the migration (the connections of an account).</param>
    public SchemaMigrations(DatabaseEngine engine, IClock clock, params IInterceptor[] interceptors)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(interceptors);
        this.engine = engine;
        this.clock = clock;
        this.interceptors = interceptors;
        sources = new MigrationSources(engine.MigrationsAssembly);
    }

    /// <summary>The version of the application written with each applied migration.</summary>
    public static string ApplicationVersion => typeof(SchemaMigrations).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public Task<SchemaState> CheckAsync(CancellationToken cancellationToken) => Translated(() => CheckOnceAsync(migrating: false, cancellationToken));

    public Task<SchemaState> MigrateAsync(CancellationToken cancellationToken) => Translated(() => MigrateOnceAsync(cancellationToken));

    /// <summary>
    /// A failure of the engine that the catalogue reads as a refusal (rights, unreachable database) becomes that refusal
    /// (A-5-10), with the numbers of the engine as its inner exception, never its message (ADR 036).
    /// </summary>
    private async Task<SchemaState> Translated(Func<Task<SchemaState>> run)
    {
        try
        {
            return await run();
        }
        catch (Exception failure) when (failure is not SchemaRefusedException && engine.Catalog.RefusalOf(failure) is { } code)
        {
            throw new SchemaRefusedException(code, null, new EngineFailureException(engine.Catalog.NumbersOf(failure)));
        }
    }

    /// <summary>
    /// The state of the database. A database that EF Core says does not exist is behind for the migration, which creates
    /// it; for the check, it is refused with <see cref="SchemaCatalog.AbsentDatabaseRefusal"/> when the provider has one:
    /// on SQL Server, EF Core answers « does not exist » on 4060, which an existing database that the account cannot
    /// open gives as well (ADR 036).
    /// </summary>
    private async Task<SchemaState> CheckOnceAsync(bool migrating, CancellationToken cancellationToken)
    {
        await using var context = engine.CreateMigrationContext(interceptors);
        var known = context.Database.GetMigrations().ToList();
        if (!await context.GetService<IRelationalDatabaseCreator>().ExistsAsync(cancellationToken))
        {
            if (!migrating && engine.Catalog.AbsentDatabaseRefusal is { } refusal)
                throw new SchemaRefusedException(refusal, null);
            return new SchemaState(known.Count == 0 ? SchemaStatus.UpToDate : SchemaStatus.Behind, [], known);
        }
        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await engine.Catalog.CheckAccessAsync(context.Database.GetDbConnection(), cancellationToken);
            // Under a lock that keeps the migrations out: a migration running elsewhere is seen entirely or not at all.
            var connection = context.Database.GetDbConnection();
            using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await using var exclusive = await engine.Catalog.CheckLockAsync(connection, cancellationToken)
                ?? await AcquireLockAsync(context, waiting, cancellationToken);
            var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
            var history = await engine.Catalog.HistoryAsync(connection, null, cancellationToken);
            var current = applied.Count == 0 ? null : Fingerprints.OfLines(await engine.Catalog.DescribeAsync(connection, null, cancellationToken));
            return SchemaVerification.Verify(known, sources, new DatabaseSchema(applied, history, current), MigrationTransactions.OutsideTransaction(context));
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private async Task<SchemaState> MigrateOnceAsync(CancellationToken cancellationToken)
    {
        var state = await CheckOnceAsync(migrating: true, cancellationToken);
        if (state.Pending.Count == 0)
            return state;
        var seal = new SchemaSealInterceptor(engine.Catalog, sources, ApplicationVersion, clock.UtcNow);
        await using (var context = engine.CreateMigrationContext([seal, .. interceptors]))
            await context.Database.MigrateAsync(cancellationToken);
        return await CheckOnceAsync(migrating: true, cancellationToken);
    }

    /// <summary>
    /// The lock of the migrations, waited for at most <see cref="DatabaseEngine.MigrationTimeout"/>: SQL Server bounds
    /// its application lock by the command timeout alone, and EF Core waits without end on SQLite as long as the row of the lock
    /// is there, even when a stopped migration left it. A wait past the delay is <c>BASE_OCCUPEE</c>, transient, or
    /// <c>VERROU_MIGRATIONS</c> when the lock was left by a holder that stopped
    /// (<see cref="SchemaCatalog.LockLeftByAStoppedHolderAsync"/>, ADR 041); the cancellation by the caller stays a
    /// cancellation. The refusal keeps a <see cref="TimeoutException"/> that gives the delay only, never the failure of the
    /// engine, whose message names the server or the file (ADR 036). The delay stops once the lock is held;
    /// <paramref name="waiting"/> lives until the lock is released.
    /// </summary>
    private async Task<IMigrationsDatabaseLock> AcquireLockAsync(DbContext context, CancellationTokenSource waiting, CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var mark = await engine.Catalog.LockMarkAsync(connection, cancellationToken);
        waiting.CancelAfter(engine.MigrationTimeout);
        try
        {
            var exclusive = await context.GetService<IHistoryRepository>().AcquireDatabaseLockAsync(waiting.Token);
            waiting.CancelAfter(Timeout.InfiniteTimeSpan);
            return exclusive;
        }
        catch (Exception) when (waiting.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            var left = await engine.Catalog.LockLeftByAStoppedHolderAsync(connection, mark, cancellationToken);
            throw new SchemaRefusedException(left ? SchemaCodes.MigrationLockHeld : SchemaCodes.DatabaseBusy, null,
                new TimeoutException($"The lock of the migrations was not obtained within {engine.MigrationTimeout}."));
        }
    }
}
