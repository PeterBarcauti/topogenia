// NewProjectWritingTests.cs
// A change set that creates a project with a deletion batch that names it and a new user (the import of an open frame, ADR 052) is written.

using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Writing;

public sealed class NewProjectWritingTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_new_project_and_a_new_user_named_by_a_deletion_batch_of_the_same_change_set_are_written_before_it(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        var at = FixedClock.Time;
        var author = new AppUser(Guid.CreateVersion7(), @"FICTIF\auteur", @"FICTIF\AUTEUR", "Auteur fictif", null, "fr", false, true, null, at, null, null, 1);
        var context = new OperationContext(author.Id, at, "fr", AppSettings.Default);
        var created = ProjectCreation.Create(context, new CreateProjectRequest("NWPLAN", "Projet importé"),
            new ProjectCreationInputs([], StoreScenario.Languages, "fr", new PeopleLookup([author], new Dictionary<string, DirectoryEntry>(), [])));
        var batch = new DeletionBatch(Guid.CreateVersion7(), created.Project.Id, null, at, author.Id, null, null);
        var changes = new ChangeSet([StoreScenario.Created(author), .. created.Records.Select(StoreScenario.Created), StoreScenario.Created(batch)]);

        var plan = WritePlan.Of(changes);
        await scenario.WriteAsync(changes);

        Assert.Equal([typeof(AppUser), typeof(Project), typeof(DeletionBatch)], plan.First.Select(write => write.Record.GetType()));
        await using var session = scenario.Database.Session();
        var stored = (await new WorkspaceStore(session).LoadAsync(created.Project.Id, WorkspaceContent.All, TestContext.Current.CancellationToken))!;
        Assert.Equal(batch, Assert.Single(stored.All<DeletionBatch>()));
        Assert.Equal(author.Id, stored.Project.CreatedBy);
    }
}
