// ProjectCodeQueriesTests.cs
// The code proposed from a name and the free code of a project (3.4, lot 3, ADR 111 points 6 and 7): the rule of the trash called, one read, no writing, the administrator alone.

using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Application.Projects;
using Topogenia.Application.Tests.Support;
using Topogenia.Application.Tests.Users;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Projects;

public sealed class ProjectCodeQueriesTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The administrator acting, with projects of the given codes, live or in the trash.</summary>
    private static UsersFixture Administrator(bool inTrash, params string[] codes)
    {
        var fixture = new UsersFixture();
        foreach (var code in codes)
            fixture.OtherProject(code, fixture.Project.Reader, MemberRole.Facilitator, inTrash);
        return fixture.ActingAs(fixture.Project.Admin);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_code_taken_live_or_in_the_trash_gives_the_next_free_one_by_the_rule_of_the_trash(bool inTrash)
    {
        var fixture = Administrator(inTrash, "ACHATS");
        Assert.Equal("ACHATA", (await fixture.Runner.RunAsync(new ProposeProjectCodeQuery("Achats"), Token)).Code);
        Assert.Equal("ACHATA", await fixture.Runner.RunAsync(new FreeProjectCodeQuery("ACHATS"), Token));

        fixture.OtherProject("ACHATA", fixture.Project.Reader, MemberRole.Facilitator, !inTrash);
        Assert.Equal("ACHATB", (await fixture.Runner.RunAsync(new ProposeProjectCodeQuery("Achats"), Token)).Code);
        Assert.Equal("ACHATB", await fixture.Runner.RunAsync(new FreeProjectCodeQuery("ACHATS"), Token));
        Assert.Equal(ProjectCodeRules.TrashCode("ACHATS", ["ACHATS", "ACHATA"]), await fixture.Runner.RunAsync(new FreeProjectCodeQuery("ACHATS"), Token));

        var sample = Administrator(inTrash, "CONGES");
        Assert.Equal("CONGEA", await sample.Runner.RunAsync(new FreeProjectCodeQuery("CONGES"), Token));
    }

    /// <summary>A code no project holds is given back unchanged, even when codes of the same start are taken (only the code itself counts).</summary>
    [Fact]
    public async Task A_free_code_is_given_back_as_it_is()
    {
        var fixture = Administrator(false, "CONGEA", "ACHATA", "GDAA");
        Assert.Equal("CONGES", await fixture.Runner.RunAsync(new FreeProjectCodeQuery("CONGES"), Token));
        Assert.Equal("LEAVE", await fixture.Runner.RunAsync(new FreeProjectCodeQuery(" LEAVE "), Token));
        Assert.Equal("ACHATS", (await fixture.Runner.RunAsync(new ProposeProjectCodeQuery("Achats"), Token)).Code);
        Assert.Equal("GDA", (await fixture.Runner.RunAsync(new ProposeProjectCodeQuery("Gestion des achats"), Token)).Code);
        Assert.Equal("TDFF", (await fixture.Runner.RunAsync(new ProposeProjectCodeQuery("Traitement des factures fournisseurs"), Token)).Code);
        Assert.Null((await fixture.Runner.RunAsync(new ProposeProjectCodeQuery("発注"), Token)).Code);
    }

    [Fact]
    public async Task The_queries_read_the_codes_once_and_write_nothing()
    {
        var fixture = Administrator(true, "ACHATS");
        var (runner, reads) = Counting(fixture);
        var before = fixture.Database.All<IRecord>().Count();

        Assert.Equal("ACHATA", (await runner.RunAsync(new ProposeProjectCodeQuery("Achats"), Token)).Code);
        Assert.Equal(["ACHA"], reads);
        reads.Clear();
        Assert.Equal("CONGES", await runner.RunAsync(new FreeProjectCodeQuery("CONGES"), Token));
        Assert.Equal(["CONG"], reads);
        reads.Clear();
        Assert.Null((await runner.RunAsync(new ProposeProjectCodeQuery("2026"), Token)).Code);
        Assert.Null((await runner.RunAsync(new ProposeProjectCodeQuery("RH"), Token)).Code);
        Assert.Empty(reads);

        Assert.Equal(0, fixture.Database.Commits);
        Assert.DoesNotContain("save", fixture.Database.Calls);
        Assert.DoesNotContain("lock", fixture.Database.Calls);
        Assert.Empty(fixture.Database.Entries);
        Assert.Equal(before, fixture.Database.All<IRecord>().Count());
    }

    [Fact]
    public async Task A_user_who_is_not_administrator_is_refused()
    {
        var fixture = Administrator(false, "ACHATS");
        var (runner, reads) = Counting(fixture);
        foreach (var user in new[] { fixture.Project.Facilitator, fixture.Project.Contributor, fixture.Project.Reader, fixture.Project.Stranger })
        {
            fixture.ActingAs(user);
            Assert.Equal(ErrorCodes.AccessDenied, (await Assert.ThrowsAsync<DomainException>(() =>
                runner.RunAsync(new ProposeProjectCodeQuery("Achats"), Token))).Code);
            Assert.Equal(ErrorCodes.AccessDenied, (await Assert.ThrowsAsync<DomainException>(() =>
                runner.RunAsync(new FreeProjectCodeQuery("ACHATS"), Token))).Code);
        }
        Assert.Empty(reads);
        Assert.Empty(fixture.Database.Calls);
    }

    [Fact]
    public async Task A_name_longer_than_200_characters_is_refused()
    {
        var fixture = Administrator(false);
        var (runner, reads) = Counting(fixture);
        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            runner.RunAsync(new ProposeProjectCodeQuery(new string('a', ProposeProjectCodeQuery.NameMax + 1)), Token));
        Assert.Equal((ErrorCodes.InvalidValue, (object?)ProposeProjectCodeQuery.NameField), (refusal.Code, refusal.Details["field"]));
        Assert.Equal((200, "name"), (ProposeProjectCodeQuery.NameMax, ProposeProjectCodeQuery.NameField));
        Assert.Empty(reads);
        Assert.Equal("AAAAAA", (await runner.RunAsync(new ProposeProjectCodeQuery(new string('a', ProposeProjectCodeQuery.NameMax)), Token)).Code);
    }

    /// <summary>A runner of the fixture whose reads across the projects note the prefix of each read of the codes, and refuse any other read.</summary>
    private static (UserRunner Runner, List<string> Reads) Counting(UsersFixture fixture)
    {
        var reads = new List<string>();
        var ports = fixture.Ports with { Projects = new CodeReads(fixture.Projects, reads) };
        var runner = new UserRunner(fixture.Project.Unit, new InMemoryAccessReader(fixture.Database), ports, fixture.Project.CurrentUser,
            fixture.Project.Clock, AppSettings.Default, UsersFixture.Languages, fixture.Project.Versions, fixture.SignedIn);
        return (runner, reads);
    }

    private sealed class CodeReads(IProjectReader inner, List<string> reads) : IProjectReader
    {
        public Task<IReadOnlyList<string>> CodesStartingWithAsync(string prefix, CancellationToken cancellationToken)
        {
            reads.Add(prefix);
            return inner.CodesStartingWithAsync(prefix, cancellationToken);
        }

        public Task<IReadOnlyList<ProjectListRow>> LiveProjectsAsync(ProjectVisibility visibility, CancellationToken cancellationToken) => throw Unexpected();

        public Task<IReadOnlyList<TrashRow>> TrashAsync(ProjectVisibility visibility, CancellationToken cancellationToken) => throw Unexpected();

        public Task<IReadOnlyList<ExistingProjectCode>> CodesAsync(string code, CancellationToken cancellationToken) => throw Unexpected();

        public Task<string?> LiveCodeAsync(Guid projectId, ProjectVisibility visibility, CancellationToken cancellationToken) => throw Unexpected();

        public Task<Guid?> LiveIdAsync(string code, ProjectVisibility visibility, CancellationToken cancellationToken) => throw Unexpected();

        public Task<IReadOnlyList<Guid>> TargetsOfAsync(Guid sourceId, CancellationToken cancellationToken) => throw Unexpected();

        private static InvalidOperationException Unexpected() => new("The queries of the code read the codes only.");
    }
}
