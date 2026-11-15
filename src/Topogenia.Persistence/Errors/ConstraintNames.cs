// ConstraintNames.cs
// Finds the guard named by an error of the engine: its name in the message, or the unique index or key of a table and its columns.

using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Topogenia.Persistence.Errors;

/// <summary>
/// The names of the guards follow `docs/modele-donnees.md` § 2 (`PK_`, `UQ_`, `UX_`, `FK_`, `CK_`): a message of the
/// engine, in whatever language the server speaks, carries the name as it is. SQLite reports a unique index by its table
/// and columns instead, which the model maps back to the name of the index or of the key.
/// </summary>
public static partial class ConstraintNames
{
    [GeneratedRegex(@"\b(?:PK|UQ|UX|FK|CK)_\w+")]
    private static partial Regex Named();

    /// <summary>The first name of a guard written in the message, or null.</summary>
    public static string? InMessage(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var match = Named().Match(message);
        return match.Success ? match.Value : null;
    }

    /// <summary>True for the name of a foreign key of the model (`FK_table_role`).</summary>
    public static bool IsForeignKey(string? name) => name is not null && name.StartsWith("FK_", StringComparison.Ordinal);

    /// <summary>The unique index or the key of <paramref name="table"/> made of exactly these columns, in this order, or null.</summary>
    public static string? UniqueOf(IModel model, string table, IReadOnlyList<string> columns)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(columns);
        foreach (var entity in model.GetEntityTypes().Where(entity => entity.GetTableName() == table))
        {
            var index = entity.GetIndexes().FirstOrDefault(index => index.IsUnique && Same(index.Properties, columns));
            if (index is not null)
                return index.GetDatabaseName();
            var key = entity.GetKeys().FirstOrDefault(key => Same(key.Properties, columns));
            if (key is not null)
                return key.GetName();
        }
        return null;
    }

    private static bool Same(IReadOnlyList<IProperty> properties, IReadOnlyList<string> columns) =>
        properties.Select(property => property.GetColumnName()).SequenceEqual(columns, StringComparer.Ordinal);
}
