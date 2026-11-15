// OutOfBand.cs
// Changes made to a database outside its migrations, as a person would by hand: the cases that the schema check must refuse.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>
/// Applies schema operations or history rows without going through the migrations. The SQL is produced by the generators
/// of the provider itself (the tests write none, spec 3.2): a column added, an index dropped, a row of the EF Core
/// history added or removed.
/// </summary>
public static class OutOfBand
{
    /// <summary>Applies operations directly, without any record in the histories.</summary>
    public static async Task ApplyAsync(AppDbContext context, params MigrationOperation[] operations)
    {
        ArgumentNullException.ThrowIfNull(context);
        var commands = context.GetService<IMigrationsSqlGenerator>().Generate(operations, context.GetService<IDesignTimeModel>().Model);
        await context.GetService<IMigrationCommandExecutor>()
            .ExecuteNonQueryAsync(commands, context.GetService<IRelationalConnection>(), TestContext.Current.CancellationToken);
    }

    /// <summary>A column added to a table of the application by hand.</summary>
    public static Task AddColumnAsync(AppDbContext context, string table, string column) =>
        ApplyAsync(context, new AddColumnOperation
        {
            Schema = Schema(context),
            Table = table,
            Name = column,
            ClrType = typeof(string),
            IsNullable = true,
        });

    /// <summary>An index dropped by hand.</summary>
    public static Task DropIndexAsync(AppDbContext context, string table, string index) =>
        ApplyAsync(context, new DropIndexOperation { Schema = Schema(context), Table = table, Name = index });

    /// <summary>A migration recorded in the history of EF Core as a newer application would record it.</summary>
    public static Task RecordInEfHistoryAsync(AppDbContext context, string migrationId) =>
        ApplyAsync(context, new SqlOperation { Sql = context.GetService<IHistoryRepository>().GetInsertScript(new HistoryRow(migrationId, ProductVersion)) });

    /// <summary>A migration removed from the history of EF Core.</summary>
    public static Task RemoveFromEfHistoryAsync(AppDbContext context, string migrationId) =>
        ApplyAsync(context, new SqlOperation { Sql = context.GetService<IHistoryRepository>().GetDeleteScript(migrationId) });

    /// <summary>The version of EF Core written by the history rows.</summary>
    public static string ProductVersion => typeof(DbContext).Assembly.GetName().Version!.ToString(3);

    private static string? Schema(AppDbContext context) => context.Model.GetDefaultSchema();
}
