// ServerDatabase.cs
// The check of the SQL Server database at the start of the server mode: never migrated by the host, each refusal by its code and its message (spec 4.4, 5.3).

using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Host.Startup;
using Topogenia.Persistence.Schema;

namespace Topogenia.Host.Server;

/// <summary>
/// Checks the database before the first request; the server mode never migrates it (A-4-38: `--migrate` does, under an
/// account of its own, spec 5.3):
/// <list type="bullet">
/// <item>a database behind the application is refused BASE_EN_RETARD;</item>
/// <item>a schema the application must not use is refused with its code of <see cref="SchemaCodes"/> (A-4-36, A-4-37), with
/// the message of the server mode: a database and its account, not a data file (<see cref="MessageOf"/>);</item>
/// <item>any other failure of the engine is refused ERREUR_INTERNE.</item>
/// </list>
/// The logs get the code, the migration and the numbers of the engine, or the type of the failure: never its message nor
/// its <c>ToString()</c>, which name the server, the database or the login (ADR 036).
/// </summary>
public sealed partial class ServerDatabase(ISchemaMigrations migrations, DatabaseEngine engine, ILogger<ServerDatabase> logger)
{
    /// <summary>The prefix of the messages of the server mode in `Startup/messages.*.json`.</summary>
    public const string MessagePrefix = "SERVEUR_";

    /// <summary>SQL Server refuses the connection of the account itself (18456 alone, ADR 036).</summary>
    public const string ConnectionRefusedMessage = MessagePrefix + "CONNEXION_REFUSEE";

    private const int LoginFailed = 18456;

    private static readonly string[] OwnMessages =
    [
        SchemaCodes.NewerDatabase, SchemaCodes.MigrationChanged, SchemaCodes.SchemaChanged, SchemaCodes.FingerprintMissing,
        SchemaCodes.InsufficientRights, SchemaCodes.DatabaseUnreachable,
    ];

    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        SchemaState state;
        try
        {
            state = await migrations.CheckAsync(cancellationToken);
        }
        catch (SchemaRefusedException refused)
        {
            var numbers = refused.InnerException is EngineFailureException failure ? failure.Numbers : [];
            LogSchemaRefused(logger, refused.Code, refused.Migration ?? "", refused.IsTransient, string.Join(",", numbers));
            throw new StartupRefusedException(refused.Code, refused.Migration is null ? null : new Dictionary<string, object?> { ["migration"] = refused.Migration })
            {
                MessageKey = MessageOf(refused.Code, numbers),
            };
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            LogEngineFailure(logger, failure.GetType().FullName ?? failure.GetType().Name, string.Join(",", engine.Catalog.NumbersOf(failure)));
            throw new StartupRefusedException(ErrorCodes.InternalError);
        }
        if (state.Status == SchemaStatus.Behind)
        {
            LogBehind(logger, state.Pending.Count);
            throw new StartupRefusedException(SchemaCodes.DatabaseBehind);
        }
    }

    /// <summary>
    /// The message of a refusal of the schema in the server mode: the one of the code when the desktop sentence speaks of
    /// a data file, the refused connection for <see cref="SchemaCodes.InsufficientRights"/> with 18456 alone.
    /// </summary>
    public static string MessageOf(string code, IReadOnlyList<int> numbers)
    {
        ArgumentNullException.ThrowIfNull(numbers);
        if (code == SchemaCodes.InsufficientRights && numbers.Contains(LoginFailed))
            return ConnectionRefusedMessage;
        return OwnMessages.Contains(code) ? MessagePrefix + code : code;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Database refused by the schema check: {Code} {Migration} (transient {Transient}; engine {Engine}).")]
    private static partial void LogSchemaRefused(ILogger logger, string code, string migration, bool transient, string engine);

    [LoggerMessage(Level = LogLevel.Error, Message = "Database behind the application: {Count} migrations to apply with --migrate.")]
    private static partial void LogBehind(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Schema check failed: {Type} (engine {Engine}).")]
    private static partial void LogEngineFailure(ILogger logger, string type, string engine);
}
