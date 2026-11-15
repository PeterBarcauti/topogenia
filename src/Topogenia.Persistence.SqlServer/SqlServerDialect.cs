// SqlServerDialect.cs
// SQL Server side of the unit of work: error numbers read (1205, 2601, 2627, 547, 2628, 8152, 8115) and the isolation of the two transactions (spec 4.3-5, 3.4).

using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Topogenia.Persistence.Errors;
using Topogenia.Persistence.Model;

namespace Topogenia.Persistence.SqlServer;

/// <summary>
/// The errors of SQL Server, read by their number as the 1.x did (`ErreurMetier.cs`, `BaseDeDonnees.cs`, annex A 5): 1205
/// (victim of a deadlock), 2601 and 2627 (unique index, unique or primary key), 547 (foreign key or CHECK, told apart by
/// the name of the guard). The name of the guard is read in the message, whatever the language of the server. A text
/// longer than its column (2628, or 8152 without the column) and an integer beyond its type (8115) are refused by the
/// column type before any CHECK: SQLite refuses the same rows by a CHECK (ADR 038), so they are read as a CHECK too, the
/// text one named after the table and column that 2628 quotes (`CK_table_column_text`). The write
/// transaction is read committed, as in the 1.x: the project lock serialises the structure operations (spec 4.7); with the
/// row versions of the database (READ_COMMITTED_SNAPSHOT), its reads do not wait for the writers. A read outside a unit is
/// a snapshot transaction (spec 3.4, ADR 031): it sees the database as it was at its first read, takes no shared lock,
/// so it neither waits for a writer nor makes one wait. The migration `SnapshotIsolation` sets the two options.
/// </summary>
public sealed partial class SqlServerDialect : EngineDialect
{
    public const int Deadlock = 1205;
    public const int DuplicateKeyRow = 2601;
    public const int UniqueKeyViolation = 2627;
    public const int ConstraintConflict = 547;
    public const int StringTruncated = 2628;
    public const int StringTruncatedUnnamed = 8152;
    public const int ArithmeticOverflow = 8115;

    /// <summary>The two quoted names of 2628, in every language: `'database.schema.table'`, then `'column'`.</summary>
    [GeneratedRegex(@"'(?:[^'.]+\.)*(?<table>[^'.]+)'[^']*'(?<column>[^']+)'")]
    private static partial Regex TruncatedColumn();

    public override async Task<StoreTransaction> BeginWriteAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new StoreTransaction(await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken));
    }

    public override async Task<StoreTransaction> BeginReadAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new StoreTransaction(await context.Database.BeginTransactionAsync(IsolationLevel.Snapshot, cancellationToken));
    }

    protected override StoreError? ClassifyOwn(Exception exception, IModel model)
    {
        if (exception is not SqlException failure)
            return null;
        var name = ConstraintNames.InMessage(failure.Message);
        return failure.Number switch
        {
            Deadlock => new StoreError(StoreErrorKind.Deadlock, null),
            DuplicateKeyRow or UniqueKeyViolation => new StoreError(StoreErrorKind.Unique, name),
            ConstraintConflict => new StoreError(ConstraintNames.IsForeignKey(name) ? StoreErrorKind.ForeignKey : StoreErrorKind.Check, name),
            StringTruncated => new StoreError(StoreErrorKind.Check, TextGuardOf(failure.Message, model)),
            StringTruncatedUnnamed or ArithmeticOverflow => new StoreError(StoreErrorKind.Check, null),
            _ => null,
        };
    }

    /// <summary>The text guard of the column that a message 2628 names, when the model has it, or null.</summary>
    private static string? TextGuardOf(string message, IModel model)
    {
        var match = TruncatedColumn().Match(message);
        if (!match.Success)
            return null;
        var (table, column) = (match.Groups["table"].Value, match.Groups["column"].Value);
        var guarded = model.GetEntityTypes().Where(entity => entity.GetTableName() == table).SelectMany(entity => entity.GetProperties())
            .Any(property => property.GetColumnName() == column && ModelGuards.IsStoredAsText(property));
        return guarded ? ModelGuards.TextCheckName(table, column) : null;
    }
}
