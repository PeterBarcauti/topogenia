// PresentationCommentStoreTests.cs
// The comments of presentation on both engines (ADR 099 revised): a line per step and language, lines written together in a unit with the version of each checked, read for some steps, added by an import, purged before their steps.

using Topogenia.Application.Presentation;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Selection;
using Topogenia.Domain.Trash;
using Topogenia.Persistence.Presentation;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Purge;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Presentation;

public sealed class PresentationCommentStoreTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static PresentationComment Comment(Guid stepId, string text, int version, AppUser author, DateTime at, string language = "fr",
        CommentSource source = CommentSource.Manual) =>
        new(stepId, text, language, source, version, at, author.Id);

    /// <summary>The write of a line over the version read of it.</summary>
    private static CommentWrite Put(PresentationComment line, int read) => new(line.StepId, line.Language, line, read);

    /// <summary>The removal of the line of a step in a language, over the version read of it.</summary>
    private static CommentWrite Remove(Guid stepId, string language, int read) => new(stepId, language, null, read);

    /// <summary>One unit of work of the store, as a request runs it.</summary>
    private static async Task InUnitAsync(TestDatabase database, Func<PresentationCommentStore, CancellationToken, Task> work)
    {
        await using var session = database.Session();
        var unit = new UnitOfWork(session);
        var store = new PresentationCommentStore(session);
        await unit.RunAsync(async token =>
        {
            await work(store, token);
            return true;
        }, Token);
    }

    private static Task WriteAsync(TestDatabase database, params CommentWrite[] writes) =>
        InUnitAsync(database, (store, token) => store.WriteAsync(writes, token));

    private static async Task<IReadOnlyList<PresentationComment>> ReadAsync(TestDatabase database, params Guid[] stepIds)
    {
        await using var session = database.Session();
        return [.. (await new PresentationCommentStore(session).OfStepsAsync(stepIds, Token)).OrderBy(line => line.StepId).ThenBy(line => line.Language, StringComparer.Ordinal)];
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_line_is_written_replaced_and_removed_with_its_version_checked(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = await PurgeBench.GraphAsync(database);
        var step = graph.Steps[1].Id;
        const string Opening = "L'acheteur vérifie la demande, puis l'envoie au fournisseur.\n";
        var text = Opening + new string('é', PresentationCommentRules.TextMax - Opening.Length);
        var first = Comment(step, text, 1, graph.Owner, graph.At(30));
        await WriteAsync(database, Put(first, 0));
        Assert.Equal([first], await ReadAsync(database, step, graph.Steps[2].Id));
        Assert.Equal(PresentationCommentRules.TextMax, (await ReadAsync(database, step))[0].Text.Length);

        var second = first with { Text = "Seconde version.", Source = CommentSource.Workbook, Version = 2, UpdatedAt = graph.At(31) };
        await WriteAsync(database, Put(second, 1));
        Assert.Equal([second], await ReadAsync(database, step));

        // A version read before the second is refused, with the author and the time of the second.
        var refusal = await Assert.ThrowsAsync<DomainException>(() => WriteAsync(database, Put(first with { Version = 2 }, 1)));
        Assert.Equal((ErrorCodes.VersionConflict, "presentation_comment", (object?)graph.Owner.Id, (object?)graph.At(31)),
            (refusal.Code, refusal.Details["object"], refusal.Details["updated_by"], refusal.Details["updated_at"]));
        Assert.Equal([second], await ReadAsync(database, step));

        await WriteAsync(database, Remove(step, "fr", 2));
        Assert.Empty(await ReadAsync(database, step));
        // A first line read as none while one was written meanwhile is refused too.
        await WriteAsync(database, Put(first, 0));
        await Assert.ThrowsAsync<DomainException>(() => WriteAsync(database, Put(first, 0)));
    }

    /// <summary>A removal, and a write on a line removed meanwhile, check the version read too (witness 426).</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_removal_or_a_write_with_a_version_that_is_no_longer_kept_is_refused(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = await PurgeBench.GraphAsync(database);
        var step = graph.Steps[3].Id;
        var first = Comment(step, "Premier.", 1, graph.Owner, graph.At(30));
        var second = first with { Text = "Second.", Version = 2, UpdatedAt = graph.At(31) };
        await WriteAsync(database, Put(first, 0));
        await WriteAsync(database, Put(second, 1));
        var stale = await Assert.ThrowsAsync<DomainException>(() => WriteAsync(database, Remove(step, "fr", 1)));
        Assert.Equal(ErrorCodes.VersionConflict, stale.Code);
        Assert.Equal([second], await ReadAsync(database, step));
        await WriteAsync(database, Remove(step, "fr", 2));
        var gone = await Assert.ThrowsAsync<DomainException>(() => WriteAsync(database, Put(second with { Version = 3 }, 2)));
        Assert.Equal((ErrorCodes.VersionConflict, (object?)null), (gone.Code, gone.Details["updated_by"]));
        Assert.Empty(await ReadAsync(database, step));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_lines_of_a_step_are_kept_by_language_each_with_its_source_and_its_own_version(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = await PurgeBench.GraphAsync(database);
        var step = graph.Steps[1].Id;
        var original = Comment(step, "L'acheteur vérifie la demande.", 1, graph.Owner, graph.At(30));
        var english = Comment(step, "The buyer checks the request.", 1, graph.Owner, graph.At(31), "en", CommentSource.Assistant);
        var german = Comment(step, "Der Einkäufer prüft die Anfrage.", 1, graph.Owner, graph.At(32), "de", CommentSource.Workbook);
        await WriteAsync(database, Put(original, 0), Put(english, 0), Put(german, 0));
        Assert.Equal([german, english, original], await ReadAsync(database, step));

        // One request: the original replaced, a translation removed, the other one left as it is.
        var changed = original with { Text = "L'acheteur vérifie la demande et la transmet.", Version = 2, UpdatedAt = graph.At(33) };
        await WriteAsync(database, Put(changed, 1), Remove(step, "en", 1));
        Assert.Equal([german, changed], await ReadAsync(database, step));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_lines_of_one_request_are_written_all_or_none(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = await PurgeBench.GraphAsync(database);
        var (step, other) = (graph.Steps[1].Id, graph.Steps[2].Id);
        var kept = Comment(other, "Déjà là.", 3, graph.Owner, graph.At(30));
        await InUnitAsync(database, (store, token) => store.AddAsync([kept], token));
        var created = Comment(step, "Nouvelle ligne.", 1, graph.Owner, graph.At(31));

        // The second line was read at a version that is no longer kept: the first one is not written either.
        var refusal = await Assert.ThrowsAsync<DomainException>(() => WriteAsync(database, Put(created, 0), Put(kept with { Text = "Changée.", Version = 3 }, 2)));
        Assert.Equal((ErrorCodes.VersionConflict, (object?)graph.At(30)), (refusal.Code, refusal.Details["updated_at"]));
        Assert.Equal([kept], await ReadAsync(database, step, other));

        await WriteAsync(database, Put(created, 0), Put(kept with { Text = "Changée.", Version = 4 }, 3));
        Assert.Equal(["Nouvelle ligne.", "Changée."], (await ReadAsync(database, step, other)).OrderBy(line => line.Version).Select(line => line.Text));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_line_is_written_only_in_a_unit_of_work_once_in_a_request_and_on_its_own_step_and_language(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = await PurgeBench.GraphAsync(database);
        await using var session = database.Session();
        var store = new PresentationCommentStore(session);
        var comment = Comment(graph.Steps[1].Id, "Hors unité.", 1, graph.Owner, graph.At(30));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.WriteAsync([Put(comment, 0)], Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.AddAsync([comment], Token));
        await Assert.ThrowsAsync<ArgumentException>(() => WriteAsync(database, new CommentWrite(graph.Steps[2].Id, "fr", comment, 0)));
        await Assert.ThrowsAsync<ArgumentException>(() => WriteAsync(database, new CommentWrite(comment.StepId, "en", comment, 0)));
        await Assert.ThrowsAsync<ArgumentException>(() => WriteAsync(database, Put(comment, 0), Remove(comment.StepId, "fr", 0)));
        Assert.Empty(await ReadAsync(database, graph.Steps[1].Id, graph.Steps[2].Id));
        // Nothing to write is nothing done, even outside a unit.
        await store.WriteAsync([], Token);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_comments_of_an_import_are_added_together_and_read_for_their_steps_only(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = await PurgeBench.GraphAsync(database);
        var comments = graph.Steps.Take(3).Select((step, index) => Comment(step.Id, $"Phrase {index}.", 1, graph.Owner, graph.At(30))).ToList();
        var translation = Comment(comments[1].StepId, "Sentence 1.", 1, graph.Owner, graph.At(30), "en", CommentSource.Assistant);
        await InUnitAsync(database, (store, token) => store.AddAsync([.. comments, translation], token));
        Assert.Equal(4, (await ReadAsync(database, [.. graph.Steps.Select(step => step.Id)])).Count);
        Assert.Equal([translation, comments[1]], await ReadAsync(database, comments[1].StepId, Guid.CreateVersion7()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_trashed_project_is_purged_with_every_line_of_the_comments_of_its_steps_and_another_project_keeps_its_own(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var (purged, other) = (await PurgeBench.GraphAsync(database), await PurgeBench.GraphAsync(database));
        var mine = purged.Steps.SelectMany(step => new[]
        {
            Comment(step.Id, "À purger.", 1, purged.Owner, purged.At(30)), Comment(step.Id, "To purge.", 1, purged.Owner, purged.At(30), "en", CommentSource.Workbook),
        }).ToList();
        var kept = Comment(other.Steps[0].Id, "À garder.", 1, other.Owner, other.At(30));
        await InUnitAsync(database, (store, token) => store.AddAsync([.. mine, kept], token));
        var trashedAt = purged.At(100);
        await PurgeBench.TrashAsync(database, purged, trashedAt);
        var plan = await PurgeBench.PlanAsync(database, purged.Project.Id, PurgeBench.AfterRetention(trashedAt));

        await PurgeBench.PurgeAsync(database, purged.Project.Id, plan, 7);

        Assert.Null(await PurgeBench.LoadAsync(database, purged.Project.Id));
        Assert.Empty(await ReadAsync(database, [.. mine.Select(comment => comment.StepId)]));
        Assert.Equal([kept], await ReadAsync(database, kept.StepId));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_expired_batch_takes_the_lines_of_the_comment_of_its_step_and_leaves_the_comments_of_the_live_steps(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var scenario = await StoreScenario.CreateAsync(database);
        var steps = new Guid[2];
        await scenario.RunAsync((workspace, context) =>
        {
            var map = workspace.RootMap.Id;
            for (var i = 0; i < steps.Length; i++)
                steps[i] = StepOperations.Create(workspace, context, new CreateStepRequest(map, workspace.LanesOf(map)[0].Id, i, $"Step {i}")).Id;
        });
        var comments = steps.Select(step => Comment(step, "Dit en présentation.", 1, scenario.Actor, FixedClock.Time)).ToList();
        var translation = Comment(steps[0], "Said while presenting.", 1, scenario.Actor, FixedClock.Time, "en", CommentSource.Assistant);
        await InUnitAsync(database, (store, token) => store.AddAsync([.. comments, translation], token));
        await scenario.RunAsync((workspace, context) => GroupDeletion.Delete(workspace, context, workspace.RootMap.Id, new GroupDeletionRequest([steps[0]], null)));
        var plan = PurgePlanner.Expired(await scenario.LoadAsync(), PurgeBench.AfterRetention(FixedClock.Time.AddDays(1)), AppSettings.Default);
        Assert.Contains(plan.Records, record => record is Step step && step.Id == steps[0]);

        await PurgeBench.PurgeAsync(database, scenario.ProjectId, plan, 4);

        Assert.Equal([comments[1]], await ReadAsync(database, steps));
    }
}
