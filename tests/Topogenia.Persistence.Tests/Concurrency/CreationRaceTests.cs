// CreationRaceTests.cs
// Users, translations and uses created or updated by their key at the same time: one row, every unit succeeds (ADR 033, A-5-14).

using System.Collections.Concurrent;
using Topogenia.Application.Ports;
using Topogenia.Domain.Documents;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Errors;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Concurrency;

/// <summary>
/// The 1.x wrote a user, a translation and a use with `MERGE ... WITH (HOLDLOCK)`; the 2.0 writes them with the change set
/// of the domain. Two units that create the same one at once (they met after their loading, before writing) both succeed:
/// the unit that loses the race on the natural key runs again, finds the record and updates it (ADR 033). SQLite never
/// lets two write transactions meet (they are immediate): its units run one after the other and nothing is retried. Under
/// the lock of the project (translations, uses, spec 5.6-11) the units never meet on either engine.
/// </summary>
public sealed class CreationRaceTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<StoreScenario> ScenarioAsync(EngineKind kind) => await StoreScenario.CreateAsync(await databases.OfAsync(kind));

    /// <summary>The retries of the units of a test, by the kind of failure and the guard named.</summary>
    private sealed class Retries
    {
        public ConcurrentQueue<StoreError> Seen { get; } = new();

        public RetryPolicy Policy => RetryPolicy.Standard(retrying: attempt => Seen.Enqueue(attempt.Error));
    }

    /// <summary>
    /// On SQL Server the units always meet (read committed lets both read before either writes) and the loser runs again
    /// once on the natural key; on SQLite, when they did not meet, nothing was retried.
    /// </summary>
    private static void AssertRace(EngineKind kind, Rendezvous meeting, Retries retries, string key)
    {
        Assert.True(kind == EngineKind.Sqlite || meeting.AllMet, meeting.Missed);
        if (meeting.AllMet)
            Assert.Equal(new StoreError(StoreErrorKind.CreationRace, key), Assert.Single(retries.Seen));
        else
            Assert.Empty(retries.Seen);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Two_projects_that_invite_one_new_login_at_the_same_time_create_one_user_that_both_name(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var guest = $@"FICTITIOUS\Guest_{Guid.CreateVersion7():N}";
        var creators = new[] { scenario.Actor, scenario.Other };
        var (meeting, retries) = (new Rendezvous(kind, 2), new Retries());

        var attempts = await Concurrently.RunAsync(2, async index =>
        {
            await using var session = scenario.Database.Session(retries.Policy);
            var unit = new UnitOfWork(session);
            var store = new WorkspaceStore(session);
            var attempt = 0;
            return await unit.RunAsync(async token =>
            {
                // The handler resolves the members from the users of the database, then creates the project and the guest.
                var known = await scenario.Database.UsersAsync(Login.Normalize(guest));
                await meeting.ArriveAsync(++attempt);
                var created = ProjectCreation.Create(scenario.Context(creators[index]),
                    new CreateProjectRequest(ScenarioSteps.NewProjectCode(), $"Invites {index}", Members: [new MemberEntry(guest, "reader", false)]),
                    new ProjectCreationInputs([], StoreScenario.Languages, "fr", new PeopleLookup([creators[index], .. known], new Dictionary<string, DirectoryEntry>(), [])));
                await store.SaveAsync(new ChangeSet([.. created.NewUsers.Select(StoreScenario.Created), .. created.Records.Select(StoreScenario.Created)]), token);
                return created.Project.Id;
            }, Token);
        });

        Assert.All(attempts, attempt => Assert.True(attempt.Succeeded));
        var user = Assert.Single(await scenario.Database.UsersAsync(Login.Normalize(guest)));
        foreach (var attempt in attempts)
        {
            await using var session = scenario.Database.Session();
            var project = (await new WorkspaceStore(session).LoadAsync(attempt.Result, WorkspaceContent.Members, Token))!;
            Assert.Contains(project.All<ProjectMember>(), member => member.UserId == user.Id && member.Role == MemberRole.Reader);
        }
        AssertRace(kind, meeting, retries, "UQ_app_user_login_normalized");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Two_translations_of_one_text_written_at_the_same_time_without_the_lock_leave_one_row(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var lane = (await scenario.LoadAsync()).FirstLane();
        var (meeting, retries) = (new Rendezvous(kind, 2), new Retries());

        var attempts = await Concurrently.RunAsync(2, index => scenario.RunAsync(
            (workspace, context) => TranslationRules.WriteManual(workspace, context, TranslationObject.Lane, lane, [new ManualTranslation("en", $"Actor {index}")]),
            new RunOptions { Lock = false, Loaded = meeting.ArriveAsync, Retry = retries.Policy }));

        Assert.All(attempts, attempt => Assert.True(attempt.Succeeded));
        var translation = Assert.Single((await scenario.LoadAsync()).All<Translation>(), row => row.ObjectId == lane);
        Assert.Equal((TranslationSource.Manual, 2), (translation.Source, translation.Version));
        Assert.StartsWith("Actor ", translation.Text, StringComparison.Ordinal);
        AssertRace(kind, meeting, retries, "PK_translation");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Translations_of_one_text_written_at_the_same_time_under_the_lock_run_in_series(EngineKind kind)
    {
        const int operations = 4;
        var scenario = await ScenarioAsync(kind);
        var lane = (await scenario.LoadAsync()).FirstLane();
        var retries = new Retries();

        var attempts = await Concurrently.RunAsync(operations, index => scenario.RunAsync(
            (workspace, context) => TranslationRules.WriteManual(workspace, context, TranslationObject.Lane, lane, [new ManualTranslation("en", $"Actor {index}")]),
            new RunOptions { Retry = retries.Policy }));

        Assert.All(attempts, attempt => Assert.True(attempt.Succeeded));
        Assert.Equal(operations, Assert.Single((await scenario.LoadAsync()).All<Translation>(), row => row.ObjectId == lane).Version);
        Assert.Empty(retries.Seen);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Two_ticks_of_one_document_by_one_step_at_the_same_time_without_the_lock_leave_one_use(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var steps = await scenario.LinkedStepsAsync(2);
        var document = await scenario.OutputOfAsync(steps[0]);
        var (meeting, retries) = (new Rendezvous(kind, 2), new Retries());

        var attempts = await Concurrently.RunAsync(2, index => scenario.RunAsync(
            (workspace, context) => DocumentUses.Use(workspace, context, steps[1], document, new DocumentUseInput(index + 1, 50 + index)),
            new RunOptions { Lock = false, Loaded = meeting.ArriveAsync, Retry = retries.Policy }));

        Assert.All(attempts, attempt => Assert.True(attempt.Succeeded));
        var stored = await scenario.LoadAsync();
        var use = Assert.Single(stored.All<DocumentUse>());
        // The loser, run again, updates the row of the winner: one version past its creation, at the structure version (AR-111).
        Assert.Equal((steps[1], Math.Max(1, stored.Project.StructureVersion) + 1), (use.StepId, use.Version));
        AssertRace(kind, meeting, retries, "PK_document_use");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task One_translation_file_imported_twice_at_the_same_time_under_the_lock_is_written_once(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var lane = (await scenario.LoadAsync()).FirstLane();
        var rows = new[] { new TranslationRow(2, "lane", lane, "Acteur", new Dictionary<string, string?> { ["en"] = "Actor" }) };

        var attempts = await Concurrently.RunAsync(2, _ => scenario.RunAsync((workspace, context) =>
            TranslationImport.Import(workspace, context, TranslationScope.Missing, rows, StoreScenario.Languages)));

        Assert.All(attempts, attempt => Assert.True(attempt.Succeeded));
        // The second import, run after the first under the lock, finds the translation and changes nothing (spec 5.6-11).
        Assert.Single(attempts, attempt => attempt.Result!.Changes.IsEmpty);
        var translation = Assert.Single((await scenario.LoadAsync()).All<Translation>(), row => row.ObjectId == lane);
        Assert.Equal(("Actor", TranslationSource.Auto, 1), (translation.Text, translation.Source, translation.Version));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_translation_file_whose_unit_fails_at_its_commit_leaves_no_translation(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var steps = await scenario.LinkedStepsAsync(3);
        var rows = steps.Select((step, i) => new TranslationRow(i + 2, "step", step, $"Step {i}", new Dictionary<string, string?> { ["en"] = $"Step {i} (en)" })).ToArray();
        await using var session = scenario.Database.Session(null, new ScriptedFailures(commits: 1));

        await Assert.ThrowsAsync<SimulatedEngineFailure>(() => scenario.RunAsync((workspace, context) =>
            Assert.Equal(3, TranslationImport.Import(workspace, context, TranslationScope.Missing, rows, StoreScenario.Languages).Written),
            new RunOptions { Session = session }));

        Assert.Empty((await scenario.LoadAsync()).All<Translation>());
    }
}
