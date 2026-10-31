// SqlServerModelCustomizer.cs
// The SQL Server part of the model: the schema of the product, binary text columns, the filters of the partial indexes and the CHECK constraints, those of the texts included (ADR 025, 038).

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Topogenia.Persistence.SqlServer;

/// <summary>Builds the common model, then adds what SQL Server writes in its own syntax (ADR 025).</summary>
public sealed class SqlServerModelCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
{
    /// <summary>
    /// The collation of every text column (ADR 038): code-point comparison, as SQLite compares text (spec 4.3-3). Not the
    /// UTF-8 variant: the ASCII columns (`varchar`) keep the code page 1252 of the usual instances, the Unicode ones
    /// (`nvarchar`) are UTF-16 whatever the collation.
    /// </summary>
    public const string BinaryCollation = "Latin1_General_100_BIN2";

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(SqlServerEngine.Schema);
        base.Customize(modelBuilder, context);
        BinaryText(modelBuilder);
        SqlServerGuards.Apply(modelBuilder);
    }

    /// <summary>
    /// Every column stored as text, codes of the closed lists included, compares in binary: a column without a collation of
    /// its own would take the one of the database, which may ignore case, accents or width (`French_CI_AS`).
    /// </summary>
    private static void BinaryText(ModelBuilder modelBuilder)
    {
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()))
        {
            if (IsText(property))
                property.SetCollation(BinaryCollation);
        }
    }

    /// <summary>A column stored as text: a string, or a closed list stored as its code (every enumeration of the model, `CodeConverter`).</summary>
    private static bool IsText(IMutableProperty property)
    {
        var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        return type == typeof(string) || type.IsEnum;
    }
}
