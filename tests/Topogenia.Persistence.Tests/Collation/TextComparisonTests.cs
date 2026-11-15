// TextComparisonTests.cs
// Every comparison of text in the database is binary on both engines: unique keys, closed lists and searches (spec 4.3-3, A-5-05, A-5-32).

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Collation;

/// <summary>
/// SQLite compares text in binary; a SQL Server column without a collation of its own takes the one of the database
/// (`French_CI_AS` on the local instance), which ignores case and width. Here the two engines must agree: two values that
/// differ by their case, a sharp s or their width are two values, a closed list refuses its code in another case, and a
/// search finds the exact value only (ADR 038).
/// </summary>
public sealed class TextComparisonTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    /// <summary>`ABC` in full width (U+FF21 to U+FF23), equal to `ABC` for a width-insensitive collation.</summary>
    private const string FullWidthAbc = "ＡＢＣ";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<EngineKind, string, string> LoginPairs()
    {
        var data = new TheoryData<EngineKind, string, string>();
        foreach (var kind in new[] { EngineKind.Sqlite, EngineKind.SqlServer })
        {
            data.Add(kind, "STRASSE", "STRAßE");
            data.Add(kind, "ABC", FullWidthAbc);
        }
        return data;
    }

    private static async Task<(TestDatabase Database, ProjectGraph Graph)> WrittenAsync(TestDatabase database)
    {
        var graph = ProjectGraph.New();
        await Refusals.SaveAsync(database, [.. graph.Records]);
        return (database, graph);
    }

    private static AppUser UserNamed(ProjectGraph graph, string normalized) =>
        graph.Reader with { Id = Guid.CreateVersion7(), Login = normalized, LoginNormalized = normalized };

    [Theory]
    [MemberData(nameof(LoginPairs))]
    public async Task Two_logins_that_only_a_linguistic_collation_takes_for_equal_are_two_users(EngineKind kind, string first, string second)
    {
        var (database, graph) = await WrittenAsync(await databases.OfAsync(kind));
        var prefix = $@"FICTITIOUS\{graph.Code}";
        var users = new[] { UserNamed(graph, prefix + first), UserNamed(graph, prefix + second) };

        await Refusals.SaveAsync(database, users);

        await using var context = database.Context();
        foreach (var user in users)
        {
            var found = await context.Set<AppUser>().Where(row => row.LoginNormalized == user.LoginNormalized).Select(row => row.Id).ToListAsync(Token);
            Assert.Equal([user.Id], found);
        }
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_project_code_in_another_case_is_another_code(EngineKind kind)
    {
        var (database, graph) = await WrittenAsync(await databases.OfAsync(kind));
        var lower = graph.Code.ToLowerInvariant();
        await using (var context = database.Context())
            Assert.False(await context.Set<Project>().AnyAsync(project => project.Code == lower, Token));

        var other = graph.Project with { Id = Guid.CreateVersion7(), Code = lower };
        await Refusals.SaveAsync(database, other);

        await using var reader = database.Context();
        Assert.Equal([other.Id], await reader.Set<Project>().Where(project => project.Code == lower).Select(project => project.Id).ToListAsync(Token));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_search_by_the_lower_case_form_of_a_login_or_a_code_finds_nothing(EngineKind kind)
    {
        var (database, graph) = await WrittenAsync(await databases.OfAsync(kind));
        var login = graph.Owner.LoginNormalized.ToLowerInvariant();
        var lane = graph.Lanes[0].Code.ToLowerInvariant();
        var step = graph.Steps[0].Code.ToLowerInvariant();

        await using var context = database.Context();
        Assert.False(await context.Set<AppUser>().AnyAsync(user => user.LoginNormalized == login, Token));
        Assert.False(await context.Set<Lane>().AnyAsync(row => row.Code == lane, Token));
        Assert.False(await context.Set<Step>().AnyAsync(row => row.Code == step, Token));
        Assert.True(await context.Set<AppUser>().AnyAsync(user => user.LoginNormalized == graph.Owner.LoginNormalized, Token));
    }

    /// <summary>A code of a closed list written in another case, by hand: (table, key columns, key values, column, value, CHECK).</summary>
    public static TheoryData<EngineKind, string> ClosedListCases()
    {
        var data = new TheoryData<EngineKind, string>();
        foreach (var kind in new[] { EngineKind.Sqlite, EngineKind.SqlServer })
        {
            foreach (var name in ClosedLists.Keys)
                data.Add(kind, name);
        }
        return data;
    }

    private static readonly Dictionary<string, Func<ProjectGraph, UpdateDataOperation>> ClosedLists = new()
    {
        ["CK_step_raci_role"] = graph => Update(TableNames.StepRaci, ["step_id", "lane_id"], [graph.Steps[0].Id, graph.Lanes[1].Id],
            "role", Codes.Of(RaciRole.A).ToLowerInvariant()),
        ["CK_project_member_role"] = graph => Update(TableNames.ProjectMember, ["project_id", "user_id"], [graph.Project.Id, graph.Owner.Id],
            "role", Codes.Of(MemberRole.Facilitator).ToUpperInvariant()),
        ["CK_step_step_type"] = graph => Update(TableNames.Step, ["id"], [graph.Steps[0].Id], "step_type", Codes.Of(StepType.Task).ToUpperInvariant()),
        ["CK_project_classification"] = graph => Update(TableNames.Project, ["id"], [graph.Project.Id], "classification",
            Codes.Of(graph.Project.Classification).ToUpperInvariant()),
    };

    private static UpdateDataOperation Update(string table, string[] keys, object[] keyValues, string column, string value)
    {
        var keyRow = new object[1, keyValues.Length];
        for (var i = 0; i < keyValues.Length; i++)
            keyRow[0, i] = keyValues[i];
        return new UpdateDataOperation { Table = table, KeyColumns = keys, KeyValues = keyRow, Columns = [column], Values = new object[,] { { value } } };
    }

    [Theory]
    [MemberData(nameof(ClosedListCases))]
    public async Task A_code_of_a_closed_list_in_another_case_is_refused_by_its_check(EngineKind kind, string check)
    {
        var (database, graph) = await WrittenAsync(await databases.OfAsync(kind));
        var operation = ClosedLists[check](graph);
        await using var context = database.Context();
        operation.Schema = context.Model.GetDefaultSchema();

        var refusal = await Assert.ThrowsAnyAsync<Exception>(() => OutOfBand.ApplyAsync(context, operation));
        Assert.Contains(check, refusal.GetBaseException().Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Labels_sort_by_their_code_points_on_both_engines(EngineKind kind)
    {
        var (database, graph) = await WrittenAsync(await databases.OfAsync(kind));
        string[] names = ["zoe", "Zoé", "ZOE", "Zoe", "Éric", "eric", "Eric", "Éric"];
        var users = names.Select((name, index) => UserNamed(graph, $@"FICTITIOUS\{graph.Code}SORT{index}") with { DisplayName = name }).ToArray();
        await Refusals.SaveAsync(database, users);

        await using var context = database.Context();
        var ids = users.Select(user => user.Id).ToList();
        var sorted = await context.Set<AppUser>().Where(user => ids.Contains(user.Id)).OrderBy(user => user.DisplayName)
            .Select(user => user.DisplayName).ToListAsync(Token);
        Assert.Equal(names.Order(StringComparer.Ordinal), sorted);
    }
}
