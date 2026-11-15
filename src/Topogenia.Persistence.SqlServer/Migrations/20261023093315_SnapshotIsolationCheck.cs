// 20261023093315_SnapshotIsolationCheck.cs
// Fourth SQL Server migration: checks in a transaction that the options of the snapshot reads hold, which records the third one (ADR 031).

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Topogenia.Persistence.SqlServer.Migrations
{
    /// <summary>
    /// Checks, in a transaction, that the options set by `SnapshotIsolation` hold (ADR 031). EF Core records a migration
    /// that has a statement outside its transaction outside any transaction too, so that the record of the history with its
    /// fingerprints (`schema_history`, written when a transaction of the migrations commits, ADR 027) cannot be written
    /// with it: it is written with this migration, which records both, the fingerprint of the schema on this one. The
    /// options are described by the fingerprint from this migration on, so that a run stopped between the two leaves the
    /// database behind and the next run completes it. Generated empty by `dotnet ef` (the model does not change), then
    /// written by hand.
    /// </summary>
    public partial class SnapshotIsolationCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF (SELECT snapshot_isolation_state_desc FROM sys.databases WHERE database_id = DB_ID()) <> 'ON'
                    OR (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE database_id = DB_ID()) = 0
                    THROW 50000, N'The options of the database for the snapshot reads are not set.', 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: the options are removed by the Down of SnapshotIsolation.
        }
    }
}
