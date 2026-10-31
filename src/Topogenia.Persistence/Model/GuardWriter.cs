// GuardWriter.cs
// Walks the guards of the common model (spec 4.2) without writing any SQL: each provider writes every condition in its own syntax (spec 3.2, ADR 025).

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Topogenia.Persistence.Model;

/// <summary>
/// What the two providers share to write the guards of <see cref="ModelGuards"/>: the index or the table of each guard,
/// the column of each property, whether a column is a decimal, the walk of the conditions in their order, the numbers in
/// the invariant culture, and the doubling of a closing character inside a delimited text. It holds no SQL (spec 3.2): each
/// provider writes the operators, the delimiters, the literals and the functions of its engine, so a change of syntax
/// stays in its provider. The provider still adds the filters and CHECK constraints to its model (ADR 025).
/// </summary>
public abstract class GuardWriter
{
    /// <summary>The filter of each partial unique index of the common model, on the model being built.</summary>
    public IEnumerable<(IMutableIndex Index, string Filter)> Filters(IMutableModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        foreach (var guard in ModelGuards.PartialIndexes)
        {
            var entity = Entity(model, guard.Record);
            yield return (entity.GetIndexes().Single(index => index.Name == guard.IndexName), Write(guard.Filter, entity));
        }
    }

    /// <summary>Each CHECK constraint of the common model (<see cref="ModelGuards.Checks"/>): its record, its name and its condition.</summary>
    public IEnumerable<(Type Record, string Name, string Condition)> Checks(IMutableModel model) => Written(model, ModelGuards.Checks);

    /// <summary>The CHECK constraint of each text column of the model being built (<see cref="ModelGuards.TextChecks"/>, ADR 038).</summary>
    public IEnumerable<(Type Record, string Name, string Condition)> TextChecks(IMutableModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return Written(model, [.. ModelGuards.TextChecks(model)]);
    }

    /// <summary>The condition written by the provider on the columns of an entity.</summary>
    public string Write(GuardCondition condition, IReadOnlyEntityType entity)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(entity);
        return condition switch
        {
            IsNull test => IsNull(Column(entity, test.Property)),
            IsNotNull test => IsNotNull(Column(entity, test.Property)),
            InList test => InList(Column(entity, test.Property), [.. test.Values.Select(Text)]),
            EqualsCode test => EqualsText(Column(entity, test.Property), Text(test.Value)),
            Between test => Between(Compared(entity, test.Property), Number(test.Minimum), Number(test.Maximum)),
            AtLeast test => AtLeast(Compared(entity, test.Property), Number(test.Minimum)),
            Differ test => Differ(Column(entity, test.Property), Column(entity, test.Other)),
            NoTrailingSpace test => NoTrailingSpace(Column(entity, test.Property)),
            TextLength test => TextLength(Column(entity, test.Property), test.Maximum, test.Fixed),
            PrintableAscii test => PrintableAscii(Column(entity, test.Property)),
            AllOf all => AllOf([.. all.Conditions.Select(item => Write(item, entity))]),
            AnyOf any => AnyOf([.. any.Conditions.Select(item => Write(item, entity))]),
            _ => throw new ArgumentException($"Unknown guard condition {condition.GetType().Name}.", nameof(condition)),
        };
    }

    /// <summary>A column name delimited in the syntax of the engine.</summary>
    protected abstract string Delimited(string column);

    /// <summary>A delimited decimal column as the engine compares it with a number; itself by default.</summary>
    protected virtual string ComparedDecimal(string column) => column;

    /// <summary>A text literal of the engine.</summary>
    protected abstract string Text(string value);

    protected abstract string IsNull(string column);

    protected abstract string IsNotNull(string column);

    protected abstract string InList(string column, IReadOnlyList<string> texts);

    protected abstract string EqualsText(string column, string text);

    protected abstract string Between(string column, string minimum, string maximum);

    protected abstract string AtLeast(string column, string minimum);

    protected abstract string Differ(string column, string other);

    protected abstract string NoTrailingSpace(string column);

    protected abstract string TextLength(string column, int maximum, bool exact);

    protected abstract string PrintableAscii(string column);

    protected abstract string AllOf(IReadOnlyList<string> conditions);

    protected abstract string AnyOf(IReadOnlyList<string> conditions);

    /// <summary>A number in the invariant culture.</summary>
    protected static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>A text between two characters, the closing one doubled inside it.</summary>
    protected static string Enclosed(string text, char opening, char closing)
    {
        ArgumentNullException.ThrowIfNull(text);
        return opening + text.Replace(closing.ToString(), new string(closing, 2), StringComparison.Ordinal) + closing;
    }

    private IEnumerable<(Type Record, string Name, string Condition)> Written(IMutableModel model, IReadOnlyList<CheckGuard> guards)
    {
        ArgumentNullException.ThrowIfNull(model);
        foreach (var guard in guards)
            yield return (guard.Record, guard.Name, Write(guard.Condition, Entity(model, guard.Record)));
    }

    private static IMutableEntityType Entity(IMutableModel model, Type record) =>
        model.FindEntityType(record) ?? throw new InvalidOperationException($"{record.Name} is not in the model.");

    private static IReadOnlyProperty Property(IReadOnlyEntityType entity, string property) =>
        entity.FindProperty(property) ?? throw new InvalidOperationException($"{entity.ClrType.Name}.{property} is not mapped.");

    private string Column(IReadOnlyEntityType entity, string property) => Delimited(Property(entity, property).GetColumnName());

    /// <summary>A column compared with a number: a decimal column as the engine compares it, any other as it is.</summary>
    private string Compared(IReadOnlyEntityType entity, string property)
    {
        var type = Property(entity, property).ClrType;
        var column = Column(entity, property);
        return (Nullable.GetUnderlyingType(type) ?? type) == typeof(decimal) ? ComparedDecimal(column) : column;
    }
}
