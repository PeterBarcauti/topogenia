// AtomicRebuildSqlGenerator.cs
// The SQL of the SQLite migrations with every rebuild of a table inside the transaction of its migration (data B2, ADR 038).

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Topogenia.Persistence.Sqlite;

/// <summary>
/// SQLite changes a constraint by rebuilding its table. EF Core writes the copy `ef_temp_&lt;table&gt;` in a transaction,
/// then turns the foreign keys off outside any transaction (`PRAGMA foreign_keys` is ignored inside one), drops each
/// table and renames its copy in a second transaction, turns them on again outside it, and creates the indexes in a
/// third: a stop between two of them left a file that no start could take again (copies left, tables gone, data B2).
/// This generator keeps every statement of a migration in its one transaction, so that the migration and its records
/// commit together or not at all (A-4-34): the foreign keys stay on and their check is deferred to the commit
/// (`PRAGMA defer_foreign_keys`, allowed in a transaction, reset by the commit), which then refuses the whole migration
/// if a reference is broken. Instead of renaming the copy (SQLite counts the references of a dropped table as broken and
/// a rename does not count them back), each table is created again with its new definition under its own name and
/// filled from the copy, which is dropped: the counter of the deferred foreign keys falls back to zero once every table
/// holds its rows again, and the statement kept in `sqlite_master` is the one the rename gave (same text). The data are
/// copied twice; the files of the desktop mode are small. Every rebuild of a later migration follows the same rule (a
/// test refuses any migration of the provider run outside a transaction).
/// </summary>
public sealed class AtomicRebuildSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, IRelationalAnnotationProvider annotations)
    : SqliteMigrationsSqlGenerator(dependencies, annotations)
{
    /// <summary>The prefix of the copy of a rebuilt table, chosen by EF Core.</summary>
    public const string CopyPrefix = "ef_temp_";

    private const string ForeignKeysOff = "PRAGMA foreign_keys = 0;";
    private const string ForeignKeysOn = "PRAGMA foreign_keys = 1;";

    /// <summary>The copies of the migration being generated, by their name: their columns and their `CREATE TABLE` statement.</summary>
    private readonly Dictionary<string, (CreateTableOperation Operation, string Statement)> copies = new(StringComparer.Ordinal);

    public override IReadOnlyList<MigrationCommand> Generate(
        IReadOnlyList<MigrationOperation> operations, IModel? model = null, MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
    {
        copies.Clear();
        try
        {
            return base.Generate(operations, model, options);
        }
        finally
        {
            copies.Clear();
        }
    }

    protected override void Generate(CreateTableOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Name.StartsWith(CopyPrefix, StringComparison.Ordinal))
        {
            // The same statement, generated apart, is kept to create the table again under its own name.
            var alone = new MigrationCommandListBuilder(Dependencies);
            base.Generate(operation, model, alone, terminate: true);
            copies[operation.Name] = (operation, string.Concat(alone.GetCommandList().Select(command => command.CommandText)));
        }
        base.Generate(operation, model, builder, terminate);
    }

    protected override void Generate(SqlOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(builder);
        if (operation.SuppressTransaction && operation.Sql == ForeignKeysOff)
        {
            builder.Append("PRAGMA defer_foreign_keys = ON;").AppendLine().EndCommand();
            return;
        }
        if (operation.SuppressTransaction && operation.Sql == ForeignKeysOn)
            return;
        base.Generate(operation, model, builder);
    }

    protected override void Generate(RenameTableOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(builder);
        if (operation.NewName is not { } table || !copies.TryGetValue(operation.Name, out var copy) || operation.NewName.StartsWith(CopyPrefix, StringComparison.Ordinal))
        {
            base.Generate(operation, model, builder);
            return;
        }
        var helper = Dependencies.SqlGenerationHelper;
        // The statement of the copy with the name of the table, as `ALTER TABLE ... RENAME TO` writes it in `sqlite_master`.
        var head = "CREATE TABLE " + helper.DelimitIdentifier(operation.Name) + " (";
        if (!copy.Statement.StartsWith(head, StringComparison.Ordinal))
            throw new InvalidOperationException($"Unexpected statement of the copy {operation.Name}.");
        builder.Append("CREATE TABLE " + helper.DelimitIdentifier(table) + " (" + copy.Statement[head.Length..].TrimEnd()).AppendLine().EndCommand();
        var columns = string.Join(", ", copy.Operation.Columns.Select(column => helper.DelimitIdentifier(column.Name)));
        builder
            .Append("INSERT INTO ").Append(helper.DelimitIdentifier(table)).Append(" (").Append(columns).Append(")")
            .AppendLine()
            .Append("SELECT ").Append(columns).Append(" FROM ").Append(helper.DelimitIdentifier(operation.Name))
            .Append(helper.StatementTerminator).AppendLine().EndCommand();
        builder.Append("DROP TABLE ").Append(helper.DelimitIdentifier(operation.Name)).Append(helper.StatementTerminator).AppendLine().EndCommand();
    }
}
