// TrashCommandsTests.cs
// The trash of the projects through the templates: move, restore, list and immediate purge, with their rights (annex A 1, A 2.15).

using Topogenia.Application.Maps;
using Topogenia.Application.Tests.Support;
using Topogenia.Application.Tests.Users;
using Topogenia.Application.Trash;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Trash;

public sealed class TrashCommandsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<string> RefusalAsync<T>(Func<Task<T>> run) => (await Assert.ThrowsAsync<DomainException>(run)).Code;

    [Fact]
    public async Task The_exact_code_moves_the_project_to_the_trash_and_nothing_is_logged()
    {
        var fixture = new UsersFixture();
        var outcome = await fixture.Commands.RunAsync(new MoveToTrashCommand(fixture.Project.ProjectId, " PRJ "), Token);
        Assert.Equal(fixture.Project.ProjectId, outcome.Result);
        Assert.Equal((TestProject.Now, (Guid?)fixture.Project.Facilitator.Id), (fixture.Project.Project.DeletedAt, fixture.Project.Project.DeletedBy));
        Assert.Equal((null, null), (outcome.OperationId, outcome.View));
        Assert.Empty(fixture.Database.Entries);
    }

    [Fact]
    public async Task Another_confirmation_is_refused_with_the_code_and_the_project_stays_live()
    {
        var fixture = new UsersFixture();
        var refusal = await Assert.ThrowsAsync<DomainException>(() => fixture.Commands.RunAsync(new MoveToTrashCommand(fixture.Project.ProjectId, "prj"), Token));
        Assert.Equal((ErrorCodes.InvalidConfirmation, "PRJ"), (refusal.Code, refusal.Details["code"]));
        Assert.Null(fixture.Project.Project.DeletedAt);
        fixture.ActingAs(fixture.Project.Contributor);
        Assert.Equal(ErrorCodes.AccessDenied, await RefusalAsync(() => fixture.Commands.RunAsync(new MoveToTrashCommand(fixture.Project.ProjectId, "PRJ"), Token)));
        Assert.Null(fixture.Project.Project.DeletedAt);
    }

    [Fact]
    public async Task A_project_in_the_trash_is_reached_by_nobody_but_the_trash()
    {
        var fixture = new UsersFixture();
        await fixture.Commands.RunAsync(new MoveToTrashCommand(fixture.Project.ProjectId, "PRJ"), Token);
        foreach (var user in new[] { fixture.Project.Facilitator, fixture.Project.Admin })
        {
            fixture.ActingAs(user);
            Assert.Equal(ErrorCodes.NotFound, await RefusalAsync(() => fixture.Queries.RunAsync(new GetMapQuery(fixture.Project.RootMapId), Token)));
            Assert.Equal(ErrorCodes.NotFound, await RefusalAsync(() =>
                fixture.Commands.RunAsync(new MoveToTrashCommand(fixture.Project.ProjectId, "PRJ"), Token)));
        }
        fixture.ActingAs(fixture.Project.Facilitator);
        await fixture.Commands.RunAsync(new RestoreProjectCommand(fixture.Project.ProjectId), Token);
        Assert.Equal((null, null), (fixture.Project.Project.DeletedAt, fixture.Project.Project.DeletedBy));
        Assert.Equal(fixture.Project.RootMapId, (await fixture.Queries.RunAsync(new GetMapQuery(fixture.Project.RootMapId), Token)).View.Id);
        Assert.Empty(fixture.Database.Entries);
    }

    [Fact]
    public async Task A_facilitator_of_the_deleted_project_or_an_administrator_restores_it_and_nobody_else()
    {
        var fixture = new UsersFixture();
        Assert.Equal(ErrorCodes.NotFound, await RefusalAsync(() => fixture.Commands.RunAsync(new RestoreProjectCommand(fixture.Project.ProjectId), Token)));
        fixture.Project.PutInTrash();
        fixture.ActingAs(fixture.Project.Contributor);
        Assert.Equal(ErrorCodes.AccessDenied, await RefusalAsync(() => fixture.Commands.RunAsync(new RestoreProjectCommand(fixture.Project.ProjectId), Token)));
        fixture.ActingAs(fixture.Project.Stranger);
        Assert.Equal(ErrorCodes.NotFound, await RefusalAsync(() => fixture.Commands.RunAsync(new RestoreProjectCommand(fixture.Project.ProjectId), Token)));
        Assert.True(fixture.Project.Project.IsInTrash);
        fixture.ActingAs(fixture.Project.Admin);
        await fixture.Commands.RunAsync(new RestoreProjectCommand(fixture.Project.ProjectId), Token);
        Assert.False(fixture.Project.Project.IsInTrash);
    }

    [Fact]
    public async Task The_trash_lists_the_projects_the_user_facilitated_with_their_purge_date_and_every_one_for_an_administrator()
    {
        var fixture = new UsersFixture();
        fixture.Project.Step(0);
        await fixture.Commands.RunAsync(new MoveToTrashCommand(fixture.Project.ProjectId, "PRJ"), Token);
        fixture.OtherProject("ACH", fixture.Project.Contributor, MemberRole.Facilitator, inTrash: true, steps: 2);
        fixture.OtherProject("VTE", fixture.Project.Facilitator, MemberRole.Facilitator);

        var trash = await fixture.Runner.RunAsync(new ListTrashQuery(), Token);
        Assert.Equal(30, trash.RetentionDays);
        var item = Assert.Single(trash.Projects);
        Assert.Equal(new TrashItem(fixture.Project.ProjectId, "PRJ", "Achats fournisseurs", TestProject.Now, "Anne Animatrice", TestProject.Now.AddDays(30), 1), item);
        fixture.ActingAs(fixture.Project.Contributor);
        Assert.Equal(["ACH"], (await fixture.Runner.RunAsync(new ListTrashQuery(), Token)).Projects.Select(project => project.Code));
        fixture.ActingAs(fixture.Project.Admin);
        Assert.Equal(["PRJ", "ACH"], (await fixture.Runner.RunAsync(new ListTrashQuery(), Token)).Projects.Select(project => project.Code));
    }

    [Fact]
    public async Task An_administrator_purges_a_project_of_the_trash_at_once_and_never_a_live_one()
    {
        var fixture = new UsersFixture();
        fixture.ActingAs(fixture.Project.Admin);
        Assert.Equal(ErrorCodes.NotFound, await RefusalAsync(() => fixture.Runner.RunAsync(new PurgeProjectCommand(fixture.Project.ProjectId), Token)));
        fixture.Project.PutInTrash();
        var outcome = await fixture.Runner.RunAsync(new PurgeProjectCommand(fixture.Project.ProjectId), Token);
        var (projectId, plan) = Assert.Single(fixture.Purge.Purged);
        Assert.True(plan.PurgesProject);
        Assert.Equal(fixture.Project.ProjectId, projectId);
        Assert.Equal(plan.Records.Count, outcome.Records);
        Assert.DoesNotContain(fixture.Database.All<Project>(), project => project.Id == fixture.Project.ProjectId);
        Assert.DoesNotContain(fixture.Database.All<ProjectMember>(), member => member.ProjectId == fixture.Project.ProjectId);
        Assert.Equal(0, fixture.Project.Unit.Attempts);
    }

    [Fact]
    public async Task A_facilitator_may_not_purge_his_project_of_the_trash()
    {
        var fixture = new UsersFixture();
        fixture.Project.PutInTrash();
        Assert.Equal(ErrorCodes.AccessDenied, await RefusalAsync(() => fixture.Runner.RunAsync(new PurgeProjectCommand(fixture.Project.ProjectId), Token)));
        Assert.Empty(fixture.Purge.Purged);
        Assert.True(fixture.Project.Project.IsInTrash);
    }
}
