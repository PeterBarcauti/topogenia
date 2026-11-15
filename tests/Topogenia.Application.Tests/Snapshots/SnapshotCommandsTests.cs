// SnapshotCommandsTests.cs
// The photos of the indicators through the runners of the application (Makigami M8): taken by a facilitator, read with the indicators of now, deleted.

using Topogenia.Application.Snapshots;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Snapshots;

public sealed class SnapshotCommandsTests
{
    [Fact]
    public async Task A_photo_is_taken_read_with_the_indicators_of_now_and_deleted()
    {
        var project = new TestProject();
        project.Step(0);
        project.Step(1);

        await project.Runner.RunAsync(new TakeSnapshotCommand(project.ProjectId, "Atelier"), CancellationToken.None);
        var view = await project.Queries.RunAsync(new SnapshotsQuery(project.ProjectId), CancellationToken.None);

        var photo = Assert.Single(view.Photos);
        Assert.Equal(("Atelier", SnapshotOrigin.Photo), (photo.Label, photo.Origin));
        Assert.Equal(view.Current, photo.Indicators);
        // Each photo carries its comparison with now (P3e): twelve indicators, none moved since it was taken.
        Assert.Equal(12, photo.Changes.Count);
        Assert.All(photo.Changes, change => Assert.True(change.Gain is null or 0d));
        Assert.Null(view.Baseline);
        Assert.Null(view.Comparison);

        await project.Runner.RunAsync(new DeleteSnapshotCommand(project.ProjectId, photo.Id), CancellationToken.None);
        Assert.Empty((await project.Queries.RunAsync(new SnapshotsQuery(project.ProjectId), CancellationToken.None)).Photos);
    }

    /// <summary>P3e: each photo carries its comparison with now, made by the core, which moves with the project after it.</summary>
    [Fact]
    public async Task Each_photo_is_compared_with_now_by_the_core_once_the_indicators_moved()
    {
        var project = new TestProject();
        var step = project.Step(0);
        await project.Runner.RunAsync(new TakeSnapshotCommand(project.ProjectId, "Avant"), CancellationToken.None);
        project.Database.Add(new PainPoint(project.NextId(), project.ProjectId, step, project.Project.Code + "-I001", "Ressaisie", "fr", 3m,
            PainPointStatus.Reported, TestProject.Now, project.Project.CreatedBy, null, null, 1));

        var view = await project.Queries.RunAsync(new SnapshotsQuery(project.ProjectId), CancellationToken.None);

        var opened = Assert.Single(view.Photos).Changes.Single(change => change.Indicator == "open_pain_points");
        Assert.Equal((0d, 1d, 1d, (bool?)false), (opened.Before, opened.After, opened.Gain, opened.Better));
    }
}
