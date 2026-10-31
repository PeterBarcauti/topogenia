// AuditStoreTests.cs
// The adapter of the audit trail on both engines: events written with their change or on their own, read newest first by filter, removed past the retention (spec 5.5).

using Topogenia.Application.Audit;
using Topogenia.Application.Members;
using Topogenia.Application.Ports;
using Topogenia.Application.Projects;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Persistence.Reading;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Reading;

public sealed class AuditStoreTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static AuditEvent Event(DateTime at, string kind, Guid? user, Guid? project) => new(Guid.CreateVersion7(), at, user, kind, project, """{"n":1}""");

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_event_appended_is_read_back_newest_first_by_period_project_user_and_kind(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        await using var session = scenario.Database.Session();
        var store = new AuditStore(session);
        var start = new DateTime(2031, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(Random.Shared.Next(1_000_000));
        var project = Guid.CreateVersion7();
        var kindOf = $"test.{Guid.CreateVersion7():N}";
        var events = new[]
        {
            Event(start, kindOf, scenario.Actor.Id, project), Event(start.AddMinutes(1), kindOf, scenario.Other.Id, project),
            Event(start.AddMinutes(2), kindOf, scenario.Actor.Id, null), Event(start.AddMinutes(3), kindOf, null, project),
        };
        foreach (var audit in events)
            await store.AppendAsync(audit, Token);
        var all = await store.ReadAsync(new AuditQuery(null, null, null, null, kindOf, 10), Token);
        Assert.Equal(events.Reverse().Select(audit => audit.Id), all.Select(audit => audit.Id));
        Assert.Equal(events[0], all[^1]);
        var ofProject = await store.ReadAsync(new AuditQuery(start.AddMinutes(1), start.AddMinutes(3), project, null, kindOf, 10), Token);
        Assert.Equal([events[1].Id], ofProject.Select(audit => audit.Id));
        var ofUser = await store.ReadAsync(new AuditQuery(null, null, null, scenario.Actor.Id, kindOf, 1), Token);
        Assert.Equal([events[2].Id], ofUser.Select(audit => audit.Id));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_events_before_the_limit_are_removed_and_the_others_stay(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        await using var session = scenario.Database.Session();
        var store = new AuditStore(session);
        var kindOf = $"test.{Guid.CreateVersion7():N}";
        var old = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await store.AppendAsync(Event(old, kindOf, null, null), Token);
        await store.AppendAsync(Event(old.AddDays(2), kindOf, null, null), Token);
        Assert.True(await store.RemoveBeforeAsync(old.AddDays(1), Token) >= 1);
        var left = await store.ReadAsync(new AuditQuery(null, null, null, null, kindOf, 10), Token);
        Assert.Equal([old.AddDays(2)], left.Select(audit => audit.At));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_event_is_never_appended_inside_a_unit_of_work_where_it_belongs_to_the_change_set(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        await using var session = scenario.Database.Session();
        var store = new AuditStore(session);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new UnitOfWork(session).RunAsync(async token =>
        {
            await store.AppendAsync(Event(DateTime.UtcNow, "test.inside", null, null), token);
            return true;
        }, Token));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_project_created_and_its_members_changed_are_written_with_their_events(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        var admin = Runners.UserOf(scenario.Actor, isAdmin: true);
        var created = await Runners.RunAsync(scenario.Database, admin, new CreateProjectCommand(new CreateProjectRequest(ScenarioSteps.NewProjectCode(), "Ventes")));
        await using (var session = scenario.Database.Session())
        {
            var people = PeopleSource.Of(Runners.Ports(session));
            await Runners.Commands(session, Runners.UserOf(scenario.Actor)).RunAsync(new ReplaceMembersCommand(created.Id,
                [new MemberEntry(scenario.Actor.Login, "facilitator", false), new MemberEntry(scenario.Other.Login, "reader", false)], people), Token);
        }
        await using var reading = scenario.Database.Session();
        var events = await new AuditStore(reading).ReadAsync(new AuditQuery(null, null, created.Id, null, null, 10), Token);
        Assert.Equal([AuditKinds.ProjectCreated, AuditKinds.MembersChanged], events.Select(audit => audit.Kind).Order(StringComparer.Ordinal));
        Assert.All(events, audit => Assert.Equal(scenario.Actor.Id, audit.UserId));
        Assert.Contains(scenario.Other.Id.ToString(), events.Single(audit => audit.Kind == AuditKinds.MembersChanged).Details, StringComparison.OrdinalIgnoreCase);
    }
}
