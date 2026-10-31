// SearchParityTests.cs
// Both engines find the same rows for a search by a part of a text, and refuse alike the text functions they would answer otherwise (ADR 038).

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Sqlite;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Collation;

/// <summary>
/// SQLite compares `LIKE` without the case of the ASCII letters, SQL Server in the binary collation of the column with it;
/// SQLite refused a `Contains` whose text ends with a space, which SQL Server answered; `ToUpper`, `Like`, `IndexOf`,
/// `Substring` and `Length` give other rows on each engine (gate P3, round 3, data finding M1). A name of the directory
/// cut on a space made a user that no engine could write (fidelity finding MAJ-1), and a NUL character escaped the
/// guards of SQLite (data finding m3).
/// </summary>
public sealed class SearchParityTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private const string Name = "Alpha Valider la COMMANDE n";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A project written, and one more user named <see cref="Name"/>, the only row that the searches may find.</summary>
    private async Task<(TestDatabase Database, AppUser User)> WrittenAsync(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = ProjectGraph.New();
        var user = ProjectGraph.OtherUser(graph.Reader, "SEARCH") with { DisplayName = Name, Language = "fr" };
        await Refusals.SaveAsync(database, [.. graph.Records, user]);
        return (database, user);
    }

    private static Task<int> CountAsync(AppDbContext context, AppUser user, System.Linq.Expressions.Expression<Func<AppUser, bool>> search) =>
        context.Set<AppUser>().Where(row => row.Id == user.Id).Where(search).CountAsync(Token);

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_search_by_the_start_or_the_end_of_a_text_compares_the_case_on_both_engines(EngineKind kind)
    {
        var (database, user) = await WrittenAsync(kind);
        var lower = "alpha valider";
        var exact = "Alpha Valider";
        var end = "COMMANDE n";
        var lowerEnd = "commande n";

        await using var context = database.Context();
        Assert.Equal(0, await CountAsync(context, user, row => row.DisplayName.StartsWith(lower)));
        Assert.Equal(1, await CountAsync(context, user, row => row.DisplayName.StartsWith(exact)));
        Assert.Equal(0, await CountAsync(context, user, row => row.DisplayName.StartsWith("alpha")));
        Assert.Equal(0, await CountAsync(context, user, row => row.DisplayName.EndsWith(lowerEnd)));
        Assert.Equal(1, await CountAsync(context, user, row => row.DisplayName.EndsWith(end)));
        Assert.Equal(0, await CountAsync(context, user, row => row.DisplayName.EndsWith("commande n")));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_search_by_a_part_of_a_text_ending_with_a_space_finds_the_same_rows_on_both_engines(EngineKind kind)
    {
        var (database, user) = await WrittenAsync(kind);
        var word = "Valider ";
        var twoSpaces = "Valider  ";
        var start = "Alpha ";

        await using var context = database.Context();
        Assert.Equal(1, await CountAsync(context, user, row => row.DisplayName.Contains(word)));
        Assert.Equal(1, await CountAsync(context, user, row => row.DisplayName.Contains("la ")));
        Assert.Equal(0, await CountAsync(context, user, row => row.DisplayName.Contains(twoSpaces)));
        Assert.Equal(0, await CountAsync(context, user, row => row.DisplayName.Contains("valider")));
        Assert.Equal(1, await CountAsync(context, user, row => row.DisplayName.StartsWith(start)));
        Assert.Equal(0, await CountAsync(context, user, row => row.Language!.Contains("f ")));
        Assert.Equal(1, await CountAsync(context, user, row => row.Language!.Contains("f")));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_search_that_one_engine_would_answer_otherwise_is_refused_on_both_before_any_sql(EngineKind kind)
    {
        var (database, user) = await WrittenAsync(kind);
        var paddedEnd = "n ";
        var withNul = "Val\0ider";
        var outsideAscii = "fł";

        await using var context = database.Context();
        await AssertRefusedTextAsync(() => CountAsync(context, user, row => row.DisplayName.EndsWith(paddedEnd)), "space");
        await AssertRefusedTextAsync(() => CountAsync(context, user, row => row.DisplayName.Contains(withNul)), "NUL");
        await AssertRefusedTextAsync(() => CountAsync(context, user, row => row.DisplayName.StartsWith(withNul)), "NUL");
        await AssertRefusedTextAsync(() => CountAsync(context, user, row => row.Language!.Contains(outsideAscii)), "ASCII");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_text_functions_that_the_engines_translate_otherwise_are_refused_on_both(EngineKind kind)
    {
        var (database, user) = await WrittenAsync(kind);
        var pattern = "%VALIDER%";

        await using var context = database.Context();
        await AssertRefusedFunctionAsync(() => CountAsync(context, user, row => row.DisplayName.ToUpper() == "X"), "String.ToUpper");
        await AssertRefusedFunctionAsync(() => CountAsync(context, user, row => row.DisplayName.ToLower() == "x"), "String.ToLower");
        await AssertRefusedFunctionAsync(() => CountAsync(context, user, row => row.DisplayName.ToUpperInvariant() == "X"), "String.ToUpperInvariant");
        await AssertRefusedFunctionAsync(() => CountAsync(context, user, row => EF.Functions.Like(row.DisplayName, pattern)), "DbFunctionsExtensions.Like");
        await AssertRefusedFunctionAsync(() => CountAsync(context, user, row => row.DisplayName.IndexOf("V") == 6), "String.IndexOf");
        await AssertRefusedFunctionAsync(() => CountAsync(context, user, row => row.DisplayName.Substring(6) == "x"), "String.Substring");
        await AssertRefusedFunctionAsync(() => CountAsync(context, user, row => row.DisplayName.Length > 3), "String.Length");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_text_holding_a_nul_character_is_refused_before_any_writing(EngineKind kind)
    {
        var (database, user) = await WrittenAsync(kind);
        var nul = ProjectGraph.OtherUser(user, "NUL") with { DisplayName = "Before\0after" };

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => Refusals.SaveAsync(database, nul));

        var refusal = Assert.IsType<ArgumentException>(failure.GetBaseException());
        Assert.Contains("NUL", refusal.Message, StringComparison.Ordinal);
        await using var context = database.Context();
        Assert.False(await context.Set<AppUser>().AnyAsync(row => row.Id == nul.Id, Token));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_user_whose_directory_name_and_email_are_cut_on_a_space_is_written_and_read_back(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = ProjectGraph.New();
        await Refusals.SaveAsync(database, [.. graph.Records]);
        var context = new OperationContext(graph.Owner.Id, DateTime.UtcNow, "fr", AppSettings.Default);
        var entry = new DirectoryEntry(new string('N', 127) + " suite", new string('e', 255) + " rest@x.example");

        var user = UserRules.NewUser(context, $@"FICTITIOUS\{graph.Code}CUT", entry, []);
        await Refusals.SaveAsync(database, user);

        await using var read = database.Context();
        var stored = await read.Set<AppUser>().SingleAsync(row => row.Id == user.Id, Token);
        Assert.Equal((new string('N', 127), new string('e', 255)), (stored.DisplayName, stored.Email));
        Assert.Equal(user, stored);
    }

    [Fact]
    public async Task A_read_only_connection_of_sqlite_compares_the_case_in_a_search_too()
    {
        var (database, user) = await WrittenAsync(EngineKind.Sqlite);
        var readOnly = new SqliteConnectionStringBuilder { DataSource = database.Name, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        var lower = "alpha";
        var exact = "Alpha";

        await using var context = new SqliteEngine(readOnly.ConnectionString).CreateContext();
        Assert.Equal(0, await CountAsync(context, user, row => row.DisplayName.StartsWith(lower)));
        Assert.Equal(1, await CountAsync(context, user, row => row.DisplayName.StartsWith(exact)));
    }

    private static async Task AssertRefusedTextAsync(Func<Task> search, string reason)
    {
        var failure = await Assert.ThrowsAnyAsync<Exception>(search);
        var refusal = Assert.IsType<ArgumentException>(failure.GetBaseException());
        Assert.Contains(reason, refusal.Message, StringComparison.Ordinal);
    }

    private static async Task AssertRefusedFunctionAsync(Func<Task> search, string function)
    {
        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(search);
        Assert.Contains($"{function} is not translated alike by both engines", refusal.Message, StringComparison.Ordinal);
    }
}
