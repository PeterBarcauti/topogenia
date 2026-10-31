// MigrationTransactions.cs
// The migrations of a provider that EF Core runs, and records, outside any transaction (spec 4.4, ADR 027, ADR 031).

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Topogenia.Persistence.Schema;

/// <summary>
/// A migration with a statement that cannot run in a transaction (an option of the database) is recorded by EF Core in
/// its history outside any transaction too: EF Core writes the row of `__EFMigrationsHistory` outside a transaction as
/// soon as one command of the migration is (`Migrator.GenerateUpSql`). Such a migration is found here by the same rule,
/// on the commands that the provider generates for it, with the public services of EF Core.
/// </summary>
public static class MigrationTransactions
{
    /// <summary>The identifiers of the migrations of the context whose commands include one run outside any transaction.</summary>
    public static IReadOnlySet<string> OutsideTransaction(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var assembly = context.GetService<IMigrationsAssembly>();
        var generator = context.GetService<IMigrationsSqlGenerator>();
        var initializer = context.GetService<IModelRuntimeInitializer>();
        var provider = context.Database.ProviderName!;
        var outside = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (id, type) in assembly.Migrations)
        {
            var migration = assembly.CreateMigration(type, provider);
            var model = migration.TargetModel switch
            {
                IMutableModel mutable => initializer.Initialize(mutable.FinalizeModel(), designTime: true, validationLogger: null),
                { } finalModel => initializer.Initialize(finalModel, designTime: true, validationLogger: null),
                null => null,
            };
            if (generator.Generate(migration.UpOperations, model).Any(command => command.TransactionSuppressed))
                outside.Add(id);
        }
        return outside;
    }
}
