// ProfileWriteTests.cs
// The profile of a user is written only when it changes: an unchanged profile sends nothing to the engine (spec 5.6-6, faiblesse 6).

using Topogenia.Domain.Projects;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Queries;

/// <summary>
/// The 1.x read and wrote the profile at each request. The domain gives an empty change set for a profile that does not
/// change (`Versioning.Update`, AR-07); the persistence part proved here is that an empty change set sends no command at
/// all, and that a real change is one statement on the row of the user, its version checked. The short cache of the
/// profile per user belongs to the Application (P4-T07).
/// </summary>
public sealed class ProfileWriteTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static async Task<CommandLog> WriteAsync(StoreScenario scenario, ChangeSet changes)
    {
        var log = new CommandLog();
        await using var session = scenario.Database.Session(null, log);
        var unit = new UnitOfWork(session);
        var store = new WorkspaceStore(session);
        await unit.RunAsync(async token =>
        {
            await store.SaveAsync(changes, token);
            return true;
        }, TestContext.Current.CancellationToken);
        return log;
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_unchanged_profile_sends_no_command_and_a_changed_one_a_single_write(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        var actor = scenario.Actor;

        var same = UserRules.ChangeProfile(scenario.Context(), actor, new ProfileChange(Language: actor.Language, DisplayName: actor.DisplayName), StoreScenario.Languages);
        var unchanged = await WriteAsync(scenario, same.Changes);
        var changed = UserRules.ChangeProfile(scenario.Context(), actor, new ProfileChange(Language: "en"), StoreScenario.Languages);
        var written = await WriteAsync(scenario, changed.Changes);

        Assert.False(same.Changed);
        Assert.Empty(unchanged.Commands);
        Assert.True(changed.Changed);
        Assert.Equal((0, 1), (written.Reads, written.Writes));
        var stored = await scenario.LoadAsync();
        Assert.Equal(changed.Record, stored.All<Domain.Model.AppUser>().Single(user => user.Id == actor.Id));
    }
}
