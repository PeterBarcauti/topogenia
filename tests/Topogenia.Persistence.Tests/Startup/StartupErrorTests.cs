// StartupErrorTests.cs
// The failures of connection and of rights that a start in server mode meets, as the local instance gives them (annex A 4, A 5).

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Topogenia.Application.Ports;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.SqlServer;
using Topogenia.Persistence.Tests.Bi;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Startup;

/// <summary>
/// SQL Server only: the numbers are those of SQL Server (the 1.x read 18456, 262 and 1802, annex A 5) and SQLite has no
/// account. The 1.x turned them into a clear message without the technical stack (A-5-10); in 2.0 the message and the stop
/// belong to the host (P4-T06). The persistence part proved here is what the host receives, on the local instance,
/// without creating any login of the server: a connection to a missing database fails with 4060 and 18456, the answer
/// that a login without user in an existing database receives too, so the check refuses a missing database as
/// `BASE_INACCESSIBLE` and never creates it, as the catalogue reads 4060 and 18456 (the 1.x said « not available »,
/// ADR 036); a user without rights on the data or on the schema is refused with 229 or 262, never served. The check run
/// by an account that cannot read the histories or the data is refused with `DROITS_INSUFFISANTS`, never taken for a new
/// database, and the catalogue of SQL Server reads 229, 262 and 1802 as `DROITS_INSUFFISANTS`. A refusal keeps the
/// numbers of the server, never its message, which names the login and the database (spec 5.2). A 1802 (the server
/// cannot create the database) needs rights on the server that a test does not touch: it is read by its number only.
/// </summary>
public sealed class StartupErrorTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    public const int CannotOpenDatabase = 4060;
    public const int LoginFailed = 18456;
    public const int ObjectPermissionDenied = 229;
    public const int DatabasePermissionDenied = 262;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SqlException Server(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException failure)
                return failure;
        }
        throw new InvalidOperationException("No error of the server.", exception);
    }

    /// <summary>
    /// The failure of a load on the missing base, as the server answers its opening. Under the load of several runs, the
    /// server sometimes cuts the connection before its answer reaches the client (233 alone, after the attempts of the
    /// unit): such a failure without answer opens again, a bounded number of times; the first answer of the server is the
    /// one returned, whatever it is (ADR 040, revision).
    /// </summary>
    private static async Task<SqlException> OpeningFailureAsync(TestDatabase missing)
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var session = missing.Session();
            var failure = await Assert.ThrowsAnyAsync<Exception>(() => new WorkspaceStore(session).LoadAsync(Guid.CreateVersion7(), WorkspaceContent.None, Token));
            if (attempt >= TransientFailures.Attempts || !TransientFailures.Unanswered(failure))
                return Server(failure);
            await Task.Delay(TransientFailures.DelayAfter(attempt), Token);
        }
    }

    /// <summary>The numbers of the server that a refusal keeps, without its message.</summary>
    private static IReadOnlyList<int> Numbers(SchemaRefusedException refusal) => Assert.IsType<EngineFailureException>(refusal.InnerException).Numbers;

    [Fact]
    public async Task A_missing_database_is_refused_as_unreachable_by_the_check_never_created_and_refused_to_a_connection_with_4060_and_18456()
    {
        await using var missing = TestDatabase.Create(EngineKind.SqlServer);

        var refusal = await Assert.ThrowsAsync<SchemaRefusedException>(() => new SchemaMigrations(missing.Engine, new FixedClock()).CheckAsync(Token));
        var failure = await OpeningFailureAsync(missing);

        Assert.Equal(SchemaCodes.DatabaseUnreachable, refusal.Code);
        Assert.False(refusal.IsTransient);
        await using (var context = missing.Context())
            Assert.False(await context.Database.CanConnectAsync(Token));
        var numbers = failure.Errors.Cast<SqlError>().Select(error => error.Number).ToList();
        Assert.Contains(CannotOpenDatabase, numbers);
        Assert.Contains(LoginFailed, numbers);
        // The same answer as a connection: what a login without user in an existing database receives too.
        Assert.Equal(refusal.Code, missing.Engine.Catalog.RefusalOf(failure));
    }

    /// <summary>
    /// A login that the instance refuses (18456 alone: no such login, or a wrong password) is the fault of the account, not
    /// of the database: refused for its rights, as the 1.x said « SQL Server refuses this account », never as unreachable,
    /// whose message tells to create the database by `--migrate` (ADR 036, round 3). The login of the test exists nowhere:
    /// no login of the server is created, and the migration creates no database.
    /// </summary>
    [Fact]
    public async Task A_login_refused_by_the_instance_is_refused_for_its_rights_by_the_check_and_the_migration_not_as_unreachable()
    {
        // The instance refuses the login before it looks at the database: a name of a test database never created is enough.
        await using var any = TestDatabase.Create(EngineKind.SqlServer);
        var refused = new SqlConnectionStringBuilder(TestServer.ConnectionString(any.Name))
        {
            IntegratedSecurity = false,
            UserID = RoleSession.NewUser("refused"),
            Password = Guid.CreateVersion7().ToString("N"),
        };
        var migrations = new SchemaMigrations(new SqlServerEngine(refused.ConnectionString), new FixedClock());

        var check = await Assert.ThrowsAsync<SchemaRefusedException>(() => migrations.CheckAsync(Token));
        var migration = await Assert.ThrowsAsync<SchemaRefusedException>(() => migrations.MigrateAsync(Token));

        Assert.All(new[] { check, migration }, refusal =>
        {
            Assert.Equal(SchemaCodes.InsufficientRights, refusal.Code);
            Assert.False(refusal.IsTransient);
            Assert.Equal([LoginFailed], Numbers(refusal));
            Assert.DoesNotContain(refused.UserID, refusal.ToString(), StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task A_user_without_rights_on_the_data_is_refused_its_reading_with_229_and_sees_no_history_of_the_schema()
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        var reports = await ImpersonatedConnections.MemberOfAsync(database, SqlServerAccess.ReportRole, RoleSession.NewUser("bi"));

        await using var session = database.Session(null, reports);
        var reading = Server(await Assert.ThrowsAnyAsync<Exception>(() => new WorkspaceStore(session).LoadAsync(Guid.CreateVersion7(), WorkspaceContent.None, Token)));
        await using var context = database.Engine.CreateContext(reports);
        var applied = await context.Database.GetAppliedMigrationsAsync(Token);
        await context.Database.OpenConnectionAsync(Token);
        IReadOnlyList<SchemaHistoryEntry> history;
        try
        {
            history = await database.Engine.Catalog.HistoryAsync(context.Database.GetDbConnection(), null, Token);
        }
        finally
        {
            // Closed by EF Core, so that the impersonation is reverted before the connection goes back to the pool.
            await context.Database.CloseConnectionAsync();
        }

        Assert.Equal(ObjectPermissionDenied, reading.Number);
        // The histories are hidden from this user, not refused: read as they are, they would make the database look new.
        Assert.Empty(applied);
        Assert.Empty(history);
        Assert.Equal(SchemaCodes.InsufficientRights, database.Engine.Catalog.RefusalOf(reading));
    }

    [Fact]
    public async Task The_check_and_the_migration_run_by_an_account_that_cannot_read_the_histories_are_refused_for_its_rights()
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        var reports = await ImpersonatedConnections.MemberOfAsync(database, SqlServerAccess.ReportRole, RoleSession.NewUser("bi"));
        var nobody = RoleSession.NewUser("none");
        await using (var owner = database.Context())
        {
            await owner.Database.OpenConnectionAsync(Token);
            await SqlServerAccessProbe.CreateUserAsync(owner.Database.GetDbConnection(), nobody, Token);
        }

        foreach (var account in new[] { reports, new ImpersonatedConnections(nobody) })
        {
            var migrations = new SchemaMigrations(database.Engine, new FixedClock(), account);
            var check = await Assert.ThrowsAsync<SchemaRefusedException>(() => migrations.CheckAsync(Token));
            var migration = await Assert.ThrowsAsync<SchemaRefusedException>(() => migrations.MigrateAsync(Token));
            Assert.Equal((SchemaCodes.InsufficientRights, ObjectPermissionDenied), (check.Code, Numbers(check)[0]));
            Assert.Equal(SchemaCodes.InsufficientRights, migration.Code);
            // The message of the server names the database (and, for a login, the account): the refusal keeps only the
            // numbers, for logs that hold identifiers and codes only (spec 5.2).
            Assert.All(new[] { check, migration }, refusal =>
                Assert.DoesNotContain(database.Name, refusal.ToString(), StringComparison.OrdinalIgnoreCase));
        }
        Assert.Equal(SchemaStatus.UpToDate, (await new SchemaMigrations(database.Engine, new FixedClock()).CheckAsync(Token)).Status);
    }

    [Fact]
    public async Task The_check_run_by_the_account_of_the_application_accepts_the_database()
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        var application = await ImpersonatedConnections.MemberOfAsync(database, SqlServerAccess.ApplicationRole, RoleSession.NewUser("app"));
        Assert.Equal(SchemaStatus.UpToDate, (await new SchemaMigrations(database.Engine, new FixedClock(), application).CheckAsync(Token)).Status);
    }

    [Fact]
    public async Task The_failures_of_a_start_are_read_as_an_unreachable_database_or_as_missing_rights()
    {
        await using var missing = TestDatabase.Create(EngineKind.SqlServer);
        var unreachable = await OpeningFailureAsync(missing);
        var database = await databases.OfAsync(EngineKind.SqlServer);
        var application = await ImpersonatedConnections.MemberOfAsync(database, SqlServerAccess.ApplicationRole, RoleSession.NewUser("app"));
        await using var context = database.Engine.CreateContext(application);
        var table = new CreateTableOperation { Schema = SqlServerEngine.Schema, Name = "probe_rights" };
        table.Columns.Add(new AddColumnOperation { Schema = SqlServerEngine.Schema, Table = table.Name, Name = "id", ClrType = typeof(int) });
        var refused = await Assert.ThrowsAnyAsync<Exception>(() => OutOfBand.ApplyAsync(context, table));

        Assert.Equal(SchemaCodes.DatabaseUnreachable, missing.Engine.Catalog.RefusalOf(unreachable));
        Assert.Equal((DatabasePermissionDenied, SchemaCodes.InsufficientRights), (Server(refused).Number, database.Engine.Catalog.RefusalOf(refused)));
        Assert.Null(database.Engine.Catalog.RefusalOf(new InvalidOperationException("Not a failure of the engine.")));
    }

    [Fact]
    public async Task The_account_of_the_application_may_not_change_the_schema_and_is_refused_with_262()
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        var application = await ImpersonatedConnections.MemberOfAsync(database, SqlServerAccess.ApplicationRole, RoleSession.NewUser("app"));
        await using var context = database.Engine.CreateContext(application);
        var table = new CreateTableOperation { Schema = SqlServerEngine.Schema, Name = "probe_table" };
        table.Columns.Add(new AddColumnOperation { Schema = SqlServerEngine.Schema, Table = table.Name, Name = "id", ClrType = typeof(int) });

        var failure = Server(await Assert.ThrowsAnyAsync<Exception>(() => OutOfBand.ApplyAsync(context, table)));
        await using var session = database.Session(null, application);
        var loaded = await new WorkspaceStore(session).LoadAsync(Guid.CreateVersion7(), WorkspaceContent.All, Token);

        Assert.Equal(DatabasePermissionDenied, failure.Number);
        Assert.Null(loaded);
    }
}
