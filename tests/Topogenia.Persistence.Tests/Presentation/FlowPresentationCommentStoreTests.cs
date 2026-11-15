// FlowPresentationCommentStoreTests.cs
// The comments of presentation of the flows on both engines (ADR 099, lot J10): a line per flow and language, lines written together in a unit with the version of each checked, read for some flows, added by an import, kept with a flow in the trash, purged before their flows.

using Topogenia.Application.Presentation;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Flows;
using Topogenia.Domain.Model;
using Topogenia.Domain.Selection;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Trash;
using Topogenia.Persistence.Presentation;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Purge;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Presentation;

public sealed class FlowPresentationCommentStoreTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static FlowPresentationComment Comment(Guid flowId, string text, int version, AppUser author, DateTime at, string language = "fr",
        CommentSource source = CommentSource.Manual) =>
        new(flowId, text, language, source, version, at, author.Id);

    /// <summary>The write of a line over the version read of it.</summary>
    private static FlowCommentWrite Put(FlowPresentationComment line, int read) => new(line.FlowId, line.Language, line, read);

    /// <summary>The removal of the line of a flow in a language, over the version read of it.</summary>
    private static FlowCommentWrite Remove(Guid flowId, string language, int read) => new(flowId, language, null, read);

    /// <summary>One unit of work of the store, as a request runs it.</summary>
    private static async Task InUnitAsync(TestDatabase database, Func<FlowPresentationCommentStore, CancellationToken, Task> work)
    {
        await using var session = database.Session();
        var unit = new UnitOfWork(session);
        var store = new FlowPresentationCommentStore(session);
        await unit.RunAsync(async token =>
        {
            await work(store, token);
            return true;
        }, Token);
    }

    private static Task WriteAsync(TestDatabase database, params FlowCommentWrite[] writes) =>
        InUnitAsync(database, (store, token) => store.WriteAsync(writes, token));

    private static async Task<IReadOnlyList<FlowPresentationComment>> ReadAsync(TestDatabase database, params Guid[] flowIds)
    {
        await using var session = database.Session();
        return [.. (await new FlowPresentationCommentStore(session).OfFlowsAsync(flowIds, Token)).OrderBy(line => line.FlowId).ThenBy(line => line.Language, StringComparer.Ordinal)];
    }

    /// <summary>The live flows of a graph, the one named first.</summary>
    private static Guid[] LiveFlows(ProjectGraph graph) =>
        [graph.LiveFlow.Id, .. graph.Of<Flow>().Where(flow => flow.DeletedAt is null && flow.Id != graph.LiveFlow.Id).Select(flow => flow.Id)];

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_line_is_written_replaced_and_removed_with_its_version_checked(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = await PurgeBench.GraphAsync(database);
        var flows = LiveFlows(graph);
        const string Opening = "Le dossier passe de l'acheteur au fournisseur, par courriel.\n";
        var text = Opening + new string('é', PresentationCommentRules.TextMax - Opening.Length);
        var first = Comment(flows[0], text, 1, graph.Owner, graph.At(30));
        await WriteAsync(database, Put(first, 0));
        Assert.Equal([first], await ReadAsync(database, flows));
        Assert.Equal(PresentationCommentRules.TextMax, (await ReadAsync(database, flows[0]))[0].Text.Length);

        var second = first with { Text = "Seconde version.", Source = CommentSource.Workbook, Version = 2, UpdatedAt = graph.At(31) };
        await WriteAsync(database, Put(second, 1));
        Assert.Equal([second], await ReadAsync(database, flows[0]));

        // A version read before the second is refused, with the author and the time of the second.
        var refusal = await Assert.ThrowsAsync<DomainException>(() => WriteAsync(database, Put(first with { Version = 2 }, 1)));
        Assert.Equal((ErrorCodes.VersionConflict, "flow_presentation_comment", (object?)graph.Owner.Id, (object?)graph.At(31)),
            (refusal.Code, refusal.Details["object"], refusal.Details["updated_by"], refusal.Details["updated_at"]));
        Assert.Equal([second], await ReadAsync(database, flows[0]));

        await WriteAsync(database, Remove(flows[0], "fr", 2));
        Assert.Empty(await ReadAsync(database, flows[0]));
        // A first line read as none while one was written meanwhile is refused too.
        await WriteAsync(database, Put(first, 0));
        await Assert.ThrowsAsync<DomainException>(() => WriteAsync(database, Put(first, 0)));
    }

    /// <summary>A removal, and a write on a line removed meanwhile, check the version read too.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_removal_or_a_write_with_a_version_that_is_no_longer_kept_is_refused(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = await PurgeBench.GraphAsync(database);
        var flow = graph.LiveFlow.Id;
        var first = Comment(flow, "Premier.", 1, graph.Owner, graph.At(30));
        var second = first with { Text = "Second.", Version = 2, UpdatedAt = graph.At(31) };
        await WriteAsync(database, Put(first, 0));
        await WriteAsync(database, Put(second, 1));
        var stale = await Assert.ThrowsAsync<DomainException>(() => WriteAsync(database, Remove(flow, "fr", 1)));
        Assert.Equal(ErrorCodes.VersionConflict, stale.Code);
        Assert.Equal([second], await ReadAsync(database, flow));
        await WriteAsync(database, Remove(flow, "fr", 2));
        var gone = await Assert.ThrowsAsync<DomainException>(() => WriteAsync(database, Put(second with { Version = 3 }, 2)));
        Assert.Equal((ErrorCodes.VersionConflict, (object?)null), (gone.Code, gone.Details["updated_by"]));
        Assert.Empty(await ReadAsync(database, flow));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_lines_of_a_flow_are_kept_by_language_each_with_its_source_and_its_own_version(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = await PurgeBench.GraphAsync(database);
        var flow = graph.LiveFlow.Id;
        var original = Comment(flow, "Le dossier part chez le fournisseur.", 1, graph.Owner, graph.At(30));
        var english = Comment(flow, "The file goes to the supplier.", 1, graph.Owner, graph.At(31), "en", CommentSource.Assistant);
        var german = Comment(flow, "Die Akte geht an den Lieferanten.", 1, graph.Owner, graph.At(32), "de", CommentSource.Workbook);
        await WriteAsync(database, Put(original, 0), Put(english, 0), Put(german, 0));
        Assert.Equal([german, english, original], await ReadAsync(database, flow));

        // One request: the original replaced, a translation removed, the other one left as it is.
        var changed = original with { Text = "Le dossier part chez le fournisseur, par courriel.", Version = 2, UpdatedAt = graph.At(33) };
        await WriteAsync(database, Put(changed, 1), Remove(flow, "en", 1));
        Assert.Equal([german, changed], await ReadAsync(database, flow));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_lines_of_one_request_are_written_all_or_none(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = await PurgeBench.GraphAsync(database);
        var flows = LiveFlows(graph);
        var (flow, other) = (flows[0], flows[1]);
        var kept = Comment(other, "Déjà là.", 3, graph.Owner, graph.At(30));
        await InUnitAsync(database, (store, token) => store.AddAsync([kept], token));
        var created = Comment(flow, "Nouvelle ligne.", 1, graph.Owner, graph.At(31));

        // The second line was read at a version that is no longer kept: the first one is not written either.
        var refusal = await Assert.ThrowsAsync<DomainException>(() => WriteAsync(database, Put(created, 0), Put(kept with { Text = "Changée.", Version = 3 }, 2)));
        Assert.Equal((ErrorCodes.VersionConflict, (object?)graph.At(30)), (refusal.Code, refusal.Details["updated_at"]));
        Assert.Equal([kept], await ReadAsync(database, flow, other));

        await WriteAsync(database, Put(created, 0), Put(kept with { Text = "Changée.", Version = 4 }, 3));
        Assert.Equal(["Nouvelle ligne.", "Changée."], (await ReadAsync(database, flow, other)).OrderBy(line => line.Version).Select(line => line.Text));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_line_is_written_only_in_a_unit_of_work_once_in_a_request_and_on_its_own_flow_and_language(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = await PurgeBench.GraphAsync(database);
        var flows = LiveFlows(graph);
        await using var session = database.Session();
        var store = new FlowPresentationCommentStore(session);
        var comment = Comment(flows[0], "Hors unité.", 1, graph.Owner, graph.At(30));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.WriteAsync([Put(comment, 0)], Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.AddAsync([comment], Token));
        await Assert.ThrowsAsync<ArgumentException>(() => WriteAsync(database, new FlowCommentWrite(flows[1], "fr", comment, 0)));
        await Assert.ThrowsAsync<ArgumentException>(() => WriteAsync(database, new FlowCommentWrite(comment.FlowId, "en", comment, 0)));
        await Assert.ThrowsAsync<ArgumentException>(() => WriteAsync(database, Put(comment, 0), Remove(comment.FlowId, "fr", 0)));
        Assert.Empty(await ReadAsync(database, flows));
        // Nothing to write is nothing done, even outside a unit.
        await store.WriteAsync([], Token);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_comments_of_an_import_are_added_together_and_read_for_their_flows_only(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = await PurgeBench.GraphAsync(database);
        var flows = LiveFlows(graph);
        var comments = flows.Select((flow, index) => Comment(flow, $"Phrase {index}.", 1, graph.Owner, graph.At(30))).ToList();
        var translation = Comment(flows[1], "Sentence 1.", 1, graph.Owner, graph.At(30), "en", CommentSource.Assistant);
        await InUnitAsync(database, (store, token) => store.AddAsync([.. comments, translation], token));
        Assert.Equal(flows.Length + 1, (await ReadAsync(database, flows)).Count);
        Assert.Equal([translation, comments[1]], await ReadAsync(database, flows[1], Guid.CreateVersion7()));
    }

    /// <summary>A flow and a step each have their comment, in their own table: the store of the ones never reads the others.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_comments_of_the_flows_and_those_of_the_steps_are_kept_apart(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = await PurgeBench.GraphAsync(database);
        var (flow, step) = (graph.LiveFlow.Id, graph.LiveFlow.SourceStepId);
        var onFlow = Comment(flow, "Sur la flèche.", 1, graph.Owner, graph.At(30));
        var onStep = new PresentationComment(step, "Sur l'étape.", "fr", CommentSource.Manual, 1, graph.At(30), graph.Owner.Id);
        await using (var session = database.Session())
        {
            var unit = new UnitOfWork(session);
            await unit.RunAsync(async token =>
            {
                await new FlowPresentationCommentStore(session).WriteAsync([Put(onFlow, 0)], token);
                await new PresentationCommentStore(session).WriteAsync([new CommentWrite(step, "fr", onStep, 0)], token);
                return true;
            }, Token);
        }
        Assert.Equal([onFlow], await ReadAsync(database, flow, step));
        await using var reader = database.Session();
        Assert.Equal([onStep], await new PresentationCommentStore(reader).OfStepsAsync([flow, step], Token));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_trashed_project_is_purged_with_every_line_of_the_comments_of_its_flows_and_of_its_steps_and_another_project_keeps_its_own(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var (purged, other) = (await PurgeBench.GraphAsync(database), await PurgeBench.GraphAsync(database));
        // Every flow of the project, the one already in the trash too: its comment waited there for its restoration.
        var mine = purged.Of<Flow>().SelectMany(flow => new[]
        {
            Comment(flow.Id, "À purger.", 1, purged.Owner, purged.At(30)), Comment(flow.Id, "To purge.", 1, purged.Owner, purged.At(30), "en", CommentSource.Workbook),
        }).ToList();
        Assert.Contains(mine, comment => comment.FlowId == purged.DeletedFlow.Id);
        var onSteps = purged.Steps.Select(step => new PresentationComment(step.Id, "À purger.", "fr", CommentSource.Manual, 1, purged.At(30), purged.Owner.Id)).ToList();
        var kept = Comment(other.LiveFlow.Id, "À garder.", 1, other.Owner, other.At(30));
        await InUnitAsync(database, (store, token) => store.AddAsync([.. mine, kept], token));
        await using (var session = database.Session())
        {
            var unit = new UnitOfWork(session);
            await unit.RunAsync(async token =>
            {
                await new PresentationCommentStore(session).AddAsync(onSteps, token);
                return true;
            }, Token);
        }
        var trashedAt = purged.At(100);
        await PurgeBench.TrashAsync(database, purged, trashedAt);
        var plan = await PurgeBench.PlanAsync(database, purged.Project.Id, PurgeBench.AfterRetention(trashedAt));

        await PurgeBench.PurgeAsync(database, purged.Project.Id, plan, 7);

        Assert.Null(await PurgeBench.LoadAsync(database, purged.Project.Id));
        Assert.Empty(await ReadAsync(database, [.. mine.Select(comment => comment.FlowId)]));
        Assert.Equal([kept], await ReadAsync(database, kept.FlowId));
        await using var reader = database.Session();
        Assert.Empty(await new PresentationCommentStore(reader).OfStepsAsync([.. onSteps.Select(comment => comment.StepId)], Token));
    }

    /// <summary>
    /// A flow goes to the trash alone, or with one of its steps: its comment stays as it was while the flow may be restored,
    /// and goes before it when the batch is purged; the comments of the live flows stay.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_flow_in_the_trash_keeps_its_comment_until_its_batch_is_purged_alone_or_with_its_step_and_the_live_flows_keep_theirs(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var scenario = await StoreScenario.CreateAsync(database);
        var steps = new Guid[4];
        var flows = new Guid[3];
        await scenario.RunAsync((workspace, context) =>
        {
            var map = workspace.RootMap.Id;
            for (var i = 0; i < steps.Length; i++)
                steps[i] = StepOperations.Create(workspace, context, new CreateStepRequest(map, workspace.LanesOf(map)[0].Id, i, $"Step {i}")).Id;
            for (var i = 0; i < flows.Length; i++)
                flows[i] = FlowRules.Create(workspace, context, new CreateFlowRequest(map, steps[i], steps[i + 1], null)).Id;
        });
        var comments = flows.Select(flow => Comment(flow, "Dit pendant le passage.", 1, scenario.Actor, FixedClock.Time)).ToList();
        var translation = Comment(flows[0], "Said along the flow.", 2, scenario.Actor, FixedClock.Time, "en", CommentSource.Assistant);
        await InUnitAsync(database, (store, token) => store.AddAsync([.. comments, translation], token));

        // The first flow is deleted alone; the last one goes with its target step.
        await scenario.RunAsync((workspace, context) => FlowRules.Delete(workspace, context, flows[0]));
        await scenario.RunAsync((workspace, context) => GroupDeletion.Delete(workspace, context, workspace.RootMap.Id, new GroupDeletionRequest([steps[3]], null)));
        var trashed = await scenario.LoadAsync();
        Assert.All(new[] { flows[0], flows[2] }, flow => Assert.NotNull(trashed.Flow(flow)!.DeletedAt));
        // In the trash, before the end of the retention: nothing is purged, every line is kept as it was.
        Assert.True(PurgePlanner.Expired(trashed, FixedClock.Time.AddDays(1), AppSettings.Default).IsEmpty);
        Assert.Equal([.. comments, translation], (await ReadAsync(database, flows)).OrderBy(line => line.Version).ThenBy(line => Array.IndexOf(flows, line.FlowId)));

        var plan = PurgePlanner.Expired(trashed, PurgeBench.AfterRetention(FixedClock.Time.AddDays(1)), AppSettings.Default);
        Assert.Equal([flows[0], flows[2]], plan.Records.OfType<Flow>().Select(flow => flow.Id).OrderBy(id => Array.IndexOf(flows, id)));
        await PurgeBench.PurgeAsync(database, scenario.ProjectId, plan, 4);

        Assert.Equal([comments[1]], await ReadAsync(database, flows));
        Assert.Null((await scenario.LoadAsync()).Flow(flows[0]));
    }
}
