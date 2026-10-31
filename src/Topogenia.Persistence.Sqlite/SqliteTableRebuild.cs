// SqliteTableRebuild.cs
// The way down of a SQLite migration that rebuilt tables: every table created again by the statements of the migration it goes back to (data M2, ADR 038).

using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Topogenia.Persistence.Sqlite;

/// <summary>
/// EF Core rebuilds a table from its model, with the columns in another order (the key, then by name) than the
/// migration that created it (the order of the properties): a way down built by EF Core leaves an equivalent schema
/// whose statements are not those the earlier migration left, and the start refuses it (`SCHEMA_MODIFIE`, its
/// fingerprint differs, data M2). <see cref="BackTo"/> writes the way down from the operations of the earlier migration
/// instead: each table is copied as it is (`CREATE TABLE ... AS SELECT`, no constraint), dropped, created again by the
/// operations of that migration (tables, foreign keys, indexes: the same statements as when it ran), filled again from
/// its copy, which is dropped. Everything runs in the transaction of the migration, the check of the foreign keys
/// deferred to its commit (<see cref="AtomicRebuildSqlGenerator"/>).
/// </summary>
public static class SqliteTableRebuild
{
    /// <summary>
    /// Brings every table that <paramref name="target"/> creates back to the statements of <paramref name="target"/>, the
    /// operations of the `Up` of the migration the way down goes back to, its rows kept. The tables must have the same
    /// columns before and after (a migration that only changes constraints).
    /// </summary>
    public static void BackTo(MigrationBuilder migrationBuilder, IReadOnlyList<MigrationOperation> target)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        ArgumentNullException.ThrowIfNull(target);
        var tables = target.OfType<CreateTableOperation>().ToList();
        migrationBuilder.Sql("PRAGMA defer_foreign_keys = ON;");
        foreach (var table in tables)
            migrationBuilder.Sql($"CREATE TABLE {Name(AtomicRebuildSqlGenerator.CopyPrefix + table.Name)} AS SELECT * FROM {Name(table.Name)};");
        foreach (var table in tables)
            migrationBuilder.DropTable(table.Name);
        migrationBuilder.Operations.AddRange(target);
        foreach (var table in tables)
        {
            var columns = string.Join(", ", table.Columns.Select(column => Name(column.Name)));
            var copy = Name(AtomicRebuildSqlGenerator.CopyPrefix + table.Name);
            migrationBuilder.Sql($"INSERT INTO {Name(table.Name)} ({columns}) SELECT {columns} FROM {copy};");
            migrationBuilder.Sql($"DROP TABLE {copy};");
        }
    }

    /// <summary>A name of the model between double quotes (the names of the model have none).</summary>
    private static string Name(string name) => "\"" + name + "\"";
}
