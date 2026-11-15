// AssistantIndicatorsTests.cs
// The indicators the model is given (F-109, decision of 07/11/2026): those of the panel « Analysis » now, as the person sees them, then each photo, the reference of a target state first, with its comparison with now made by the core; the working day of the project.

using Topogenia.Application.Assistant;
using Topogenia.Application.Authorization;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Application.Snapshots;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Tests.Assistant;

public sealed class AssistantIndicatorsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The indicators of a project read as a query reads the workspace, as the person of the query sees them.</summary>
    [RequiresRole(MemberRole.Reader)]
    private sealed class IndicatorsOf(Guid projectId) : ProjectQuery<AssistantIndicators>(AccessTarget.Project(projectId))
    {
        public override WorkspaceContent Content => WorkspaceContent.All;

        public override AssistantIndicators Read(ProjectWorkspace workspace, QueryContext context) => AssistantIndicators.Of(workspace, context.Viewer);
    }

    private static PainPoint OpenPainPoint(TestProject project, Guid step) =>
        new(project.NextId(), project.ProjectId, step, project.Project.Code + "-I001", "Ressaisie", "fr", 3m, PainPointStatus.Reported, TestProject.Now,
            project.Project.CreatedBy, null, null, 1);

    [Fact]
    public async Task The_model_is_given_the_indicators_now_and_each_photo_compared_with_now_by_the_core()
    {
        var project = new TestProject();
        var step = project.Step(0);
        await project.Runner.RunAsync(new TakeSnapshotCommand(project.ProjectId, "Avant l'atelier"), Token);
        project.Database.Add(OpenPainPoint(project, step));

        var indicators = await project.Queries.RunAsync(new IndicatorsOf(project.ProjectId), Token);

        Assert.Equal((1, project.Project.WorkdayHours), (indicators.Now.OpenPainPoints, indicators.WorkdayHours));
        var photo = Assert.Single(indicators.Photos);
        Assert.Equal(("Avant l'atelier", SnapshotOrigin.Photo, TestProject.Now), (photo.Label, photo.Origin, photo.TakenAt));
        var opened = photo.Changes.Single(change => change.Indicator == "open_pain_points");
        Assert.Equal((0d, 1d, (bool?)false), (opened.Before, opened.After, opened.Better));
    }

    [Fact]
    public async Task The_reference_of_a_target_state_comes_before_the_photos()
    {
        var project = new TestProject();
        project.Step(0);
        await project.Runner.RunAsync(new TakeSnapshotCommand(project.ProjectId, "Après l'atelier"), Token);
        project.Database.Add(new StreamSnapshot(project.NextId(), project.ProjectId, "État actuel", SnapshotOrigin.Baseline, TestProject.Now.AddDays(1),
            project.Project.CreatedBy, null, null, 1));

        var indicators = await project.Queries.RunAsync(new IndicatorsOf(project.ProjectId), Token);

        Assert.Equal([("État actuel", SnapshotOrigin.Baseline), ("Après l'atelier", SnapshotOrigin.Photo)],
            indicators.Photos.Select(photo => (photo.Label, photo.Origin)));
        Assert.All(indicators.Photos, photo => Assert.Equal(12, photo.Changes.Count));
    }
}
