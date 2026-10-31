// 20261023174605_TextGuardsCheck.cs
// Third SQLite migration: checks in a transaction that the tables rebuilt by the second one break no foreign key (ADR 038).

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Topogenia.Persistence.Sqlite.Migrations
{
    /// <summary>
    /// `TextGuardsAndIntegerBounds` adds CHECK constraints, which SQLite can only add by rebuilding each table. Its
    /// transaction already defers the check of the foreign keys to its commit (<see cref="AtomicRebuildSqlGenerator"/>);
    /// this migration checks them once more, apart, after the rebuild: no row breaks a foreign key (a count of
    /// `pragma_foreign_key_check` refused by the CHECK of a temporary table, which leaves nothing in the database). It
    /// first recorded the second migration, which EF Core then ran outside any transaction, as the SQL Server pair
    /// `SnapshotIsolation` and `SnapshotIsolationCheck` does (ADR 031); since the rebuild runs in its own transaction
    /// (data B2), each of the two records itself. Generated empty by `dotnet ef` (the model does not change), then written
    /// by hand.
    /// </summary>
    public partial class TextGuardsCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE "foreign_key_violations" ("count" INTEGER NOT NULL CHECK ("count" = 0));
                INSERT INTO "foreign_key_violations" SELECT count(*) FROM pragma_foreign_key_check;
                DROP TABLE "foreign_key_violations";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The CHECK constraints are removed by the Down of TextGuardsAndIntegerBounds. The record of this migration goes
            // with its row of EF Core, so that the start reads the file as behind and migrates it again (data M2).
            migrationBuilder.DeleteData(table: "schema_history", keyColumn: "migration_id", keyValue: "20261023174605_TextGuardsCheck");
        }
    }
}
