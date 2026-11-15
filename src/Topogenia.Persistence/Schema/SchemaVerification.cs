// SchemaVerification.cs
// The refusals of the schema computed from the migrations of the application and the two histories of the database (spec 4.4).

using Topogenia.Application.Ports;

namespace Topogenia.Persistence.Schema;

/// <summary>What the database holds: the migrations applied by EF Core, the history with fingerprints, the schema read now.</summary>
public sealed record DatabaseSchema(IReadOnlyList<string> Applied, IReadOnlyList<SchemaHistoryEntry> History, string? CurrentFingerprint);

/// <summary>
/// The check of spec 4.4, in the order of the 1.x (`MiseAJourBase.Verifier`): a database newer than the application
/// first, then a migration that changed, then a migration without its record, then a schema changed outside the
/// migrations. Each refusal is a <see cref="SchemaRefusedException"/> with its code.
/// </summary>
public static class SchemaVerification
{
    /// <summary>
    /// The state of the schema, or the refusal of the database. <paramref name="outsideTransaction"/> lists the known
    /// migrations that EF Core records outside any transaction (<see cref="MigrationTransactions"/>): see
    /// <see cref="AwaitingRecord"/>.
    /// </summary>
    public static SchemaState Verify(IReadOnlyList<string> known, MigrationSources sources, DatabaseSchema database, IReadOnlySet<string>? outsideTransaction = null)
    {
        ArgumentNullException.ThrowIfNull(known);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(database);
        var knownSet = known.ToHashSet(StringComparer.Ordinal);
        var recorded = database.History.ToDictionary(entry => entry.MigrationId, StringComparer.Ordinal);
        var newer = database.Applied.Concat(recorded.Keys).Where(id => !knownSet.Contains(id)).Order(StringComparer.Ordinal).FirstOrDefault();
        if (newer is not null)
            throw new SchemaRefusedException(SchemaCodes.NewerDatabase, newer);
        var changed = recorded.Values.Where(entry => entry.MigrationFingerprint != sources.FingerprintOf(entry.MigrationId))
            .Select(entry => entry.MigrationId).Order(StringComparer.Ordinal).FirstOrDefault();
        if (changed is not null)
            throw new SchemaRefusedException(SchemaCodes.MigrationChanged, changed);
        var awaiting = AwaitingRecord(known, database, recorded, outsideTransaction ?? new HashSet<string>());
        var unrecorded = database.Applied.Where(id => !recorded.ContainsKey(id) && id != awaiting).Order(StringComparer.Ordinal).FirstOrDefault();
        if (unrecorded is not null)
            throw new SchemaRefusedException(SchemaCodes.FingerprintMissing, unrecorded);
        // A record whose migration EF Core no longer lists as applied: its history was changed outside the migrations.
        var forgotten = recorded.Keys.Where(id => !database.Applied.Contains(id, StringComparer.Ordinal)).Order(StringComparer.Ordinal).FirstOrDefault();
        if (forgotten is not null)
            throw new SchemaRefusedException(SchemaCodes.SchemaChanged, forgotten);
        CheckSchema(database, recorded, awaiting);
        var pending = known.Where(id => !database.Applied.Contains(id, StringComparer.Ordinal)).ToList();
        var applied = known.Where(id => database.Applied.Contains(id, StringComparer.Ordinal)).ToList();
        return new SchemaState(pending.Count == 0 ? SchemaStatus.UpToDate : SchemaStatus.Behind, applied, pending);
    }

    /// <summary>
    /// The last applied migration when it is the one left without its record by a run that stopped after EF Core recorded
    /// it outside any transaction, or null. It is accepted, the database being behind, when: EF Core records it outside a
    /// transaction; every migration before it is recorded, the one just before it at least (the reference of the schema);
    /// and the next known migration, still pending, runs in a transaction, whose commit records both of them
    /// (<see cref="SchemaSealInterceptor"/>). Its effects enter the description of the schema only with that next
    /// migration (ADR 031): the schema read now must still be the one recorded before it.
    /// </summary>
    private static string? AwaitingRecord(
        IReadOnlyList<string> known, DatabaseSchema database, Dictionary<string, SchemaHistoryEntry> recorded, IReadOnlySet<string> outsideTransaction)
    {
        var applied = database.Applied.Order(StringComparer.Ordinal).ToList();
        if (applied.Count < 2 || recorded.ContainsKey(applied[^1]) || !outsideTransaction.Contains(applied[^1]) || !recorded.ContainsKey(applied[^2]))
            return null;
        var ordered = known.Order(StringComparer.Ordinal).ToList();
        var position = ordered.IndexOf(applied[^1]);
        if (position < 0 || position + 1 >= ordered.Count)
            return null;
        var next = ordered[position + 1];
        return outsideTransaction.Contains(next) || database.Applied.Contains(next, StringComparer.Ordinal) ? null : applied[^1];
    }

    /// <summary>
    /// The schema read now must be the one left by the last applied migration (its recorded fingerprint), or, when that
    /// migration awaits its record, by the migration just before it.
    /// </summary>
    private static void CheckSchema(DatabaseSchema database, Dictionary<string, SchemaHistoryEntry> recorded, string? awaiting)
    {
        var last = database.Applied.Order(StringComparer.Ordinal).LastOrDefault(id => id != awaiting);
        if (last is null)
            return;
        if (recorded[last].SchemaFingerprint is not { } expected)
            throw new SchemaRefusedException(SchemaCodes.FingerprintMissing, last);
        if (!string.Equals(expected, database.CurrentFingerprint, StringComparison.Ordinal))
            throw new SchemaRefusedException(SchemaCodes.SchemaChanged, last);
    }
}
