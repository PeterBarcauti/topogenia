// StoreScenario.cs
// A project created by the domain and changed by its operations through the unit of work and the workspace store, as a handler will.

using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>What an operation left: the workspace in memory after its commit and the change set written.</summary>
public sealed record Outcome(ProjectWorkspace Workspace, ChangeSet Changes);

/// <summary>
/// How <see cref="StoreScenario"/> runs an operation: with the lock of the project (the gabarit of a structure command, spec
/// 4.7) or without it, as a given user, in a given session or with a given retry policy, and a step called after each loading
/// with the number of the attempt.
/// </summary>
public sealed record RunOptions
{
    public bool Lock { get; init; } = true;

    public AppUser? User { get; init; }

    public StoreSession? Session { get; init; }

    public RetryPolicy? Retry { get; init; }

    public Func<int, Task>? Loaded { get; init; }
}

/// <summary>
/// The gabarit of a structure command (spec 3.4): a unit of work, the lock of the project, the loading of its workspace,
/// a rule of the domain, the commit of the workspace and the writing of its change set. Each scenario has its own users
/// and project code, so that many scenarios share one database.
/// </summary>
public sealed class StoreScenario
{
    public static readonly LanguageSettings Languages =
        new(["fr", "en"], "fr", new Dictionary<string, string> { ["fr"] = "Acteur", ["en"] = "Actor" });

    private static int counter;
    private int ticks;

    private StoreScenario(TestDatabase database, AppUser actor, AppUser other)
    {
        Database = database;
        Actor = actor;
        Other = other;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public TestDatabase Database { get; }

    public AppUser Actor { get; }

    /// <summary>A second user, the author of the concurrent changes.</summary>
    public AppUser Other { get; }

    public Guid ProjectId { get; private set; }

    /// <summary>A scenario with its two users written, and its project created by the domain and written.</summary>
    public static async Task<StoreScenario> CreateAsync(TestDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        var number = Interlocked.Increment(ref counter);
        var scenario = new StoreScenario(database, User("Actor", number), User("Other", number));
        await scenario.WriteAsync(new ChangeSet([Created(scenario.Actor), Created(scenario.Other)]));
        var created = ProjectCreation.Create(scenario.Context(), new CreateProjectRequest(CodeOf(number), $"Fictitious process {number}"),
            new ProjectCreationInputs([], Languages, "fr", new PeopleLookup([scenario.Actor], new Dictionary<string, DirectoryEntry>(), [])));
        scenario.ProjectId = created.Project.Id;
        await scenario.WriteAsync(new ChangeSet(created.Records.Select(Created).ToList()));
        return scenario;
    }

    public static RecordChange Created(IRecord record) => new(ChangeKind.Created, null, record);

    /// <summary>The context of the next operation of the actor (or of <paramref name="user"/>): one second later each time, in UTC.</summary>
    public OperationContext Context(AppUser? user = null) =>
        new((user ?? Actor).Id, FixedClock.Time.AddSeconds(Interlocked.Increment(ref ticks)), "fr", AppSettings.Default);

    /// <summary>Runs an operation of the domain as a structure command does, in its own session and unit of work.</summary>
    public Task<Outcome> RunAsync(Action<ProjectWorkspace, OperationContext> operation, AppUser? user = null, StoreSession? session = null) =>
        RunAsync(operation, new RunOptions { User = user, Session = session });

    /// <summary>
    /// Runs an operation in a unit of work as <see cref="RunAsync(Action{ProjectWorkspace, OperationContext}, AppUser?, StoreSession?)"/>
    /// does, with the lock of the project or without it, and a step between the loading and the operation (to make
    /// concurrent units meet). Each attempt of the unit loads the workspace again: a retried unit sees what was committed since.
    /// </summary>
    public async Task<Outcome> RunAsync(Action<ProjectWorkspace, OperationContext> operation, RunOptions options)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(options);
        var session = options.Session ?? Database.Session(options.Retry);
        var attempt = 0;
        try
        {
            var unit = new UnitOfWork(session);
            var store = new WorkspaceStore(session);
            return await unit.RunAsync(async token =>
            {
                if (options.Lock)
                    await unit.LockProjectAsync(ProjectId, token);
                var workspace = (await store.LoadAsync(ProjectId, WorkspaceContent.All, token))!;
                if (options.Loaded is { } loaded)
                    await loaded(++attempt);
                var context = Context(options.User);
                operation(workspace, context);
                var changes = workspace.Commit(context);
                await store.SaveAsync(changes, token);
                return new Outcome(workspace, changes);
            }, Token);
        }
        finally
        {
            if (options.Session is null)
                await session.DisposeAsync();
        }
    }

