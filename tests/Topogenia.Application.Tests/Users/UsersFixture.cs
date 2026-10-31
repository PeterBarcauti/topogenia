// UsersFixture.cs
// In-memory fakes of the ports of the users, the cross-project reads, the directory, the administrators and the purge, and the runners on them.

using Topogenia.Application.Commands;
using Topogenia.Application.Maps;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Trash;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Tests.Users;

/// <summary>
/// The workspace store of the persistence as the members need it: the users named by the members are loaded with them
/// (`ProjectScope.MemberUsers`), and a user created on a login that another user already holds is refused as the unique
/// key `UQ_app_user_login_normalized` refuses it, a failure that makes the unit run again (ADR 033, a transient failure
/// of the fake unit of work).
/// </summary>
public sealed class MemberUsersStore(InMemoryDatabase database) : IWorkspaceStore
{
    public Task<ProjectWorkspace?> LoadAsync(Guid projectId, WorkspaceContent content, CancellationToken cancellationToken)
    {
        if (database.Load(projectId, content) is null)
            return Task.FromResult<ProjectWorkspace?>(null);
        var members = database.All<ProjectMember>().Where(member => member.ProjectId == projectId).Select(member => member.UserId).ToHashSet();
        var users = content.HasFlag(WorkspaceContent.Members) ? database.All<AppUser>().Where(user => members.Contains(user.Id)) : [];
        return Task.FromResult<ProjectWorkspace?>(new ProjectWorkspace(projectId,
            database.All<IRecord>().Where(record => record is not AppUser).Concat(users)));
    }

    public Task SaveAsync(ChangeSet changes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        foreach (var user in changes.Created<AppUser>())
        {
            if (database.All<AppUser>().Any(other => other.LoginNormalized == user.LoginNormalized && other.Id != user.Id))
                throw new TimeoutException("A concurrent unit created this login first.");
        }
        database.Save(changes);
        return Task.CompletedTask;
    }
}

/// <summary>The users of the in-memory database; <see cref="StaleReads"/> reads of logins see nobody, as a read made before a concurrent creation.</summary>
public sealed class InMemoryUserReader(InMemoryDatabase database) : IUserReader
{
    public int StaleReads { get; set; }

    public Task<AppUser?> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(database.All<AppUser>().SingleOrDefault(user => user.Id == userId));

    public Task<AppUser?> FindByLoginAsync(string login, CancellationToken cancellationToken) =>
        Task.FromResult(database.All<AppUser>().SingleOrDefault(user => user.LoginNormalized == Login.Normalize(login)));

    public Task<IReadOnlyList<AppUser>> FindByLoginsAsync(IReadOnlyCollection<string> logins, CancellationToken cancellationToken)
    {
        if (StaleReads > 0)
        {
            StaleReads--;
            return Task.FromResult<IReadOnlyList<AppUser>>([]);
        }
        var normalized = logins.Select(Login.Normalize).ToHashSet();
        return Task.FromResult<IReadOnlyList<AppUser>>(database.All<AppUser>().Where(user => normalized.Contains(user.LoginNormalized)).ToList());
    }

    public Task<IReadOnlyList<AppUser>> FindManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AppUser>>(database.All<AppUser>().Where(user => userIds.Contains(user.Id)).ToList());

    public Task<UserProfile?> ProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = database.All<AppUser>().SingleOrDefault(row => row.Id == userId);
        var live = database.All<Project>().Where(project => !project.IsInTrash).Select(project => project.Id).ToHashSet();
        var roles = database.All<ProjectMember>().Where(member => member.UserId == userId && live.Contains(member.ProjectId))
            .Select(member => new ProjectRole(member.ProjectId, member.Role, member.IsSponsor)).ToList();
        return Task.FromResult(user is null ? null : new UserProfile(user, roles));
    }
}

