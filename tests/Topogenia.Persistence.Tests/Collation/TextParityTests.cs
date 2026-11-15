// TextParityTests.cs
// Both engines take and refuse the same texts: trailing spaces, widths and ASCII columns, written by the application or by hand (ADR 038).

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Persistence.Errors;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Collation;

/// <summary>
/// SQL Server ignores the trailing spaces of a comparison (`'X' = 'X '`), refuses a text longer than its column (2628)
/// and converts a character of a `varchar` column outside its code page (`ł` read back `l`); SQLite compares the spaces,
/// keeps any length and any character. The converter of the texts refuses such a value before any writing or search,
/// the text guard of each column refuses it when written by hand, and a text too long is an integrity conflict naming
/// that guard on both engines (gate P3, round 2, data finding M1).
/// </summary>
public sealed class TextParityTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<(TestDatabase Database, ProjectGraph Graph)> WrittenAsync(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = ProjectGraph.New();
        await Refusals.SaveAsync(database, [.. graph.Records]);
        return (database, graph);
    }

    private static AppUser UserNamed(ProjectGraph graph, string normalized) =>
        graph.Reader with { Id = Guid.CreateVersion7(), Login = normalized, LoginNormalized = normalized };

    /// <summary>The innermost failure of saving the records, which must be the refusal of the converter.</summary>
    private static async Task AssertRefusedBeforeWritingAsync(TestDatabase database, params object[] records)
    {
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => Refusals.SaveAsync(database, records));
        Assert.IsType<ArgumentException>(failure.GetBaseException());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_login_or_a_code_with_a_trailing_space_is_refused_before_any_writing(EngineKind kind)
    {
        var (database, graph) = await WrittenAsync(kind);
        var login = $@"FICTITIOUS\{graph.Code}TRAIL";
        var user = UserNamed(graph, login);
        await Refusals.SaveAsync(database, user);

        await AssertRefusedBeforeWritingAsync(database, UserNamed(graph, login + " "));
        await AssertRefusedBeforeWritingAsync(database, graph.Project with { Id = Guid.CreateVersion7(), Code = "QZQ " });

        await using var context = database.Context();
        Assert.Equal([user.Id], await context.Set<AppUser>().Where(row => row.LoginNormalized == login).Select(row => row.Id).ToListAsync(Token));
        Assert.False(await context.Set<Project>().AnyAsync(row => row.Code == "QZQ", Token));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_search_by_a_value_with_a_trailing_space_is_refused_on_both_engines(EngineKind kind)
    {
        var (database, graph) = await WrittenAsync(kind);
        var padded = graph.Owner.LoginNormalized + " ";

        await using var context = database.Context();
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => context.Set<AppUser>().CountAsync(user => user.LoginNormalized == padded, Token));

        Assert.IsType<ArgumentException>(failure.GetBaseException());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_language_outside_printable_ascii_is_refused_before_any_writing(EngineKind kind)
    {
        var (database, graph) = await WrittenAsync(kind);

        await AssertRefusedBeforeWritingAsync(database, UserNamed(graph, $@"FICTITIOUS\{graph.Code}LANG") with { Language = "pl-ł€" });
        await AssertRefusedBeforeWritingAsync(database, graph.Project with { Id = Guid.CreateVersion7(), Code = "QZÉ" });
    }

    /// <summary>A value written by hand that the text guard of its column refuses: (table, keys, key values, column, value, guards).</summary>
    public static TheoryData<EngineKind, string> HandWrittenCases()
    {
        var data = new TheoryData<EngineKind, string>();
        foreach (var kind in new[] { EngineKind.Sqlite, EngineKind.SqlServer })
        {
            foreach (var name in HandWritten.Keys)
                data.Add(kind, name);
        }
        return data;
    }

    private static readonly Dictionary<string, Func<ProjectGraph, (UpdateDataOperation Operation, string[] Guards)>> HandWritten = new()
    {
        ["login with a trailing space"] = graph => (Update(TableNames.AppUser, [graph.Reader.Id], "login_normalized", graph.Reader.LoginNormalized + " "),
            ["CK_app_user_login_normalized_text"]),
        ["project code with a trailing space"] = graph => (Update(TableNames.Project, [graph.Project.Id], "code", "QZ "), ["CK_project_code_text"]),
        ["closed list code with a trailing space"] = graph => (Update(TableNames.Step, [graph.Steps[0].Id], "step_type", Codes.Of(StepType.Task) + " "),
            ["CK_step_step_type_text", "CK_step_step_type"]),
        ["language outside ascii but in the code page"] = graph => (Update(TableNames.AppUser, [graph.Reader.Id], "language", "pl-é"),
            ["CK_app_user_language_text"]),
        ["color shorter than its fixed width"] = graph => (Update(TableNames.Lane, [graph.Lanes[0].Id], "color", "#ABC"), ["CK_lane_color_text"]),
    };

    private static UpdateDataOperation Update(string table, object[] key, string column, object value) => Update(table, ["id"], key, column, value);

    private static UpdateDataOperation Update(string table, string[] keys, object[] key, string column, object value)
    {
        var keyRow = new object[1, key.Length];
        for (var i = 0; i < key.Length; i++)
            keyRow[0, i] = key[i];
        return new UpdateDataOperation { Table = table, KeyColumns = keys, KeyValues = keyRow, Columns = [column], Values = new object[,] { { value } } };
    }

    [Theory]
    [MemberData(nameof(HandWrittenCases))]
    public async Task A_text_written_by_hand_that_both_engines_would_not_take_alike_is_refused_by_its_text_guard(EngineKind kind, string name)
    {
        var (database, graph) = await WrittenAsync(kind);
        var (operation, guards) = HandWritten[name](graph);
        await using var context = database.Context();
        operation.Schema = context.Model.GetDefaultSchema();

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => OutOfBand.ApplyAsync(context, operation));

        var error = database.Dialect.Classify(failure, context.Model);
        Assert.Equal(StoreErrorKind.Check, error?.Kind);
        Assert.Contains(error!.Constraint, guards);
    }

    /// <summary>
    /// What no CHECK can align, written by hand only (ADR 038): SQL Server cuts the spaces beyond the width of a column
    /// without error (`A ` in the `char(1)` of a role) and converts a character outside its code page in a `varchar`
    /// before any CHECK sees it (`ł` becomes `l`), so it takes a valid row; SQLite refuses both by the text guard. The
    /// application never writes them: its converter refuses them first on both engines.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_text_written_by_hand_that_sql_server_converts_is_taken_converted_there_and_refused_by_sqlite(EngineKind kind)
    {
        var (database, graph) = await WrittenAsync(kind);
        var role = Update(TableNames.StepRaci, ["step_id", "lane_id"], [graph.Steps[0].Id, graph.Lanes[1].Id], "role", Codes.Of(RaciRole.A) + " ");
        var language = Update(TableNames.AppUser, [graph.Reader.Id], "language", "pl-ł");
        await using var context = database.Context();
        role.Schema = language.Schema = context.Model.GetDefaultSchema();

        if (kind == EngineKind.Sqlite)
        {
            foreach (var operation in new[] { role, language })
            {
                var failure = await Assert.ThrowsAnyAsync<Exception>(() => OutOfBand.ApplyAsync(context, operation));
                Assert.Equal(StoreErrorKind.Check, database.Dialect.Classify(failure, context.Model)?.Kind);
            }
            return;
        }
        await OutOfBand.ApplyAsync(context, role, language);
        await using var reader = database.Context();
        Assert.Equal(RaciRole.A, (await reader.Set<StepRaci>().SingleAsync(row => row.StepId == graph.Steps[0].Id && row.LaneId == graph.Lanes[1].Id, Token)).Role);
        Assert.Equal("pl-l", (await reader.Set<AppUser>().SingleAsync(row => row.Id == graph.Reader.Id, Token)).Language);
    }

    public static TheoryData<EngineKind, string> TooLongCases()
    {
        var data = new TheoryData<EngineKind, string>();
        foreach (var kind in new[] { EngineKind.Sqlite, EngineKind.SqlServer })
        {
            data.Add(kind, "CK_project_name_text");
            data.Add(kind, "CK_project_code_text");
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(TooLongCases))]
    public async Task A_text_longer_than_its_column_is_an_integrity_conflict_naming_its_text_guard(EngineKind kind, string guard)
    {
        var (database, graph) = await WrittenAsync(kind);
        var project = guard == "CK_project_name_text"
            ? graph.Project with { Id = Guid.CreateVersion7(), Code = "QZL", Name = new string('n', 201) }
            : graph.Project with { Id = Guid.CreateVersion7(), Code = "QZQZQZQ" };
        await using var context = database.Context();
        context.Add(project);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync(Token));

        var error = database.Dialect.Classify(failure, context.Model);
        Assert.Equal(new StoreError(StoreErrorKind.Check, guard), error);
        Assert.Equal(ErrorCodes.IntegrityConflict, StoreRefusals.Of(error!)?.Code);
    }

    [Fact]
    public async Task Every_text_column_has_its_text_guard_on_both_engines()
    {
        foreach (var kind in new[] { EngineKind.Sqlite, EngineKind.SqlServer })
        {
            await using var database = TestDatabase.Create(kind);
            await using var context = database.Context();
            foreach (var entity in context.GetService<IDesignTimeModel>().Model.GetEntityTypes())
            {
                var checks = entity.GetCheckConstraints().Select(check => check.Name).ToHashSet();
                foreach (var property in entity.GetProperties().Where(ModelGuards.IsStoredAsText))
                    Assert.Contains(ModelGuards.TextCheckName(entity.GetTableName()!, property.GetColumnName()), checks);
            }
        }
    }
}
