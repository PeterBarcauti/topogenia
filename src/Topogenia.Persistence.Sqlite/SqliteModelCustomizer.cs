// SqliteModelCustomizer.cs
// The SQLite part of the model: the filters of the partial indexes and the CHECK constraints in SQLite syntax, those of the texts included (ADR 025, 038).

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Topogenia.Persistence.Sqlite;

/// <summary>Builds the common model, then adds what SQLite writes in its own syntax (ADR 025); no schema.</summary>
public sealed class SqliteModelCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);
        SqliteGuards.Apply(modelBuilder);
    }
}
