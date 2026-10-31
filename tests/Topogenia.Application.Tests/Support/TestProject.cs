// TestProject.cs
// A small fictional project for the handler tests: PRJ, its root map, one lane, users of every role, and the runner.

using Topogenia.Application.Commands;
using Topogenia.Application.Maps;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Support;

/// <summary>
/// One project in an in-memory database, with a facilitator, a contributor, a reader, an administrator who is not a
/// member and a stranger; identifiers are predictable, the clock is fixed. One project per database: the fake store loads
/// every record of the database.
/// </summary>
public sealed class TestProject
{
    public static readonly DateTime Now = new(2026, 10, 24, 8, 0, 0, DateTimeKind.Utc);

    private int nextId = 0x100;

    public TestProject()
    {
        ProjectId = NextId();
        RootMapId = NextId();
        LaneId = NextId();
        Facilitator = User("FICTIF\\animateur", "Anne Animatrice");
        Contributor = User("FICTIF\\contributeur", "Carl Contributeur");
        Reader = User("FICTIF\\lecteur", "Lea Lectrice");
        Admin = User("FICTIF\\admin", "Alex Admin", isAdmin: true);
        Stranger = User("FICTIF\\inconnu", "Ines Inconnue");
        Database.Add(new Project(ProjectId, "PRJ", "Achats fournisseurs", "fr", null, null, Classification.Internal, null, null, 0,
            Now, Facilitator.Id, null, null, null, null, 1));
        foreach (var counter in ProjectCreation.InitialCounters)
            Database.Add(new ProjectCounter(ProjectId, counter, counter == CounterKind.Lane ? 1 : 0));
        Database.Add(new ProcessMap(RootMapId, ProjectId, null, "Achats fournisseurs", "fr", Now, Facilitator.Id, null, null, null, null, 1));
        Database.Add(new Lane(LaneId, RootMapId, "PRJ-C01", "Acteur 1", "fr", LaneType.Person, null, "#E3F2FD", 0, null,
            Now, Facilitator.Id, null, null, null, null, 1));
        Member(Facilitator, MemberRole.Facilitator);
        Member(Contributor, MemberRole.Contributor);
        Member(Reader, MemberRole.Reader);
        Unit = new InMemoryUnitOfWork(Database);
        Runner = new CommandRunner(Unit, new InMemoryAccessReader(Database), new InMemoryWorkspaceStore(Database),
            new InMemoryOperationLog(Database), new MapViewBuilder(AppSettings.Default), CurrentUser, Clock, AppSettings.Default, Versions);
        Queries = new QueryRunner(new InMemoryAccessReader(Database), new InMemoryWorkspaceStore(Database), new InMemoryOperationLog(Database),
            new MapViewBuilder(AppSettings.Default), CurrentUser);
        CurrentUser.User = Facilitator;
    }

    public InMemoryDatabase Database { get; } = new();

    public InMemoryUnitOfWork Unit { get; }

    public FakeCurrentUser CurrentUser { get; } = new();

    public FakeVersionRequirement Versions { get; } = new();

    public FixedClock Clock { get; } = new(Now);

    public CommandRunner Runner { get; }

    public QueryRunner Queries { get; }

    public Guid ProjectId { get; }

    public Guid RootMapId { get; }

    public Guid LaneId { get; }

    public CurrentUser Facilitator { get; }

    public CurrentUser Contributor { get; }

    public CurrentUser Reader { get; }

    public CurrentUser Admin { get; }

    public CurrentUser Stranger { get; }

    public Project Project => Database.Get<Project>(Domain.Model.Project.KeyOf(ProjectId));

    public Guid NextId() => new($"00000000-0000-0000-0000-{nextId++:x12}");

    public TestProject ActingAs(CurrentUser user)
    {
        CurrentUser.User = user;
        return this;
    }

    /// <summary>Adds a live step in a cell of the first lane and returns its identifier.</summary>
    public Guid Step(int column, Guid? assignee = null, Guid? laneId = null, Guid? mapId = null)
    {
        var id = NextId();
        Database.Add(new Step(id, mapId ?? RootMapId, laneId ?? LaneId, $"PRJ-E{column + 1:D3}", $"Étape {column}", "fr", StepType.Task,
            column, 0, assignee, AutomationStatus.Manual, null, Now, Facilitator.Id, null, null, null, null, 1));
        return id;
    }

    /// <summary>Adds a second root-level map of the same project (a sub-map of a step) and returns it with its lane.</summary>
    public (Guid MapId, Guid LaneId) SubMap(Guid parentStepId, string language = "fr")
    {
        var mapId = NextId();
        var laneId = NextId();
        Database.Add(new ProcessMap(mapId, ProjectId, parentStepId, "Détail", language, Now, Facilitator.Id, null, null, null, null, 1));
        Database.Add(new Lane(laneId, mapId, "PRJ-C99", "Acteur 1", "fr", LaneType.Person, null, "#E3F2FD", 0, null,
            Now, Facilitator.Id, null, null, null, null, 1));
        return (mapId, laneId);
    }

    public void PutInTrash() => Database.Add(Project with { DeletedAt = Now.AddDays(-1) });

    private CurrentUser User(string login, string name, bool isAdmin = false)
    {
        var user = new CurrentUser(NextId(), login, name, "fr", isAdmin);
        Database.Add(new AppUser(user.Id, login, login.ToUpperInvariant(), name, null, "fr", isAdmin, true, null, Now, null, null, 1));
        return user;
    }

    private void Member(CurrentUser user, MemberRole role) =>
        Database.Add(new ProjectMember(ProjectId, user.Id, role, false, false, 0, Now, Facilitator.Id, null, null));
}
