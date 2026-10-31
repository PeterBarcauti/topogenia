// SchemaRefusedException.cs
// Refusal of a database whose schema the application must not use, its stable codes, and the failure of the engine behind it reduced to its numbers (spec 4.4, annex A 4).

using System.Globalization;

namespace Topogenia.Application.Ports;

/// <summary>Stable codes of the refusals of the schema (spec 4.4); the host translates them into a clear message (AR-15).</summary>
public static class SchemaCodes
{
    /// <summary>The database holds a migration that the application does not know: install the matching version.</summary>
    public const string NewerDatabase = "BASE_PLUS_RECENTE";

    /// <summary>An applied migration differs from the one of the application since it was applied.</summary>
    public const string MigrationChanged = "MIGRATION_MODIFIEE";

    /// <summary>The schema differs from the one the migrations left (changed outside the migrations).</summary>
    public const string SchemaChanged = "SCHEMA_MODIFIE";

    /// <summary>An applied migration has no fingerprint (applied without the application, or its record was removed).</summary>
    public const string FingerprintMissing = "EMPREINTE_ABSENTE";

    /// <summary>Server mode: the database is behind the application and must be migrated by `--migrate` (spec 4.4).</summary>
    public const string DatabaseBehind = "BASE_EN_RETARD";

    /// <summary>
    /// The account cannot read the histories of the migrations or the data, may not do what the migration needs, or is
    /// refused by the instance (SQL Server 18456 alone): its rights, not the database, are at fault (A-5-10). The 1.x read
    /// 262 and 1802 so, and 18456 as a refused account, by the first number of the failure only (annex A 5); 2.0 reads 229
    /// too (ADR 036).
    /// </summary>
    public const string InsufficientRights = "DROITS_INSUFFISANTS";

    /// <summary>
    /// The database cannot be opened by the account: database that it cannot open (SQL Server 4060, with 18456), or
    /// database that does not exist at the check of the server mode (ADR 036). The 1.x read every first number other than
    /// the rights and 18456 (4060 for a database that it could not open) as a database not available (A-5-10).
    /// </summary>
    public const string DatabaseUnreachable = "BASE_INACCESSIBLE";

    /// <summary>
    /// A contention of the engine stopped the check or the migration (a lock not obtained, a deadlock, a broken connection,
    /// SQL Server 1807 with 1802 for instance): nothing is wrong with the database nor the rights, the same run succeeds
    /// later (ADR 036). The only transient code.
    /// </summary>
    public const string DatabaseBusy = "BASE_OCCUPEE";

    /// <summary>
    /// Desktop mode: the lock of the migrations was not obtained within the delay of the migrations, and nothing shows a
    /// holder alive: its row stayed through the whole delay and nobody writes the file. The lock of SQLite is a row that
    /// outlives its holder: a migration stopped without removing it leaves it, and it never goes by itself, so the same run
    /// fails again (ADR 041). A holder seen alive gives <see cref="DatabaseBusy"/> instead. Not transient: the host tells to
    /// close any other instance first, then, if none runs, to remove the row (P4-T05).
    /// </summary>
    public const string MigrationLockHeld = "VERROU_MIGRATIONS";
}

/// <summary>
/// The failure of the engine behind a refusal, kept as the inner exception of <see cref="SchemaRefusedException"/> and
/// reduced to its numbers: the message of the engine names the login, the database or the file, which the logs must not
/// hold (spec 5.2: identifiers and codes only, ADR 036).
/// </summary>
public sealed class EngineFailureException : Exception
{
    public EngineFailureException(IReadOnlyList<int> numbers)
        : base(Text(numbers))
    {
        Numbers = [.. numbers];
    }

    public EngineFailureException()
        : this([])
    {
    }

    /// <summary>The numbers of the errors of the engine, in its order (SQLite: the result code, then the extended code).</summary>
    public IReadOnlyList<int> Numbers { get; }

    private static string Text(IReadOnlyList<int> numbers)
    {
        ArgumentNullException.ThrowIfNull(numbers);
        return "Engine errors: " + string.Join(", ", numbers.Select(number => number.ToString(CultureInfo.InvariantCulture))) + ".";
    }
}

/// <summary>A database refused by the schema check, with the code of <see cref="SchemaCodes"/> and the migration concerned.</summary>
public sealed class SchemaRefusedException : Exception
{
    public SchemaRefusedException(string code, string? migration)
        : base(migration is null ? code : $"{code}: {migration}")
    {
        Code = code;
        Migration = migration;
    }

    public SchemaRefusedException()
        : this(SchemaCodes.SchemaChanged, null)
    {
    }

    /// <summary>
    /// A refusal that translates a failure of the engine, kept as the inner exception for the logs of the host: an
    /// <see cref="EngineFailureException"/> (numbers only) or a <see cref="TimeoutException"/> that gives the delay.
    /// </summary>
    public SchemaRefusedException(string code, string? migration, Exception inner)
        : base(migration is null ? code : $"{code}: {migration}", inner)
    {
        Code = code;
        Migration = migration;
    }

    public string Code { get; }

    /// <summary>True for <see cref="SchemaCodes.DatabaseBusy"/>: the check or the migration can run again as it is.</summary>
    public bool IsTransient => Code == SchemaCodes.DatabaseBusy;

    /// <summary>The migration that holds the refusal, when there is one.</summary>
    public string? Migration { get; }
}
