// ReadingFixture.cs
// The templates of lot C on the adapters of the persistence: user, directory, administrators, clocks and runners in a session of their own.

using Topogenia.Application.Commands;
using Topogenia.Application.Maps;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Persistence.Assistant;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Purge;
using Topogenia.Persistence.Reading;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Reading;

/// <summary>A protector of the tests of the persistence: the text kept shows it is not the secret, and gives it back.</summary>
internal sealed class MarkedSecrets : ISecretProtector
{
    public string Protect(string secret) => "marked:" + secret;

    public string? Unprotect(string protectedSecret) => protectedSecret.StartsWith("marked:", StringComparison.Ordinal) ? protectedSecret[7..] : null;
}

internal sealed class RequestUser(CurrentUser? user) : ICurrentUser
{
    public CurrentUser? User { get; } = user;
}

internal sealed class ConfiguredAdministrators(params string[] logins) : IAdministrators
{
    public IReadOnlyCollection<string> Logins { get; } = logins;
}

/// <summary>A directory that knows nobody, and makes the first call of each unit meet the others when a meeting is given.</summary>
internal sealed class MeetingDirectory(Rendezvous? meeting = null) : IUserDirectory
{
    private int calls;

    public async Task<DirectoryEntry?> FindAsync(string login, CancellationToken cancellationToken)
    {
        if (meeting is not null)
            await meeting.ArriveAsync(Interlocked.Increment(ref calls));
        return null;
    }
}

internal sealed class ShiftedClock(TimeSpan shift) : IClock
{
    public DateTime UtcNow => FixedClock.Time + shift;
}

/// <summary>The runners of the application on the adapters of the persistence, each in the session it is given.</summary>
internal static class Runners
{
    public static CurrentUser UserOf(AppUser user, bool isAdmin = false) => new(user.Id, user.Login, user.DisplayName, "fr", isAdmin);

    public static UserPorts Ports(StoreSession session, IUserDirectory? directory = null, params string[] administrators) =>
        new(new UserReader(session), new ProjectReader(session), new WorkspaceStore(session), directory ?? new MeetingDirectory(),
            new ConfiguredAdministrators(administrators), new TrashPurge(session), new AuditStore(session),
            new AssistantPorts(new AssistantSettingsStore(session), new MarkedSecrets(), []));

    public static UserRunner Users(StoreSession session, CurrentUser? user, IUserDirectory? directory = null, params string[] administrators) =>
        new(new UnitOfWork(session), new AccessReader(session), Ports(session, directory, administrators), new RequestUser(user), new FixedClock(),
            AppSettings.Default, StoreScenario.Languages, new VersionsRead(), new SignInCache());

    public static CommandRunner Commands(StoreSession session, CurrentUser user) =>
        new(new UnitOfWork(session), new AccessReader(session), new WorkspaceStore(session), new OperationLogStore(session),
            new MapViewBuilder(AppSettings.Default), new RequestUser(user), new FixedClock(), AppSettings.Default, new VersionsRead());

    public static QueryRunner Queries(StoreSession session, CurrentUser user) =>
        new(new AccessReader(session), new WorkspaceStore(session), new OperationLogStore(session), new MapViewBuilder(AppSettings.Default),
            new RequestUser(user));

    /// <summary>A user request run in a session of its own.</summary>
    public static async Task<T> RunAsync<T>(TestDatabase database, CurrentUser user, UserRequest<T> request)
    {
        await using var session = database.Session();
        return await Users(session, user).RunAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>A command of a project run in a session of its own.</summary>
    public static async Task<CommandOutcome<T>> RunAsync<T>(TestDatabase database, CurrentUser user, ProjectCommand<T> command)
    {
        await using var session = database.Session();
        return await Commands(session, user).RunAsync(command, TestContext.Current.CancellationToken);
    }

    /// <summary>A read of a project run in a session of its own.</summary>
    public static async Task<T> RunAsync<T>(TestDatabase database, CurrentUser user, ProjectQuery<T> query)
    {
        await using var session = database.Session();
        return await Queries(session, user).RunAsync(query, TestContext.Current.CancellationToken);
    }
}
