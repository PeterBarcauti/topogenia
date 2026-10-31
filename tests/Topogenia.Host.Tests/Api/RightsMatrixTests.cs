// RightsMatrixTests.cs
// Every entry point of the API against every role: 401 anonymous, 404 non-member, 403 weaker role, success from the declared right (annex A 1, A-4-07).

using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Topogenia.Host.Api;
using Topogenia.Host.Tests.Dissolutions;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Api;

/// <summary>
/// One case per route and engine. Each case draws a fresh project created by a second administrator
/// (<see cref="Logins.Owner"/>): a detailed row with a document, a flow, and when the route needs it a frame of
/// dissolution or a project in the trash. It then sends the same valid request as each role: anonymous, stranger
/// (authenticated, member of nothing), reader, contributor (who holds no lane: never the right to fill), facilitator,
/// administrator (member of nothing: facilitator of every project, `Droits.cs:30`, checked on each project drawn). The
/// refusals come first, without `If-Match`; the lowest role that is granted is then tried, and the administrator on a
/// second fresh project, since a granted call may change or remove its target. Granted means the success of the route
/// (2xx): a valid body, and the `If-Match` of the record read when the handler of the route reads it (found on the
/// endpoints, <see cref="IfMatchRequirement.ReadsIfMatch"/>), so that no refusal of the server mode, no validation and no
/// error passes for a granted right. The health, the command Quit and the sample project, whose answer depends on the mode
/// and not only on the role, are out of the table: `CommonRulesTests`, `ServerRunTests`, `DesktopServerTests` and
/// `SampleProjectRoutesTests` prove them.
/// </summary>
public sealed partial class RightsMatrixTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    public enum Right
    {
        Authenticated,
        Reader,
        SheetFill,
        Facilitator,
        Admin,
    }

    private enum Role
    {
        Anonymous,
        Stranger,
        Reader,
        Contributor,
        Facilitator,
        Admin,
    }

    [GeneratedRegex(@":[a-z]+\}")]
    private static partial Regex Constraint();

    public static TheoryData<EngineKind, string> Cases()
    {
        var cases = new TheoryData<EngineKind, string>();
        foreach (var kind in new[] { EngineKind.Sqlite, EngineKind.SqlServer })
        {
            foreach (var entry in Table)
                cases.Add(kind, entry.Name);
        }
        return cases;
    }

    /// <summary>The routes of the table and those left out on purpose are exactly the routes of the host.</summary>
    public static IReadOnlyList<string> Routes =>
        [.. Table.Select(entry => entry.Name), "GET /api/health", "POST /api/desktop/quit", "POST /api/desktop/sample-project"];

    [Fact]
    public void The_table_holds_every_route_of_the_host_once()
    {
        Assert.Equal(Routes.Count, Routes.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(RouteTableTests.ExpectedRoutes.Order(StringComparer.Ordinal), Routes.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task The_table_knows_the_version_to_send_for_every_route_that_reads_if_match()
    {
        var reading = RoutesReadingIfMatch(await hosts.OfAsync(EngineKind.Sqlite));
        Assert.NotEmpty(reading);
        Assert.Empty(Table.Where(entry => reading.Contains(entry.Name) && entry.Version is null).Select(entry => entry.Name));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Each_role_is_refused_or_granted_as_the_right_of_the_route_says(EngineKind kind, string route)
    {
        var entry = Table.Single(candidate => candidate.Name == route);
        var host = await hosts.OfAsync(kind);
        var readsIfMatch = RoutesReadingIfMatch(host).Contains(route);
        var targets = await TargetsAsync(host, entry);
        foreach (var role in Enum.GetValues<Role>())
        {
            if (Expected(entry.Right, role) is { } refusal)
            {
                var reply = await SendAsync(targets, entry, role, null);
                Assert.True(reply.Status == refusal, $"{route} as {role}: expected {refusal}, got {reply.Status} {reply.Body}");
            }
        }
        var lowest = Enum.GetValues<Role>().First(role => Expected(entry.Right, role) is null);
        AssertGranted(route, lowest, await GrantedAsync(targets, entry, lowest, readsIfMatch));
        if (lowest != Role.Admin)
            AssertGranted(route, Role.Admin, await GrantedAsync(await TargetsAsync(host, entry), entry, Role.Admin, readsIfMatch));
    }

    /// <summary>
    /// The request of a granted role, after it put on its stack what undo (one operation) or redo (one undone) needs, with
    /// the version of its record when the route reads `If-Match`.
    /// </summary>
    private static async Task<ApiResponse> GrantedAsync(Targets targets, Entry entry, Role role, bool readsIfMatch)
    {
        using var client = ClientOf(targets, role);
        if (entry.Prepare is not null)
            await entry.Prepare(targets);
        if (entry.Stack > 0)
        {
            (await client.PostAsync($"/api/maps/{targets.Root}/lanes", new { name = "Pile" })).Ok();
            if (entry.Stack > 1)
                (await client.PostAsync($"/api/maps/{targets.Root}/undo")).Ok();
        }
        int? version = null;
        if (readsIfMatch)
        {
            Assert.True(entry.Version is not null, $"{entry.Name} reads If-Match: the table must say which version to send.");
            version = await entry.Version(targets, client);
        }
        return await SendAsync(targets, entry, role, version);
    }

    /// <summary>The status that refuses <paramref name="role"/> on a route of <paramref name="right"/>, null when it is granted.</summary>
    private static int? Expected(Right right, Role role) => (right, role) switch
    {
        (_, Role.Anonymous) => 401,
        (_, Role.Admin) => null,
        (Right.Authenticated, _) => null,
        (Right.Admin, _) => 403,
        (_, Role.Stranger) => 404,
        (Right.Reader, _) => null,
        (_, Role.Facilitator) => null,
        _ => 403,
    };

    private static void AssertGranted(string route, Role role, ApiResponse reply) =>
        Assert.True(reply.Status is >= 200 and <= 299, $"{route} as {role}: the right should be granted and the route succeed, got {reply.Status} {reply.Body}");

    private static async Task<ApiResponse> SendAsync(Targets targets, Entry entry, Role role, int? version)
    {
        using var client = ClientOf(targets, role);
        return await client.SendAsync(new HttpMethod(entry.Method), entry.Url(targets), entry.Body(targets), version);
    }

    private static ApiClient ClientOf(Targets targets, Role role) => role switch
    {
        Role.Anonymous => targets.Project.Host.As(null),
        Role.Stranger => targets.Project.Stranger,
        Role.Reader => targets.Project.Reader,
        Role.Contributor => targets.Project.Contributor,
        Role.Facilitator => targets.Project.Facilitator,
        _ => targets.Project.Admin,
    };

    /// <summary>The routes of the host whose handler reads `If-Match`, named as the table names them.</summary>
    private static HashSet<string> RoutesReadingIfMatch(ApiHost host) =>
        host.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<MethodInfo>() is { } handler && IfMatchRequirement.ReadsIfMatch(handler))
            .SelectMany(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods
                .Select(method => $"{method} {Constraint().Replace(endpoint.RoutePattern.RawText!.TrimEnd('/'), "}")}"))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>A fresh project of <see cref="Logins.Owner"/>, of which the administrator of the matrix is checked to be no member.</summary>
    private static async Task<Targets> TargetsAsync(ApiHost host, Entry entry)
    {
        var project = await ApiProject.CreateAsync(host, Logins.Owner);
        await AssertAdminIsNoMemberAsync(project);
        var row = await DetailedRow.CreateAsync(project, "Bon de commande");
        var map = await project.MapAsync();
        var flow = map.GetProperty("flows").EnumerateArray()
            .Single(item => item.GetProperty("source_step_id").GetGuid() == row.Before).GetProperty("id").GetGuid();
        using var facilitator = project.Facilitator;
        var otherLane = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Qualité" })).Ok().GetProperty("id").GetGuid();
        var subMapLane = (await project.MapAsync(row.SubMap)).GetProperty("lanes")[0].GetProperty("id").GetGuid();
        Guid? frame = entry.NeedsFrame
            ? (await facilitator.PostAsync($"/api/steps/{row.Macro}/dissolve", null, await row.MacroVersionAsync())).Ok().GetProperty("id").GetGuid()
            : null;
        Guid? trashed = null;
        if (entry.NeedsTrash)
        {
            var other = await ApiProject.CreateAsync(host, Logins.Owner);
            await AssertAdminIsNoMemberAsync(other);
            (await facilitator.DeleteAsync($"/api/projects/{other.Id}?confirmation={other.Code}", await ProjectVersionAsync(other))).Ok(204);
            trashed = other.Id;
        }
        var codes = (Code(map, row.Before), Code(map, row.After));
        var targets = new Targets(project, row, flow, otherLane, subMapLane, codes, frame, trashed);
        if (entry.NeedsUse)
            (await facilitator.PutAsync(targets.UseUrl, new { wait_hours = 2 }, NoRecord)).Ok();
        await PrepareSheetAsync(targets, entry.Sheet);
        return targets;
    }

    /// <summary>
    /// The sheet of Before saved as a draft (<see cref="SheetState.Draft"/>), then validated (<see cref="SheetState.Validated"/>),
    /// then questioned (<see cref="SheetState.Questioned"/>).
    /// </summary>
    private static async Task PrepareSheetAsync(Targets targets, SheetState state)
    {
        if (state == SheetState.None)
            return;
        using var facilitator = targets.Project.Facilitator;
        var url = $"/api/steps/{targets.Row.Before}/sheet";
        (await facilitator.PutAsync(url, StepSheets.Purpose("Commander"), await SheetVersionAsync(targets))).Ok();
        if (state is SheetState.Validated or SheetState.Questioned)
            (await facilitator.PostAsync($"{url}/validate", null, await SheetVersionAsync(targets))).Ok();
        if (state == SheetState.Questioned)
            (await facilitator.PostAsync($"{url}/clarification", new { text = "Pourquoi ?" }, await SheetVersionAsync(targets))).Ok();
    }

    private static string Code(JsonElement map, Guid stepId) => StructureCalls.StepOf(map, stepId).GetProperty("code").GetString()!;

    private static async Task AssertAdminIsNoMemberAsync(ApiProject project)
    {
        using var facilitator = project.Facilitator;
        var members = (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("members").EnumerateArray()
            .Select(member => member.GetProperty("login").GetString()).ToList();
        Assert.Contains(Logins.Owner, members, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(Logins.Admin, members, StringComparer.OrdinalIgnoreCase);
    }
}
