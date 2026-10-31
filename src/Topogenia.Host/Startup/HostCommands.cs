// HostCommands.cs
// The command line of the executable: --migrate (server mode), --check (configuration and database), --export and --import, run without serving any request (spec 6.3).

using Microsoft.Extensions.Options;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;
using Topogenia.Host.Composition;
using Topogenia.Host.Desktop;
using Topogenia.Host.Server;
using Topogenia.Persistence.Sqlite;
using Topogenia.Persistence.Sqlite.Files;

namespace Topogenia.Host.Startup;

/// <summary>
/// The application is built as for a run (same configuration, same services), but the server is never started: the
/// command uses the services, writes its messages to the console in the language of the user, and gives the exit code.
/// <list type="bullet">
/// <item><c>--migrate</c> (server mode only, A-4-38): applies the pending migrations of the SQL Server database, creating
/// it when it does not exist, under the account that runs the command, an account of its own (spec 5.3); the desktop mode
/// migrates its data file at the start, after its backup.</item>
/// <item><c>--check</c> (both modes): the configuration of the mode, then the database, read without any change: the
/// refusals of the start, and in server mode a database behind; a desktop data file still to create or to migrate is
/// usable (the start does it). A desktop data file of a running application (its named lock, <see cref="DesktopInstance"/>)
/// or held open by another program is not opened (ADR 029). The content of the file never changes; closing it writes its
/// log back into it, as a clean stop does.</item>
/// <item><c>--export</c> and <c>--import</c> (both modes, ADR 055): one package written or read under the rights of the
/// account that runs the command (`ExchangeCommands.cs`).</item>
/// </list>
/// Exit codes: <see cref="Success"/>, <see cref="Problem"/>, <see cref="Usage"/> (more than one command). The logs get
/// codes, numbers of the engine and types, never a path nor a message of an exception (ADR 036).
/// </summary>
public static partial class HostCommands
{
    public const string Migrate = "--migrate";

    public const string Check = "--check";

    public const int Success = 0;

    public const int Problem = 1;

    public const int Usage = 2;

    private static readonly string[] Commands = [Migrate, Check, Export, Import];

