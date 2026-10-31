// ModelNaming.cs
// Names of the database objects: snake_case columns and the 1.x conventions PK_, FK_, IX_ (spec 4.1, MODELE_DONNEES 1.x).

using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Topogenia.Persistence.Model;

/// <summary>
/// Gives every object of the model the name of the 1.x conventions (`MODELE_DONNEES.md`, section 1): tables and columns
/// in snake_case, `PK_table`, `FK_table_role` (role: first column of the key without `_id`), `IX_table_columns`. A name
/// set by a configuration (a 1.x column name such as `column_index`, the `UQ_` and `UX_` indexes) is kept.
/// </summary>
public static class ModelNaming
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var table = entity.GetTableName()
                ?? throw new InvalidOperationException($"{entity.ClrType.Name} has no table name.");
            foreach (var property in entity.GetProperties().Where(property => property.FindAnnotation(RelationalAnnotationNames.ColumnName) is null))
                property.SetColumnName(SnakeCase(property.Name));
            entity.FindPrimaryKey()?.SetName($"PK_{table}");
            foreach (var key in entity.GetForeignKeys().Where(key => key.FindAnnotation(RelationalAnnotationNames.Name) is null))
                key.SetConstraintName($"FK_{table}_{Role(key.Properties[0].GetColumnName())}");
            foreach (var index in entity.GetIndexes().Where(index => index.Name is null && index.FindAnnotation(RelationalAnnotationNames.Name) is null))
                index.SetDatabaseName($"IX_{table}_{string.Join("_", index.Properties.Select(property => property.GetColumnName()))}");
        }
    }

    /// <summary>`SourceStepId` gives `source_step_id`, `IsAdmin` gives `is_admin`.</summary>
    public static string SnakeCase(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0 && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
                builder.Append('_');
            builder.Append(char.ToLowerInvariant(name[i]));
        }
        return builder.ToString();
    }

    private static string Role(string column) => column.EndsWith("_id", StringComparison.Ordinal) ? column[..^3] : column;
}