    /// <summary>
    /// The change set of an operation computed on the workspace as it is now, without writing it: written later, it
    /// carries the state it read, as an operation whose loading happened before a concurrent commit.
    /// </summary>
    public async Task<Outcome> PrepareAsync(Action<ProjectWorkspace, OperationContext> operation, AppUser? user = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var workspace = await LoadAsync();
        var context = Context(user);
        operation(workspace, context);
        return new Outcome(workspace, workspace.Commit(context));
    }

    /// <summary>A user written in its own unit, with a login unique to this scenario.</summary>
    public async Task<AppUser> NewUserAsync(string name)
    {
        var user = User(name, Interlocked.Increment(ref counter));
        await WriteAsync(new ChangeSet([Created(user)]));
        return user;
    }

    /// <summary>The members of the project become the actor (facilitator) and these users (contributors).</summary>
    public Task<Outcome> SetMembersAsync(params AppUser[] contributors) =>
        RunAsync((workspace, context) => MemberRules.Replace(workspace, context,
            [new MemberEntry(Actor.Login, "facilitator", false), .. contributors.Select(user => new MemberEntry(user.Login, "contributor", false))],
            new PeopleLookup([Actor, .. contributors], new Dictionary<string, DirectoryEntry>(), [])));

    /// <summary>Writes a change set in a unit of work of its own, without the lock of the project.</summary>
    public Task WriteAsync(ChangeSet changes) => WriteAsync(changes, false);

    /// <summary>
    /// Writes a change set in a unit of work of its own, under the lock of the project or without it. A change set that
    /// modifies or removes a record without version needs the lock (ADR 030): without it, the write is refused.
    /// </summary>
    public async Task WriteAsync(ChangeSet changes, bool locked)
    {
        await using var session = Database.Session();
        var unit = new UnitOfWork(session);
        var store = new WorkspaceStore(session);
        await unit.RunAsync(async token =>
        {
            if (locked)
                await unit.LockProjectAsync(ProjectId, token);
            await store.SaveAsync(changes, token);
            return true;
        }, Token);
    }

    /// <summary>The workspace of the project read again from the database, every group.</summary>
    public async Task<ProjectWorkspace> LoadAsync(WorkspaceContent content = WorkspaceContent.All)
    {
        await using var session = Database.Session();
        return (await new WorkspaceStore(session).LoadAsync(ProjectId, content, Token))!;
    }

    /// <summary>
    /// The database holds what the operation left in memory, and every record it touched is in its expected state (content,
    /// absent, created: the check of the undo, `ProjectWorkspace.FirstOutdated`); no step is left parked.
    /// </summary>
    public async Task<ProjectWorkspace> AssertStoredAsync(Outcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        var stored = await LoadAsync();
        WorkspaceRecords.AssertSame(outcome.Workspace, stored);
        Assert.Null(stored.FirstOutdated(outcome.Changes.Touched));
        Assert.All(stored.All<Step>(), step => Assert.InRange(step.Column, 0, Cell.MaxColumn));
        return stored;
    }

    private static AppUser User(string name, int number)
    {
        var login = $@"FICTITIOUS\{name}{number}_{Guid.CreateVersion7():N}";
        return new AppUser(Guid.CreateVersion7(), login, Login.Normalize(login), $"{name} {number}", null, "fr", false, true, null,
            FixedClock.Time, null, null, 1);
    }

    /// <summary>Six capital letters, unique per scenario and per process (the codes of <see cref="ProjectGraph"/> start lower).</summary>
    private static string CodeOf(int number)
    {
        var letters = new char[6];
        var value = 200_000_000 + number + (Environment.ProcessId % 1000 * 100_000);
        for (var i = 5; i >= 0; i--)
        {
            letters[i] = (char)('A' + (value % 26));
            value /= 26;
        }
        return new string(letters);
    }
}
