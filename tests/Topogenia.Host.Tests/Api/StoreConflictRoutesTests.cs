// StoreConflictRoutesTests.cs
// A guard of the database met by a write is answered 409 through the routes: CONFLIT_UNICITE naming the index, CONFLIT_INTEGRITE naming the foreign key or the CHECK (A-1.1-09, A-1.1-10, A-2.16-11, A-3-03, A-3-25, A-4-53).

using Microsoft.Extensions.DependencyInjection;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Host.Errors;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Api;

/// <summary>
/// The project lock serialises the writes of one project (spec 4.7): two requests of the tests cannot meet between the
/// reading and the writing. The variant of the host writes instead, through the real store and its real base, the change
/// set as a concurrent writer would have made it stale: the new step on a cell taken meanwhile (the race of the
/// 1.x test of two creations in one cell), the new flow to a step removed meanwhile; or as a rule missed by the domain
/// would have made it (weakness 12 of the 1.x, A-4-53): the new step on a row past the CHECK of the grid. The reply is the refusal of the
/// store as the API gives it, in French by default and in English when asked, and nothing is written.
/// </summary>
public sealed class StoreConflictRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private static readonly ErrorMessages Messages = ErrorMessages.Embedded();

    /// <summary>The variant whose store rewrites the created records of type <typeparamref name="T"/> before writing them.</summary>
    private static Action<IServiceCollection> Rewriting<T>(Func<T, T> rewrite)
        where T : IRecord => services =>
    {
        services.AddScoped<WorkspaceStore>();
        services.AddScoped<IWorkspaceStore>(provider => new StaleStore(provider.GetRequiredService<WorkspaceStore>(), changes => new ChangeSet(
            [.. changes.Changes.Select(change => change is { Kind: ChangeKind.Created, After: T record } ? change with { After = rewrite(record) } : change)])));
    };

    private static void AssertMessages(ApiResponse french, ApiResponse english, string code)
    {
        Assert.Equal(Messages.Of("fr")[code], french.Json.GetProperty("message").GetString());
        Assert.Equal(Messages.Of("en")[code], english.Json.GetProperty("message").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_written_on_a_cell_taken_meanwhile_is_refused_409_unique_conflict_naming_the_index_and_nothing_is_written(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        var taken = await project.AddStepAsync(0, "Commander");
        var url = $"/api/maps/{project.RootMapId}/steps";
        var body = new { lane_id = project.FirstLaneId, column = 5, label = "Payer" };
        await project.Host.WithVariantAsync(Rewriting<Step>(step => step with { Column = 0, Row = 0 }), async clientOf =>
        {
            using var facilitator = clientOf(Logins.Facilitator);
            var french = await facilitator.PostAsync(url, body);
            Assert.Equal(IndexNames.LiveStepOfCell, french.Refused(409, "CONFLIT_UNICITE").GetProperty("constraint").GetString());
            var english = await facilitator.SendAsync(HttpMethod.Post, url, body, null, ("Accept-Language", "en"));
            english.Refused(409, "CONFLIT_UNICITE");
            AssertMessages(french, english, "CONFLIT_UNICITE");
        });
        Assert.Equal([taken.Id], (await project.MapAsync()).GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("id").GetGuid()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_flow_written_to_a_step_removed_meanwhile_is_refused_409_integrity_conflict_naming_the_foreign_key_and_nothing_is_written(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        var (source, target) = (await project.AddStepAsync(0, "Commander"), await project.AddStepAsync(1, "Payer"));
        var url = $"/api/maps/{project.RootMapId}/flows";
        var body = new { source_step_id = source.Id, target_step_id = target.Id };
        await project.Host.WithVariantAsync(Rewriting<Flow>(flow => flow with { TargetStepId = Guid.CreateVersion7() }), async clientOf =>
        {
            using var facilitator = clientOf(Logins.Facilitator);
            var french = await facilitator.PostAsync(url, body);
            var constraint = french.Refused(409, "CONFLIT_INTEGRITE").GetProperty("constraint");
            Assert.Equal(kind == EngineKind.SqlServer ? "FK_flow_target_step" : null, constraint.GetString());
            var english = await facilitator.SendAsync(HttpMethod.Post, url, body, null, ("Accept-Language", "en"));
            english.Refused(409, "CONFLIT_INTEGRITE");
            AssertMessages(french, english, "CONFLIT_INTEGRITE");
        });
        Assert.Empty(StructureCalls.FlowPairs(await project.MapAsync()));
        using var direct = project.Facilitator;
        Assert.Equal([(source.Id, target.Id)], StructureCalls.FlowPairs((await direct.PostAsync(url, body)).Ok().GetProperty("map")));
    }

    /// <summary>
    /// A CHECK of the base met beyond the domain (here the bound of the rows of the grid, <see cref="Cell.MaxRow"/>, which
    /// the domain checks first) is a refusal of integrity naming its constraint, never an internal error (A-4-53, AR-80).
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_row_written_out_of_a_check_constraint_is_refused_409_integrity_conflict_naming_it_and_never_500(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        var url = $"/api/maps/{project.RootMapId}/steps";
        var body = new { lane_id = project.FirstLaneId, column = 0, label = "Commander" };
        await project.Host.WithVariantAsync(Rewriting<Step>(step => step with { Row = Cell.MaxRow + 1 }), async clientOf =>
        {
            using var facilitator = clientOf(Logins.Facilitator);
            var french = await facilitator.PostAsync(url, body);
            Assert.NotEqual(500, french.Status);
            Assert.Equal("CK_step_row_index", french.Refused(409, "CONFLIT_INTEGRITE").GetProperty("constraint").GetString());
            var english = await facilitator.SendAsync(HttpMethod.Post, url, body, null, ("Accept-Language", "en"));
            Assert.Equal("CK_step_row_index", english.Refused(409, "CONFLIT_INTEGRITE").GetProperty("constraint").GetString());
            AssertMessages(french, english, "CONFLIT_INTEGRITE");
        });
        Assert.Empty((await project.MapAsync()).GetProperty("steps").EnumerateArray());
        using var direct = project.Facilitator;
        var written = (await direct.PostAsync(url, body)).Ok().GetProperty("map").GetProperty("steps");
        Assert.Equal([0], written.EnumerateArray().Select(step => step.GetProperty("row").GetInt32()));
    }

    /// <summary>The store of the host, whose writing receives the change set made stale by <paramref name="stale"/>.</summary>
    private sealed class StaleStore(WorkspaceStore inner, Func<ChangeSet, ChangeSet> stale) : IWorkspaceStore
    {
        public Task<ProjectWorkspace?> LoadAsync(Guid projectId, WorkspaceContent content, CancellationToken cancellationToken) =>
            inner.LoadAsync(projectId, content, cancellationToken);

        public Task SaveAsync(ChangeSet changes, CancellationToken cancellationToken) => inner.SaveAsync(stale(changes), cancellationToken);
    }
}
