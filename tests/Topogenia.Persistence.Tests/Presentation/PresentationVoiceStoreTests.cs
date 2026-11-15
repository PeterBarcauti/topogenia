// PresentationVoiceStoreTests.cs
// The voices of the projects on both engines (ADR 103): a line per project and language, written in a unit with its version checked, read for its project, added by an import, purged before its project and left by the purge of a batch.

using Topogenia.Application.Presentation;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Flows;
using Topogenia.Domain.Model;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Trash;
using Topogenia.Persistence.Presentation;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Purge;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Presentation;

public sealed class PresentationVoiceStoreTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private const string Hortense = "Hortense - voix du poste";

    private static PresentationVoice Voice(ProjectGraph graph, string name, int version, int minute, string language = "fr", bool local = true) =>
        new(graph.Project.Id, language, name, local, version, graph.At(minute), graph.Owner.Id);

    /// <summary>The write of a voice over the version read of its line.</summary>
    private static VoiceWrite Put(PresentationVoice line, int read) => new(line.ProjectId, line.Language, line, read);

    /// <summary>The removal of the voice of a project for a language, over the version read of it.</summary>
    private static VoiceWrite Remove(Guid projectId, string language, int read) => new(projectId, language, null, read);

    /// <summary>One unit of work of the store, as a request runs it.</summary>
    private static async Task InUnitAsync(TestDatabase database, Func<PresentationVoiceStore, CancellationToken, Task> work)
    {
        await using var session = database.Session();
        var unit = new UnitOfWork(session);
        var store = new PresentationVoiceStore(session);
        await unit.RunAsync(async token =>
        {
            await work(store, token);
            return true;
        }, Token);
    }

    private static Task WriteAsync(TestDatabase database, VoiceWrite write) => InUnitAsync(database, (store, token) => store.WriteAsync(write, token));

    private static async Task<IReadOnlyList<PresentationVoice>> ReadAsync(TestDatabase database, Guid projectId)
    {
        await using var session = database.Session();
        return [.. (await new PresentationVoiceStore(session).OfProjectAsync(projectId, Token)).OrderBy(line => line.Language, StringComparer.Ordinal)];
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_voice_is_written_replaced_and_removed_with_its_version_checked(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = await PurgeBench.GraphAsync(database);
        var project = graph.Project.Id;
        // A name as long as the rule allows, with letters outside ASCII: it is read back as it was written.
        var first = Voice(graph, Hortense + " " + new string('é', PresentationVoiceRules.NameMax - Hortense.Length - 1), 1, 30);
        await WriteAsync(database, Put(first, 0));
        Assert.Equal([first], await ReadAsync(database, project));
        Assert.Equal(PresentationVoiceRules.NameMax, (await ReadAsync(database, project))[0].Name.Length);

        var second = first with { Name = "Julie", Local = false, Version = 2, UpdatedAt = graph.At(31) };
        await WriteAsync(database, Put(second, 1));
        Assert.Equal([second], await ReadAsync(database, project));

        // A version read before the second is refused, with the author and the time of the second.
        var refusal = await Assert.ThrowsAsync<DomainException>(() => WriteAsync(database, Put(first with { Version = 2 }, 1)));
        Assert.Equal((ErrorCodes.VersionConflict, "presentation_voice", (object?)graph.Owner.Id, (object?)graph.At(31)),
            (refusal.Code, refusal.Details["object"], refusal.Details["updated_by"], refusal.Details["updated_at"]));
        Assert.Equal([second], await ReadAsync(database, project));

        await WriteAsync(database, Remove(project, "fr", 2));
        Assert.Empty(await ReadAsync(database, project));
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
        var project = graph.Project.Id;
        var first = Voice(graph, Hortense, 1, 30);
        var second = first with { Name = "Julie", Version = 2, UpdatedAt = graph.At(31) };
        await WriteAsync(database, Put(first, 0));
        await WriteAsync(database, Put(second, 1));
        var stale = await Assert.ThrowsAsync<DomainException>(() => WriteAsync(database, Remove(project, "fr", 1)));
        Assert.Equal(ErrorCodes.VersionConflict, stale.Code);
        Assert.Equal([second], await ReadAsync(database, project));
        await WriteAsync(database, Remove(project, "fr", 2));
        var gone = await Assert.ThrowsAsync<DomainException>(() => WriteAsync(database, Put(second with { Version = 3 }, 2)));
        Assert.Equal((ErrorCodes.VersionConflict, (object?)null), (gone.Code, gone.Details["updated_by"]));
        Assert.Empty(await ReadAsync(database, project));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Each_language_of_a_project_keeps_its_own_voice_and_its_own_version_read_for_its_project_only(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var (graph, other) = (await PurgeBench.GraphAsync(database), await PurgeBench.GraphAsync(database));
        var french = Voice(graph, Hortense, 1, 30);
        var english = Voice(graph, "Julie", 1, 31, "en", local: false);
        var elsewhere = Voice(other, "Julie", 1, 32);
        foreach (var voice in new[] { french, english, elsewhere })
            await WriteAsync(database, Put(voice, 0));
        Assert.Equal([english, french], await ReadAsync(database, graph.Project.Id));
        Assert.Equal([elsewhere], await ReadAsync(database, other.Project.Id));

        // The French voice changed and the English one removed: the other project keeps its own, with its version.
        var changed = french with { Name = "Julie", Version = 2, UpdatedAt = graph.At(33), UpdatedBy = graph.Reader.Id };
        await WriteAsync(database, Put(changed, 1));
        await WriteAsync(database, Remove(graph.Project.Id, "en", 1));
        Assert.Equal([changed], await ReadAsync(database, graph.Project.Id));
        Assert.Equal([elsewhere], await ReadAsync(database, other.Project.Id));
        Assert.Empty(await ReadAsync(database, Guid.CreateVersion7()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_voice_is_written_only_in_a_unit_of_work_and_on_its_own_project_and_language(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var (graph, other) = (await PurgeBench.GraphAsync(database), await PurgeBench.GraphAsync(database));
        await using var session = database.Session();
        var store = new PresentationVoiceStore(session);
        var voice = Voice(graph, "Hors unité", 1, 30);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.WriteAsync(Put(voice, 0), Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.AddAsync([voice], Token));
        await Assert.ThrowsAsync<ArgumentException>(() => WriteAsync(database, new VoiceWrite(other.Project.Id, "fr", voice, 0)));
        await Assert.ThrowsAsync<ArgumentException>(() => WriteAsync(database, new VoiceWrite(voice.ProjectId, "en", voice, 0)));
        Assert.Empty(await ReadAsync(database, graph.Project.Id));
        Assert.Empty(await ReadAsync(database, other.Project.Id));
        // Nothing to add is nothing done, even outside a unit.
        await store.AddAsync([], Token);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_voices_of_an_import_are_added_together_with_the_version_they_bring(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = await PurgeBench.GraphAsync(database);
        var voices = new[] { Voice(graph, Hortense, 1, 30), Voice(graph, "Julie", 1, 30, "en", local: false), Voice(graph, "Voix allemande", 1, 30, "de") };
        await InUnitAsync(database, (store, token) => store.AddAsync(voices, token));
        Assert.Equal(voices.OrderBy(voice => voice.Language, StringComparer.Ordinal), await ReadAsync(database, graph.Project.Id));
        // Added, each line is written again over the version it was added with.
        await WriteAsync(database, Put(voices[0] with { Name = "Julie", Version = 2 }, 1));
        Assert.Equal(2, (await ReadAsync(database, graph.Project.Id)).Single(voice => voice.Language == "fr").Version);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_trashed_project_is_purged_with_its_voices_and_another_project_keeps_its_own(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var (purged, other) = (await PurgeBench.GraphAsync(database), await PurgeBench.GraphAsync(database));
        var mine = new[] { Voice(purged, Hortense, 1, 30), Voice(purged, "Julie", 2, 31, "en", local: false) };
        var kept = Voice(other, Hortense, 1, 30);
        await InUnitAsync(database, (store, token) => store.AddAsync([.. mine, kept], token));
        var trashedAt = purged.At(100);
        await PurgeBench.TrashAsync(database, purged, trashedAt);
        // In the trash, the project keeps its voices for its restoration.
        Assert.Equal(2, (await ReadAsync(database, purged.Project.Id)).Count);
        var plan = await PurgeBench.PlanAsync(database, purged.Project.Id, PurgeBench.AfterRetention(trashedAt));

        // Whatever the number of records by transaction: the voices go in the one that removes the project.
        await PurgeBench.PurgeAsync(database, purged.Project.Id, plan, 7);

        Assert.Null(await PurgeBench.LoadAsync(database, purged.Project.Id));
        Assert.Empty(await ReadAsync(database, purged.Project.Id));
        Assert.Equal([kept], await ReadAsync(database, other.Project.Id));
    }

    /// <summary>The purge of a batch of the trash removes what the batch deleted and leaves the project: its voices stay.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_purge_of_a_batch_of_the_trash_leaves_the_voices_of_its_project(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var scenario = await StoreScenario.CreateAsync(database);
        var flow = Guid.Empty;
        await scenario.RunAsync((workspace, context) =>
        {
            var map = workspace.RootMap.Id;
            var steps = Enumerable.Range(0, 2).Select(i => StepOperations.Create(workspace, context, new CreateStepRequest(map, workspace.LanesOf(map)[0].Id, i, $"Step {i}")).Id)
                .ToList();
            flow = FlowRules.Create(workspace, context, new CreateFlowRequest(map, steps[0], steps[1], null)).Id;
        });
        var voice = new PresentationVoice(scenario.ProjectId, "fr", Hortense, true, 1, FixedClock.Time, scenario.Actor.Id);
        await InUnitAsync(database, (store, token) => store.AddAsync([voice], token));
        await scenario.RunAsync((workspace, context) => FlowRules.Delete(workspace, context, flow));
        var plan = PurgePlanner.Expired(await scenario.LoadAsync(), PurgeBench.AfterRetention(FixedClock.Time.AddDays(1)), AppSettings.Default);
        Assert.Equal([flow], plan.Records.OfType<Flow>().Select(deleted => deleted.Id));

        await PurgeBench.PurgeAsync(database, scenario.ProjectId, plan, 4);

        Assert.Null((await scenario.LoadAsync()).Flow(flow));
        Assert.Equal([voice], await ReadAsync(database, scenario.ProjectId));
    }
}
