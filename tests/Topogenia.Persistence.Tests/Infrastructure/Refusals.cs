// Refusals.cs
// Writes rows and expects the database itself to refuse them by a given guard: unique index, CHECK or foreign key.

using Microsoft.EntityFrameworkCore;

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>The kinds of guards of the database (spec 4.2).</summary>
public enum GuardKind
{
    Unique,
    Check,
    ForeignKey,
}

/// <summary>
/// Saves rows in a new context and checks the refusal of the engine. SQL Server names the guard in its message; SQLite
/// names a CHECK, gives the columns of a unique index and names no foreign key: the expected text follows the engine.
/// </summary>
public static class Refusals
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static async Task SaveAsync(TestDatabase database, params object[] records)
    {
        ArgumentNullException.ThrowIfNull(database);
        await using var context = database.Context();
        context.AddRange(records);
        await context.SaveChangesAsync(Token);
    }

    public static async Task AssertRefusedAsync(TestDatabase database, GuardKind kind, string name, params object[] records)
    {
        ArgumentNullException.ThrowIfNull(database);
        await using var context = database.Context();
        context.AddRange(records);
        var refusal = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Token));
        var message = refusal.InnerException?.Message ?? refusal.Message;
        Assert.Contains(Expected(database, context, kind, name), message, StringComparison.Ordinal);
    }

    private static string Expected(TestDatabase database, DbContext context, GuardKind kind, string name)
    {
        if (database.Kind == EngineKind.SqlServer)
            return name;
        return kind switch
        {
            GuardKind.Check => $"CHECK constraint failed: {name}",
            GuardKind.ForeignKey => "FOREIGN KEY constraint failed",
            _ => "UNIQUE constraint failed: " + UniqueColumns(context, name),
        };
    }

    /// <summary>`table.column, table.column`: how SQLite reports a unique index (or key) of the model.</summary>
    private static string UniqueColumns(DbContext context, string name)
    {
        foreach (var entity in context.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            var properties = entity.GetIndexes().FirstOrDefault(index => index.GetDatabaseName() == name)?.Properties
                ?? entity.GetKeys().FirstOrDefault(key => key.GetName() == name)?.Properties;
            if (properties is not null)
                return string.Join(", ", properties.Select(property => $"{table}.{property.GetColumnName()}"));
        }
        throw new InvalidOperationException($"No unique index or key {name} in the model.");
    }
}
