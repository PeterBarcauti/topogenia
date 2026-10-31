// 20261023092342_SnapshotIsolation.cs
// Third SQL Server migration: the snapshot isolation and the read committed row versions of the database, outside any transaction (ADR 031).

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Topogenia.Persistence.SqlServer.Migrations
{
    /// <summary>
    /// The options of the database that the reads need (1.x `db/003_concurrence.sql`, spec 3.4, ADR 031): the snapshot
    /// isolation of the consistent reads, and the row versions of read committed, so that no statement that reads waits for
    /// a writer. Generated empty by `dotnet ef` (the model does not change), then written by hand. An option of the
    /// database cannot change in a transaction: each statement runs alone, outside any transaction, and only when the option
    /// is not set yet, so that a migration that failed after it can run again (1.x `-- sans-transaction`). EF Core then
    /// records this migration outside any transaction too: `SnapshotIsolationCheck` writes its record in `schema_history`.
    /// The change of read committed needs the database for itself: it rolls back the transactions of the other sessions, as
    /// in the 1.x.
    /// </summary>
    public partial class SnapshotIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF (SELECT snapshot_isolation_state FROM sys.databases WHERE database_id = DB_ID()) = 0
                    ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION ON;
                """,
                suppressTransaction: true);
            migrationBuilder.Sql(
                """
                IF (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE database_id = DB_ID()) = 0
                    ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
                """,
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE database_id = DB_ID()) = 1
                    ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE;
                """,
                suppressTransaction: true);
            migrationBuilder.Sql(
                """
                IF (SELECT snapshot_isolation_state FROM sys.databases WHERE database_id = DB_ID()) <> 0
                    ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION OFF;
                """,
                suppressTransaction: true);
        }
    }
}
