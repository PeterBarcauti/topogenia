// SqliteDialect.cs
// SQLite side of the unit of work: result codes read (busy, locked, constraints) and the two transactions, immediate and deferred (spec 4.3-5, 3.4).

using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Topogenia.Persistence.Errors;

namespace Topogenia.Persistence.Sqlite;

/// <summary>
/// The errors of SQLite, read by their result codes: `SQLITE_BUSY` (5) and `SQLITE_LOCKED` (6) for a database held by
/// another connection, `SQLITE_CONSTRAINT` (19) told apart by its extended code (unique 2067, primary key 1555, foreign
/// key 787, check 275). SQLite names a CHECK in its message, gives the table and columns of a unique index or key (mapped
/// back to its name by the model) and names no foreign key. The write transaction is immediate: it takes the write lock
/// of the database at once, so that the lock of the project serialises the units (spec 4.7) and a unit never fails to
/// upgrade a read into a write. A read outside a unit is a deferred transaction, which sees the database as it was at
/// its first read until it ends (ADR 031).
/// </summary>
public sealed partial class SqliteDialect : EngineDialect
{
    public const int Busy = 5;
    public const int Locked = 6;
    public const int Constraint = 19;
    public const int ConstraintCheck = 275;
    public const int ConstraintForeignKey = 787;
    public const int ConstraintPrimaryKey = 1555;
    public const int ConstraintUnique = 2067;

    [GeneratedRegex(@"UNIQUE constraint failed: (?<columns>[\w\.]+(?:, [\w\.]+)*)")]
    private static partial Regex UniqueColumns();

    public override Task<StoreTransaction> BeginWriteAsync(AppDbContext context, CancellationToken cancellationToken) =>
        BeginAsync(context, deferred: false, cancellationToken);

    public override Task<StoreTransaction> BeginReadAsync(AppDbContext context, CancellationToken cancellationToken) =>
        BeginAsync(context, deferred: true, cancellationToken);

    protected override StoreError? ClassifyOwn(Exception exception, IModel model)
    {
        if (exception is not SqliteException failure)
            return null;
        if (failure.SqliteErrorCode is Busy or Locked)
            return new StoreError(StoreErrorKind.Busy, null);
        if (failure.SqliteErrorCode != Constraint)
            return null;
        return failure.SqliteExtendedErrorCode switch
        {
            ConstraintUnique or ConstraintPrimaryKey => new StoreError(StoreErrorKind.Unique, UniqueName(failure.Message, model)),
            ConstraintForeignKey => new StoreError(StoreErrorKind.ForeignKey, null),
            ConstraintCheck => new StoreError(StoreErrorKind.Check, ConstraintNames.InMessage(failure.Message)),
            _ => null,
        };
    }

    /// <summary>The unique index or key whose `table.column` list SQLite gives, or null.</summary>
    private static string? UniqueName(string message, IModel model)
    {
        var match = UniqueColumns().Match(message);
        if (!match.Success)
            return null;
        var columns = match.Groups["columns"].Value.Split(", ");
        var table = columns[0][..columns[0].IndexOf('.', StringComparison.Ordinal)];
        return ConstraintNames.UniqueOf(model, table, columns.Select(column => column[(column.IndexOf('.', StringComparison.Ordinal) + 1)..]).ToList());
    }

    /// <summary>
    /// A transaction opened by the provider of SQLite itself (EF Core opens only immediate ones), handed to EF Core; the
    /// connection opened for it is closed after it.
    /// </summary>
    private static async Task<StoreTransaction> BeginAsync(AppDbContext context, bool deferred, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var transaction = ((SqliteConnection)context.Database.GetDbConnection()).BeginTransaction(deferred);
            var shared = await context.Database.UseTransactionAsync(transaction, cancellationToken);
            return new StoreTransaction(shared!, async () =>
            {
                await transaction.DisposeAsync();
                await context.Database.CloseConnectionAsync();
            });
        }
        catch
        {
            await context.Database.CloseConnectionAsync();
            throw;
        }
    }
}