/// <summary>The reads across the projects of the in-memory database, with the counts of the 1.x.</summary>
public sealed class InMemoryProjectReader(InMemoryDatabase database) : IProjectReader
{
    public Task<IReadOnlyList<ProjectListRow>> LiveProjectsAsync(ProjectVisibility visibility, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProjectListRow>>(Visible(visibility, null).Where(project => !project.IsInTrash)
            .Select(project => new ProjectListRow(project, Names(project.Id),
                database.All<ProcessMap>().Single(map => map.ProjectId == project.Id && map.IsRoot).Id,
                database.All<ProjectMember>().SingleOrDefault(member => member.ProjectId == project.Id && member.UserId == visibility.UserId)?.Role,
                LiveSteps(project.Id), LiveMaps(project.Id).Count()))
            .ToList());

    public Task<IReadOnlyList<TrashRow>> TrashAsync(ProjectVisibility visibility, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TrashRow>>(Visible(visibility, MemberRole.Facilitator).Where(project => project.IsInTrash)
            .Select(project => new TrashRow(project, database.All<AppUser>().SingleOrDefault(user => user.Id == project.DeletedBy)?.DisplayName,
                LiveSteps(project.Id)))
            .ToList());

    public Task<IReadOnlyList<ExistingProjectCode>> CodesAsync(string code, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ExistingProjectCode>>(database.All<Project>().Where(project => project.Code == code)
            .Select(project => new ExistingProjectCode(project.Code, project.IsInTrash)).ToList());

    public Task<IReadOnlyList<string>> CodesStartingWithAsync(string prefix, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(database.All<Project>().Where(project => project.Code.StartsWith(prefix, StringComparison.Ordinal))
            .Select(project => project.Code).ToList());

    private IEnumerable<Project> Visible(ProjectVisibility visibility, MemberRole? role) =>
        database.All<Project>().Where(project => visibility.Everything || database.All<ProjectMember>()
            .Any(member => member.ProjectId == project.Id && member.UserId == visibility.UserId && (role is null || member.Role == role)));

    private IEnumerable<ProcessMap> LiveMaps(Guid projectId) =>
        database.All<ProcessMap>().Where(map => map.ProjectId == projectId && map.DeletedAt is null);

    private int LiveSteps(Guid projectId)
    {
        var maps = LiveMaps(projectId).Select(map => map.Id).ToHashSet();
        return database.All<Step>().Count(step => step.DeletedAt is null && maps.Contains(step.MapId));
    }

    private Dictionary<string, string> Names(Guid projectId) =>
        database.All<Translation>().Where(translation => translation.ObjectType == TranslationObject.Project && translation.ObjectId == projectId)
            .ToDictionary(translation => translation.Language, translation => translation.Text);
}

public sealed class FakeDirectory : IUserDirectory
{
    public Dictionary<string, DirectoryEntry> Entries { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Asked { get; } = [];

    public Task<DirectoryEntry?> FindAsync(string login, CancellationToken cancellationToken)
    {
        Asked.Add(login);
        return Task.FromResult(Entries.GetValueOrDefault(login));
    }
}

public sealed class FakeAdministrators : IAdministrators
{
    public List<string> List { get; } = [];

    public IReadOnlyCollection<string> Logins => List;
}

/// <summary>
/// The purge of the persistence on one or several in-memory databases: the candidates before the limit, and the records
/// of a plan removed in a unit of their own; the projects of <see cref="Failing"/> fail as a broken database would.
/// </summary>
public sealed class FakeTrashPurge(params InMemoryDatabase[] databases) : ITrashPurge
{
    public List<(Guid ProjectId, PurgePlan Plan)> Purged { get; } = [];

    public HashSet<Guid> Failing { get; } = [];

    public Task<IReadOnlyList<Guid>> ProjectsToPurgeAsync(DateTime limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(databases.SelectMany(database =>
                database.All<Project>().Where(project => project.DeletedAt < limit).Select(project => project.Id)
                    .Concat(database.All<DeletionBatch>().Where(batch => batch.DeletedAt < limit).Select(batch => batch.ProjectId)))
            .Distinct().Order().ToList());

    public Task<PurgeOutcome> PurgeAsync(Guid projectId, PurgePlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (Failing.Contains(projectId))
            throw new InvalidOperationException("The database of this project is broken.");
        var database = databases.Single(candidate => candidate.All<Project>().Any(project => project.Id == projectId));
        database.Begin();
        database.Save(new ChangeSet(plan.Records.Select(record => new RecordChange(ChangeKind.Removed, record, null)).ToList()));
        database.Commit();
        Purged.Add((projectId, plan));
        return Task.FromResult(new PurgeOutcome(plan.Records.Count, 0, 1));
    }
}

/// <summary>
/// A <see cref="TestProject"/> with the fakes of the ports of the users and the three runners on them: the command and
/// query templates load the users of the members, as the persistence does; <see cref="Runner"/> is the template of the
/// requests that name no existing project. The administrator of the project is the one of the configuration.
/// </summary>
public sealed class UsersFixture
{
    public static readonly LanguageSettings Languages =
        new(["fr", "en"], "en", new Dictionary<string, string> { ["fr"] = "Acteur 1", ["en"] = "Actor 1" });

    public UsersFixture()
    {
        Administrators.List.Add(Project.Admin.Login);
        Store = new MemberUsersStore(Database);
        Users = new InMemoryUserReader(Database);
        Projects = new InMemoryProjectReader(Database);
        Purge = new FakeTrashPurge(Database);
        Assistant = new AssistantDoubles(Database);
        Ports = new UserPorts(Users, Projects, Store, Directory, Administrators, Purge, new InMemoryAuditStore(Database), Assistant.Ports);
        Runner = new UserRunner(Project.Unit, new InMemoryAccessReader(Database), Ports, Project.CurrentUser, Project.Clock, AppSettings.Default, Languages, Project.Versions,
            SignedIn);
        Commands = new CommandRunner(Project.Unit, new InMemoryAccessReader(Database), Store, new InMemoryOperationLog(Database),
            new MapViewBuilder(AppSettings.Default), Project.CurrentUser, Project.Clock, AppSettings.Default, Project.Versions);
        Queries = new QueryRunner(new InMemoryAccessReader(Database), Store, new InMemoryOperationLog(Database), new MapViewBuilder(AppSettings.Default),
            Project.CurrentUser);
    }

    public TestProject Project { get; } = new();

    public InMemoryDatabase Database => Project.Database;

    public FakeDirectory Directory { get; } = new();

    public FakeAdministrators Administrators { get; } = new();

    public MemberUsersStore Store { get; }

    public InMemoryUserReader Users { get; }

    public InMemoryProjectReader Projects { get; }

    public FakeTrashPurge Purge { get; }

    /// <summary>The ports of the assistant (3.0): its settings in memory, a protector and two connectors that answer as told.</summary>
    public AssistantDoubles Assistant { get; }

    public UserPorts Ports { get; }

    /// <summary>The users kept by the sign-ins of the fixture (shared by its runner, as the host shares one per process).</summary>
    public SignInCache SignedIn { get; } = new();

    public UserRunner Runner { get; }

    public CommandRunner Commands { get; }

    public QueryRunner Queries { get; }

    public UsersFixture ActingAs(CurrentUser user)
    {
        Project.ActingAs(user);
        return this;
    }

    public AppUser UserRecord(Guid id) => Database.Get<AppUser>(AppUser.KeyOf(id));

    /// <summary>A second live or trashed project of the database, seen only through the reads across the projects.</summary>
    public Guid OtherProject(string code, CurrentUser member, MemberRole role, bool inTrash = false, int steps = 0)
    {
        var id = Project.NextId();
        var map = Project.NextId();
        var lane = Project.NextId();
        var now = TestProject.Now;
        Database.Add(new Project(id, code, $"Projet {code}", "fr", null, null, Classification.Internal, null, null, 0, now, member.Id, null, null,
            inTrash ? now.AddDays(-2) : null, inTrash ? member.Id : null, 1));
        Database.Add(new ProcessMap(map, id, null, $"Projet {code}", "fr", now, member.Id, null, null, null, null, 1));
        Database.Add(new Lane(lane, map, $"{code}-C01", "Acteur 1", "fr", LaneType.Person, null, "#E3F2FD", 0, null, now, member.Id, null, null, null, null, 1));
        Database.Add(new ProjectMember(id, member.Id, role, false, false, 0, now, member.Id, null, null));
        for (var column = 0; column < steps; column++)
            Database.Add(new Step(Project.NextId(), map, lane, $"{code}-E{column + 1:D3}", $"Étape {column}", "fr", StepType.Task, column, 0, null,
                AutomationStatus.Manual, null, now, member.Id, null, null, null, null, 1));
        return id;
    }
}
