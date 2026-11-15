// SchemaHistoryEntry.cs
// One applied migration with its fingerprints: the history of the schema checked before any use of the database (spec 4.4).

namespace Topogenia.Persistence.Schema;

/// <summary>
/// A row of `schema_history` (spec 4.4, 1.x `schema_version`): the migration applied, the SHA-256 fingerprint of its
/// source (LF line ends, as the 1.x scripts), the fingerprint of the schema read from the catalogue right after the last
/// migration of its transaction (null for the migrations applied before it in the same transaction), the version of the
/// application that applied it and when. EF Core keeps its own history next to it (`__EFMigrationsHistory`).
/// </summary>
public sealed record SchemaHistoryEntry(
    string MigrationId,
    string MigrationFingerprint,
    string? SchemaFingerprint,
    string ApplicationVersion,
    DateTime AppliedAt)
{
    /// <summary>Width of a migration identifier (`20261023120000_Initial`).</summary>
    public const int MigrationIdLength = 150;
}