    /// <summary>
    /// The commands named in <paramref name="args"/> and the other arguments, left to the configuration (the operands and
    /// options of an exchange command are not among them); null without any command.
    /// </summary>
    public static (IReadOnlyList<string> Commands, string[] Others)? Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var commands = args.Where(arg => Commands.Contains(arg, StringComparer.OrdinalIgnoreCase)).Select(arg => arg.ToLowerInvariant()).ToList();
        return commands.Count == 0 ? null : (commands, [.. SplitExchange(args).Others.Where(arg => !Commands.Contains(arg, StringComparer.OrdinalIgnoreCase))]);
    }

    /// <summary>Runs the commands of <paramref name="args"/> (see <see cref="Parse"/>); <paramref name="configure"/> lets a test add its configuration.</summary>
    public static async Task<int> RunAsync(string[] args, TextWriter output, Action<WebApplicationBuilder>? configure, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        var parsed = Parse(args) ?? throw new ArgumentException("No command in the arguments.", nameof(args));
        await using var application = HostApplication.Build(parsed.Others, configure);
        var services = application.Services;
        var console = new StartupConsole(services.GetRequiredService<LanguageSettings>(), output, TextReader.Null, interactive: false);
        if (parsed.Commands.Distinct().Count() > 1)
        {
            console.Write("COMMANDES_MULTIPLES");
            return Usage;
        }
        var options = services.GetRequiredService<IOptions<ProductOptions>>().Value;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(HostCommands).FullName!);
        try
        {
            if (parsed.Commands[0] == Migrate)
                return await MigrateAsync(services, options, console, logger, cancellationToken);
            if (SplitExchange(args).Exchange is { } exchange)
                return await ExchangeAsync(services, options, exchange, console, logger, cancellationToken);
            return options.Mode == HostMode.Server
                ? await CheckServerAsync(services, options, console, cancellationToken)
                : await CheckDesktopAsync(services, console, logger, cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            // Never a stack nor a message at the console or in the log: they may name a file, a base or an account.
            LogFailed(logger, failure.GetType().FullName ?? failure.GetType().Name, "");
            console.Write(ErrorCodes.InternalError);
            return Problem;
        }
    }

    private static async Task<int> MigrateAsync(IServiceProvider services, ProductOptions options, StartupConsole console, ILogger logger,
        CancellationToken cancellationToken)
    {
        if (options.Mode != HostMode.Server)
        {
            console.Write("MIGRATE_MODE_POSTE");
            return Problem;
        }
        // The names of the server are needed to serve, not to migrate.
        if (ServerSettings.Refusal(options) is { Code: not StartupCodes.InvalidAllowedHosts } refusal)
        {
            console.Write(refusal.MessageKey, refusal.Details);
            return Problem;
        }
        var migrations = services.GetRequiredService<ISchemaMigrations>();
        try
        {
            var before = await AppliedBeforeAsync(migrations, cancellationToken);
            var after = await MigrateRetriedAsync(migrations, logger, cancellationToken);
            var applied = after.Applied.Count - before;
            LogMigrated(logger, applied);
            console.Write(applied == 0 ? "MIGRATION_INUTILE" : "MIGRATION_FAITE", new Dictionary<string, object?> { ["count"] = applied });
            return Success;
        }
        catch (SchemaRefusedException refused)
        {
            var numbers = refused.InnerException is EngineFailureException failure ? failure.Numbers : [];
            LogRefused(logger, refused.Code, refused.Migration ?? "", string.Join(",", numbers));
            console.Write(ServerDatabase.MessageOf(refused.Code, numbers),
                refused.Migration is null ? null : new Dictionary<string, object?> { ["migration"] = refused.Migration });
            return Problem;
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            var engine = services.GetRequiredService<Persistence.Schema.DatabaseEngine>();
            LogFailed(logger, failure.GetType().FullName ?? failure.GetType().Name, string.Join(",", engine.Catalog.NumbersOf(failure)));
            console.Write(ErrorCodes.InternalError);
            return Problem;
        }
    }

    /// <summary>The attempts of <c>--migrate</c> on a passing refusal (BASE_OCCUPEE: `model` locked by another creation, a deadlock).</summary>
    public const int MigrateAttempts = 4;

    /// <summary>
    /// The migration, run again on a passing refusal after 2, 4 then 8 seconds: an administrator runs the command once,
    /// a creation of the base can meet another one on the instance (1807). Any other refusal goes up at once.
    /// </summary>
    private static async Task<SchemaState> MigrateRetriedAsync(ISchemaMigrations migrations, ILogger logger, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await migrations.MigrateAsync(cancellationToken);
            }
            catch (SchemaRefusedException refused) when (refused.IsTransient && attempt < MigrateAttempts)
            {
                LogRetried(logger, refused.Code, attempt);
                await Task.Delay(TimeSpan.FromSeconds(1 << attempt), cancellationToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Command --migrate: passing refusal {Code} at attempt {Attempt}, run again.")]
    private static partial void LogRetried(ILogger logger, string code, int attempt);

    /// <summary>The migrations applied before <c>--migrate</c>: none on a database that does not exist yet, which the migration creates.</summary>
    private static async Task<int> AppliedBeforeAsync(ISchemaMigrations migrations, CancellationToken cancellationToken)
    {
        try
        {
            return (await migrations.CheckAsync(cancellationToken)).Applied.Count;
        }
        catch (SchemaRefusedException refused) when (refused.Code == SchemaCodes.DatabaseUnreachable)
        {
            return 0;
        }
    }

    private static async Task<int> CheckServerAsync(IServiceProvider services, ProductOptions options, StartupConsole console, CancellationToken cancellationToken)
    {
        if (await ServerProblemAsync(services, options, console, cancellationToken))
            return Problem;
        console.Write("VERIFICATION_REUSSIE");
        return Success;
    }

    /// <summary>The settings of the server mode, then the check of its start: true, the refusal written, when one fails.</summary>
    private static async Task<bool> ServerProblemAsync(IServiceProvider services, ProductOptions options, StartupConsole console, CancellationToken cancellationToken)
    {
        try
        {
            if (ServerSettings.Refusal(options) is { } refusal)
                throw refusal;
            await services.GetRequiredService<ServerDatabase>().CheckAsync(cancellationToken);
            return false;
        }
        catch (StartupRefusedException refused)
        {
            console.Write(refused.MessageKey, refused.Details);
            return true;
        }
    }

    private static async Task<int> CheckDesktopAsync(IServiceProvider services, StartupConsole console, ILogger logger, CancellationToken cancellationToken)
    {
        var paths = services.GetRequiredService<DesktopPaths>();
        var environment = services.GetRequiredService<DataFileEnvironment>();
        if (DesktopDataFile.Check(paths.DataFile, environment) is { } refusal)
        {
            console.Write(refusal.Code, new Dictionary<string, object?> { ["place"] = refusal.Place });
            return Problem;
        }
        var instance = services.GetRequiredService<DesktopInstance>();
        if (!instance.TryAcquire() || DataFileUse.HeldOpen(paths.DataFile) is not null)
        {
            console.Write("VERIFICATION_FICHIER_OUVERT");
            return Problem;
        }
        try
        {
            var result = File.Exists(paths.DataFile)
                ? await CheckDataFileAsync(services, console, logger, cancellationToken)
                : Success;
            if (result != Success)
                return result;
            if (!File.Exists(paths.DataFile))
                console.Write("VERIFICATION_FICHIER_A_CREER");
            var exposed = DesktopDatabase.ExposedPlaces(paths, environment);
            foreach (var place in exposed)
                LogExposed(logger, place);
            StartupService.WarnOfExposedFolders(console, paths, exposed);
            return Success;
        }
        finally
        {
            instance.Release();
        }
    }

    /// <summary>The schema of an existing data file, read by the check of the start; the engine is closed after, its log written back.</summary>
    private static async Task<int> CheckDataFileAsync(IServiceProvider services, StartupConsole console, ILogger logger, CancellationToken cancellationToken)
    {
        var engine = services.GetRequiredService<SqliteEngine>();
        try
        {
            var state = await services.GetRequiredService<ISchemaMigrations>().CheckAsync(cancellationToken);
            if (state.Status == SchemaStatus.Behind)
                console.Write("VERIFICATION_FICHIER_A_MIGRER", new Dictionary<string, object?> { ["count"] = state.Pending.Count });
            else
                console.Write("VERIFICATION_REUSSIE");
            return Success;
        }
        catch (SchemaRefusedException refused)
        {
            var numbers = refused.InnerException is EngineFailureException failure ? failure.Numbers : [];
            LogRefused(logger, refused.Code, refused.Migration ?? "", string.Join(",", numbers));
            console.Write(refused.Code, refused.Migration is null ? null : new Dictionary<string, object?> { ["migration"] = refused.Migration });
            return Problem;
        }
        finally
        {
            await CloseAsync(engine, logger, cancellationToken);
        }
    }

    /// <summary>
    /// The close writes the log back into the file, as a clean stop (the content does not change); on a file the check has
    /// just refused (damaged, read-only), it may fail in turn: the refusal stands, the failure is logged by its type.
    /// </summary>
    private static async Task CloseAsync(SqliteEngine engine, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            await engine.CloseAsync(cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            LogCloseFailed(logger, failure.GetType().FullName ?? failure.GetType().Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Command --check: the folder of the {Place} is readable by the other accounts of the machine.")]
    private static partial void LogExposed(ILogger logger, string place);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Command --check: the close of the data file failed: {Type}.")]
    private static partial void LogCloseFailed(ILogger logger, string type);

    [LoggerMessage(Level = LogLevel.Information, Message = "Command --migrate: {Count} migrations applied.")]
    private static partial void LogMigrated(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Command refused by the schema check: {Code} {Migration} (engine {Engine}).")]
    private static partial void LogRefused(ILogger logger, string code, string migration, string engine);

    [LoggerMessage(Level = LogLevel.Error, Message = "Command failed: {Type} (engine {Engine}).")]
    private static partial void LogFailed(ILogger logger, string type, string engine);
}
