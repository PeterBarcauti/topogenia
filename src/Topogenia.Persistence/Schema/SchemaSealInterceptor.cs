// SchemaSealInterceptor.cs
// Records each migration with its fingerprints inside the transaction that applies it, just before its commit (spec 4.4, A-4-34).

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Topogenia.Persistence.Schema;

/// <summary>
/// Added only to the context that migrates. When EF Core commits a transaction of the migrations, every migration that
/// its history now lists without a record in `schema_history` is recorded there, in the same transaction: the record and
/// the migration are written together or not at all, as the 1.x wrote `schema_version` with its script. The last one
/// receives the fingerprint of the schema, read in the same transaction.
/// </summary>
internal sealed class SchemaSealInterceptor(SchemaCatalog catalog, MigrationSources sources, string applicationVersion, DateTime appliedAt)
    : DbTransactionInterceptor
{
    public override InterceptionResult TransactionCommitting(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        SealAsync(transaction, eventData.Context!, CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        await SealAsync(transaction, eventData.Context!, cancellationToken);
        return result;
    }

    private async Task SealAsync(DbTransaction transaction, DbContext context, CancellationToken cancellationToken)
    {
        var history = context.GetService<IHistoryRepository>();
        if (!await history.ExistsAsync(cancellationToken))
            return;
        var applied = (await history.GetAppliedMigrationsAsync(cancellationToken)).Select(row => row.MigrationId).ToList();
        if (applied.Count == 0)
            return;
        var connection = transaction.Connection!;
        var recorded = (await catalog.HistoryAsync(connection, transaction, cancellationToken)).Select(entry => entry.MigrationId).ToHashSet(StringComparer.Ordinal);
        var unrecorded = applied.Where(id => !recorded.Contains(id)).Order(StringComparer.Ordinal).ToList();
        if (unrecorded.Count == 0)
            return;
        var schema = Fingerprints.OfLines(await catalog.DescribeAsync(connection, transaction, cancellationToken));
        foreach (var id in unrecorded)
        {
            var entry = new SchemaHistoryEntry(id, sources.FingerprintOf(id), id == unrecorded[^1] ? schema : null, applicationVersion, appliedAt);
            await catalog.RecordAsync(connection, transaction, entry, cancellationToken);
        }
    }
}
