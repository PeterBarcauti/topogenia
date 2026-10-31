// GuardWritingRulesTests.cs
// Each provider writes the guards of the common model in its own SQL, and both write the same conditions but for their syntax (spec 3.2, 4.2, ADR 025).

using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Sqlite;
using Topogenia.Persistence.SqlServer;

namespace Topogenia.Architecture.Tests;

/// <summary>
/// The common persistence walks the guards without SQL (<see cref="GuardWriter"/>); each provider writes the operators
/// of its engine (spec 3.2). The SQL of a guard is thus written twice, once per provider: these rules keep the two
/// writings in step. The model of each provider holds, for every guard of <see cref="ModelGuards"/> (partial indexes,
/// CHECK constraints, text guards), what its writer writes; and once the delimiters and the comparison of a decimal are
/// set aside, the two providers write the same condition for every partial index and CHECK constraint of the common
/// model. The models are built without opening a connection.
/// </summary>
public sealed partial class GuardWritingRulesTests
{
    private static readonly IModel SqlServer = ModelOf(new SqlServerEngine("Server=.;Integrated Security=true"));

    private static readonly IModel Sqlite = ModelOf(new SqliteEngine("Data Source=guards.db"));

    [GeneratedRegex(@"CAST\((""[^""]*"") AS REAL\)")]
    private static partial Regex ComparedDecimal();

    [GeneratedRegex(@"""([^""]*)""")]
    private static partial Regex Delimited();

    private static IModel ModelOf(DatabaseEngine engine)
    {
        using var context = engine.CreateContext();
        return context.GetService<IDesignTimeModel>().Model;
    }

    /// <summary>Every guard of the common model and of the text columns of a model: its record, its name and its condition.</summary>
    private static List<(Type Record, string Name, GuardCondition Condition)> Guards(IModel model, bool texts) =>
    [
        .. ModelGuards.PartialIndexes.Select(guard => (guard.Record, guard.IndexName, guard.Filter)),
        .. ModelGuards.Checks.Select(guard => (guard.Record, guard.Name, guard.Condition)),
        .. (texts ? ModelGuards.TextChecks(model) : []).Select(guard => (guard.Record, guard.Name, guard.Condition)),
    ];

    /// <summary>The SQL that the model holds for a guard: the filter of its index or the condition of its CHECK constraint.</summary>
    private static string? Stored(IModel model, Type record, string name)
    {
        var entity = model.FindEntityType(record)!;
        return entity.GetIndexes().SingleOrDefault(index => index.Name == name)?.GetFilter()
            ?? entity.GetCheckConstraints().SingleOrDefault(check => check.Name == name)?.Sql;
    }

    /// <summary>A condition of SQLite in the syntax of SQL Server: `[column]`, a decimal column compared as it is.</summary>
    private static string? AsSqlServer(string? sqlite) =>
        sqlite is null ? null : Delimited().Replace(ComparedDecimal().Replace(sqlite, "$1"), "[$1]");

    public static TheoryData<string> Providers => ["SqlServer", "Sqlite"];

    [Theory]
    [MemberData(nameof(Providers))]
    public void Each_provider_holds_in_its_model_the_guards_that_its_writer_writes(string provider)
    {
        var (model, writer) = provider == "SqlServer" ? (SqlServer, (GuardWriter)SqlServerGuards.Writer) : (Sqlite, SqliteGuards.Writer);
        var guards = Guards(model, texts: true);
        Assert.Contains(guards, guard => guard.Condition is AllOf or AnyOf);
        Assert.All(guards, guard =>
            Assert.Equal(writer.Write(guard.Condition, model.FindEntityType(guard.Record)!), Stored(model, guard.Record, guard.Name)));
    }

    [Fact]
    public void Both_providers_write_the_same_conditions_but_for_their_syntax()
    {
        var guards = Guards(SqlServer, texts: false);
        Assert.Contains(guards, guard => Stored(Sqlite, guard.Record, guard.Name)!.Contains("CAST(", StringComparison.Ordinal));
        Assert.All(guards, guard =>
            Assert.Equal(Stored(SqlServer, guard.Record, guard.Name), AsSqlServer(Stored(Sqlite, guard.Record, guard.Name))));
    }
}
