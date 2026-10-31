// ServerStartupTests.cs
// The start of the server mode: refused without its settings, a database checked and never migrated, each refusal with its message and logged without the engine (spec 4.4, 5.3, 6.2).

using Microsoft.Extensions.DependencyInjection;
using Topogenia.Application.Ports;
using Topogenia.Host.Composition;
using Topogenia.Host.Server;
using Topogenia.Host.Startup;
using Topogenia.Host.Tests.Desktop;
using Topogenia.Persistence.SqlServer;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Server;

public sealed class ServerStartupTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ProductOptions Valid() => new()
    {
        Mode = HostMode.Server,
        Database = new DatabaseOptions { Provider = DatabaseKind.SqlServer, ConnectionString = "Server=.;Database=Topogenia_Unused;Integrated Security=true" },
        Server = new ServerOptions { AllowedHosts = ["topogenia.test"] },
    };

    public static TheoryData<string[], string?> AllowedHosts => new()
    {
        { [], StartupCodes.InvalidAllowedHosts },
        { ["*"], StartupCodes.InvalidAllowedHosts },
        { ["*.example.test"], StartupCodes.InvalidAllowedHosts },
        { ["topogenia.test", " "], StartupCodes.InvalidAllowedHosts },
        { ["topogenia.test", "topogenia"], null },
    };

    [Theory]
    [MemberData(nameof(AllowedHosts))]
    public void The_server_mode_needs_the_explicit_list_of_its_names_without_a_wildcard(string[] hosts, string? refused)
    {
        var options = Valid();
        options.Server.AllowedHosts = [.. hosts];
        Assert.Equal(refused, ServerSettings.Refusal(options)?.Code);
    }

    [Fact]
    public void The_server_mode_needs_sql_server_and_its_connection_string()
    {
        var sqlite = Valid();
        sqlite.Database.Provider = DatabaseKind.Sqlite;
        var blank = Valid();
        blank.Database.ConnectionString = " ";
        Assert.Equal(StartupCodes.ServerModeWithSqlite, ServerSettings.Refusal(sqlite)?.Code);
        Assert.Equal(StartupCodes.MissingConnectionString, ServerSettings.Refusal(blank)?.Code);
        Assert.Null(ServerSettings.Refusal(Valid()));
    }

    [Theory]
    [InlineData(0, StartupCodes.InvalidMigrationTimeout)]
    [InlineData(1, null)]
    [InlineData(1440, null)]
    [InlineData(1441, StartupCodes.InvalidMigrationTimeout)]
    public void The_delay_of_the_migrations_is_from_one_minute_to_one_day(int minutes, string? refused)
    {
        var options = Valid();
        options.Database.MigrationTimeoutMinutes = minutes;
        Assert.Equal(refused, ServerSettings.Refusal(options)?.Code);
    }

    [Fact]
    public async Task The_engine_of_the_server_mode_gets_the_delay_of_the_migrations_of_the_configuration()
    {
        await using var run = await ServerRun.StartAsync(new Dictionary<string, string?> { [$"{ProductOptions.Section}:Database:MigrationTimeoutMinutes"] = "45" });
        Assert.Equal(TimeSpan.FromMinutes(45), run.Services.GetRequiredService<Persistence.Schema.DatabaseEngine>().MigrationTimeout);
    }

    [Fact]
    public async Task The_whole_host_refuses_to_start_without_allowed_hosts_or_without_connection_string()
    {
        await using var database = await TestDatabase.MigratedAsync(EngineKind.SqlServer);
        var section = ProductOptions.Section;
        var wildcard = await Assert.ThrowsAsync<StartupRefusedException>(() => ServerRun.StartAsync(database,
            new Dictionary<string, string?> { [$"{section}:Server:AllowedHosts:0"] = "*" }, ownsDatabase: false));
        Assert.Equal(StartupCodes.InvalidAllowedHosts, wildcard.Code);
        var connection = await Assert.ThrowsAsync<StartupRefusedException>(() => ServerRun.StartAsync(database,
            new Dictionary<string, string?> { [$"{section}:Database:ConnectionString"] = "" }, ownsDatabase: false));
        Assert.Equal(StartupCodes.MissingConnectionString, connection.Code);
    }

    [Fact]
    public async Task A_database_behind_is_refused_and_never_migrated_by_the_start()
    {
        await using var database = await TestDatabase.CreatedAsync(EngineKind.SqlServer);
        var refusal = await Assert.ThrowsAsync<StartupRefusedException>(() => ServerRun.StartAsync(database, null, ownsDatabase: false));
        Assert.Equal((SchemaCodes.DatabaseBehind, SchemaCodes.DatabaseBehind), (refusal.Code, refusal.MessageKey));
        var state = await new Persistence.Schema.SchemaMigrations(database.Engine, new FixedClock()).CheckAsync(Token);
        Assert.Equal((SchemaStatus.Behind, 0), (state.Status, state.Applied.Count));
    }

    [Fact]
    public async Task A_missing_database_is_refused_unreachable_with_the_message_of_the_server_and_never_created()
    {
        await using var database = TestDatabase.Create(EngineKind.SqlServer);
        var refusal = await Assert.ThrowsAsync<StartupRefusedException>(() => ServerRun.StartAsync(database, null, ownsDatabase: false));
        Assert.Equal((SchemaCodes.DatabaseUnreachable, "SERVEUR_BASE_INACCESSIBLE"), (refusal.Code, refusal.MessageKey));
        Assert.DoesNotContain(database.Name, refusal.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(await TestServer.ExistsAsync(database.Name, Token));
    }

    public static TheoryData<string, int[], string> Messages => new()
    {
        { SchemaCodes.NewerDatabase, [], "SERVEUR_BASE_PLUS_RECENTE" },
        { SchemaCodes.MigrationChanged, [], "SERVEUR_MIGRATION_MODIFIEE" },
        { SchemaCodes.SchemaChanged, [], "SERVEUR_SCHEMA_MODIFIE" },
        { SchemaCodes.FingerprintMissing, [], "SERVEUR_EMPREINTE_ABSENTE" },
        { SchemaCodes.InsufficientRights, [229], "SERVEUR_DROITS_INSUFFISANTS" },
        { SchemaCodes.InsufficientRights, [18456], ServerDatabase.ConnectionRefusedMessage },
        { SchemaCodes.DatabaseUnreachable, [4060, 18456], "SERVEUR_BASE_INACCESSIBLE" },
        { SchemaCodes.DatabaseBusy, [1205], SchemaCodes.DatabaseBusy },
    };

    [Theory]
    [MemberData(nameof(Messages))]
    public async Task A_schema_refused_stops_the_start_with_its_code_the_message_of_the_server_and_the_numbers_of_the_engine_in_the_log(
        string code, int[] numbers, string message)
    {
        var migrations = new RefusingMigrations(new SchemaRefusedException(code, "Initial", new EngineFailureException(numbers)));
        var logger = new CapturingLogger<ServerDatabase>();
        var database = new ServerDatabase(migrations, new SqlServerEngine("Server=.;Database=Topogenia_Unused"), logger);
        var refusal = await Assert.ThrowsAsync<StartupRefusedException>(() => database.CheckAsync(Token));
        Assert.Equal((code, message, (object?)"Initial"), (refusal.Code, refusal.MessageKey, refusal.Details["migration"]));
        var line = Assert.Single(logger.Lines);
        Assert.Contains(code, line, StringComparison.Ordinal);
        Assert.Contains(string.Join(",", numbers), line, StringComparison.Ordinal);
        Assert.DoesNotContain("Topogenia_Unused", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Another_failure_of_the_check_is_an_internal_error_logged_by_its_type_only()
    {
        var migrations = new RefusingMigrations(new InvalidOperationException("Login failed for user FICTIF\\secret on Topogenia_Unused."));
        var logger = new CapturingLogger<ServerDatabase>();
        var database = new ServerDatabase(migrations, new SqlServerEngine("Server=.;Database=Topogenia_Unused"), logger);
        var refusal = await Assert.ThrowsAsync<StartupRefusedException>(() => database.CheckAsync(Token));
        Assert.Equal("ERREUR_INTERNE", refusal.Code);
        var line = Assert.Single(logger.Lines);
        Assert.Contains(typeof(InvalidOperationException).FullName!, line, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", line, StringComparison.Ordinal);
        Assert.DoesNotContain("Topogenia_Unused", line, StringComparison.Ordinal);
    }

    private sealed class RefusingMigrations(Exception failure) : ISchemaMigrations
    {
        public Task<SchemaState> CheckAsync(CancellationToken cancellationToken) => throw failure;

        public Task<SchemaState> MigrateAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("The start of the server mode never migrates.");
    }
}
