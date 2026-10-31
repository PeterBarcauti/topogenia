// ExpiredPurgeTests.cs
// The daily purge: the projects in the trash past the retention, the expired batches of the live projects, a failure that blocks nothing (annex A 2.15).

using Topogenia.Application.Audit;
using Topogenia.Application.Maintenance;
using Topogenia.Application.Ports;
using Topogenia.Application.Tests.Support;
using Topogenia.Application.Tests.Users;
using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Tests.Maintenance;

/// <summary>
/// The workspace store over a database of several projects: each load keeps the records of its project only (the
/// persistence reads a project by joins on it, `ProjectScope`).
/// </summary>
public sealed class ProjectStore(InMemoryDatabase database) : IWorkspaceStore
{
    public Task<ProjectWorkspace?> LoadAsync(Guid projectId, WorkspaceContent content, CancellationToken cancellationToken)
    {
        if (database.Load(projectId, content) is null)
            return Task.FromResult<ProjectWorkspace?>(null);
        var maps = database.All<ProcessMap>().Where(map => map.ProjectId == projectId).Select(map => map.Id).ToHashSet();
        var records = database.All<IRecord>().Where(record => record switch
        {
            AppUser => false,
            Project project => project.Id == projectId,
            ProcessMap map => map.ProjectId == projectId,
            Lane lane => maps.Contains(lane.MapId),
            Step step => maps.Contains(step.MapId),
            ProjectMember member => member.ProjectId == projectId,
            ProjectCounter counter => counter.ProjectId == projectId,
            DeletionBatch batch => batch.ProjectId == projectId,
            _ => throw new InvalidOperationException($"No project rule for {record.GetType().Name}."),
        });
        return Task.FromResult<ProjectWorkspace?>(new ProjectWorkspace(projectId, records));
    }

    public Task SaveAsync(ChangeSet changes, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The purge writes through its port only.");
}

public sealed class ExpiredPurgeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (UsersFixture Fixture, Guid Trashed, FakeTrashPurge Purge, ExpiredPurge Job) Setup(int trashedDaysAgo, int batchDaysAgo)
    {
        var fixture = new UsersFixture();
        var live = fixture.Project;
        var trashed = fixture.OtherProject("ACH", live.Facilitator, Domain.Values.MemberRole.Facilitator, inTrash: true, steps: 2);
        fixture.Database.Add(fixture.Database.Get<Project>(Project.KeyOf(trashed)) with { DeletedAt = TestProject.Now.AddDays(-trashedDaysAgo) });
        var batch = new DeletionBatch(live.NextId(), live.ProjectId, null, TestProject.Now.AddDays(-batchDaysAgo), live.Facilitator.Id, null, null);
        var step = live.Step(0);
        fixture.Database.Add(batch, fixture.Database.Get<Step>(Step.KeyOf(step)) with { DeletedAt = batch.DeletedAt, DeletionBatchId = batch.Id });
        var purge = new FakeTrashPurge(fixture.Database);
        return (fixture, trashed, purge, new ExpiredPurge(live.Clock, purge, new ProjectStore(fixture.Database), AppSettings.Default,
            new InMemoryAuditStore(fixture.Database)));
    }

    [Fact]
    public async Task A_project_in_the_trash_past_the_retention_goes_whole_and_an_expired_batch_goes_with_what_it_deleted()
    {
        var (fixture, trashed, purge, job) = Setup(31, 31);
        var report = await job.RunAsync(Token);
        Assert.Equal(TestProject.Now.AddDays(-30), report.Limit);
        Assert.Empty(report.Failures);
        Assert.Equal([(trashed, true, 0), (fixture.Project.ProjectId, false, 1)],
            report.Purged.OrderBy(done => done.Whole ? 0 : 1).Select(done => (done.ProjectId, done.Whole, done.Batches)));
        var project = Assert.Single(fixture.Database.All<Project>());
        Assert.Equal(fixture.Project.ProjectId, project.Id);
        Assert.Empty(fixture.Database.All<DeletionBatch>());
        Assert.Empty(fixture.Database.All<Step>());
        Assert.Equal([fixture.Project.LaneId], fixture.Database.All<Lane>().Select(lane => lane.Id));
        Assert.Equal(2, purge.Purged.Count);
    }

    [Fact]
    public async Task A_whole_project_purged_is_traced_without_user_and_an_expired_batch_is_not()
    {
        var (fixture, trashed, _, job) = Setup(31, 31);
        await job.RunAsync(Token);
        var audit = Assert.Single(fixture.Database.All<AuditEvent>());
        Assert.Equal((AuditKinds.ProjectPurged, (Guid?)null, (Guid?)trashed, TestProject.Now), (audit.Kind, audit.UserId, audit.ProjectId, audit.At));
    }

    [Fact]
    public async Task Nothing_is_purged_before_the_retention()
    {
        var (fixture, _, purge, job) = Setup(29, 29);
        var report = await job.RunAsync(Token);
        Assert.Empty(report.Purged);
        Assert.Empty(purge.Purged);
        Assert.Equal(2, fixture.Database.All<Project>().Count());
        Assert.Single(fixture.Database.All<DeletionBatch>());
    }

    [Fact]
    public async Task A_project_whose_purge_fails_is_reported_and_the_others_are_still_purged()
    {
        var (fixture, trashed, purge, job) = Setup(31, 31);
        purge.Failing.Add(trashed);
        var report = await job.RunAsync(Token);
        var failure = Assert.Single(report.Failures);
        Assert.Equal(trashed, failure.ProjectId);
        Assert.IsType<InvalidOperationException>(failure.Failure);
        Assert.Equal([fixture.Project.ProjectId], report.Purged.Select(done => done.ProjectId));
        Assert.Equal(2, fixture.Database.All<Project>().Count());
        Assert.Empty(fixture.Database.All<DeletionBatch>());
    }
}
