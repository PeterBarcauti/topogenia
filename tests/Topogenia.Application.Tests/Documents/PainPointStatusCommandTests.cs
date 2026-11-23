// PainPointStatusCommandTests.cs
// The status of a pain point chosen on the sheet of the receiving step through the template (ADR 116): the right to fill that sheet, the If-Match on the pain point, one unit, no operation logged, no audit, and the undo of a structure operation outdated by it.

using Topogenia.Application.Dissolutions;
using Topogenia.Application.Documents;
using Topogenia.Application.Tests.Dissolutions;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Dissolutions;
using Topogenia.Domain.Documents;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using static Topogenia.Application.Tests.Sheets.SheetFixture;

namespace Topogenia.Application.Tests.Documents;

public sealed class PainPointStatusCommandTests
{
    private static PainPoint PointOf(TestProject project, Guid id) => project.Database.Get<PainPoint>(PainPoint.KeyOf(id));

    private static List<PainPointStatusChange> HistoryOf(TestProject project, Guid id) =>
        [.. project.Database.All<PainPointStatusChange>().Where(change => change.PainPointId == id).OrderBy(change => change.ChangedAt)
            .ThenBy(change => change.StatusBefore is not null)];

    /// <summary>A pain point raised on the producing step by the use of one of its outputs by the receiving step, through the commands; returns its identifier.</summary>
    private static async Task<Guid> RaisedAsync(TestProject project, Guid producer, Guid receiver, string description, decimal? hours = null)
    {
        project.ActingAs(project.Facilitator);
        var document = (await project.Runner.RunAsync(new AddDocumentCommand(producer, Output($"Sortie {description}")), Token)).Result;
        var used = await project.Runner.RunAsync(new UseDocumentCommand(receiver, document, new DocumentUseInput(1m, null, description, hours)), Token);
        return used.Result.PainPointId!.Value;
    }

    /// <summary>
    /// The contributor is given the producing step of the first pain point and the receiving step of the second: he may
    /// choose the status of the second only. The reader chooses none, a stranger finds none; the facilitator and the
    /// administrator choose every one.
    /// </summary>
    [Fact]
    public async Task The_command_requires_the_right_to_fill_the_receiving_sheet()
    {
        var project = new TestProject();
        var givenProducer = project.Step(0, assignee: project.Contributor.Id);
        var freeReceiver = project.Step(1);
        var freeProducer = project.Step(2);
        var givenReceiver = project.Step(3, assignee: project.Contributor.Id);
        var produced = await RaisedAsync(project, givenProducer, freeReceiver, "Saisie en double");
        var received = await RaisedAsync(project, freeProducer, givenReceiver, "Relance");

        Assert.Equal(ErrorCodes.AccessDenied, (await Refused(project.ActingAs(project.Reader), new ChangePainPointStatusCommand(freeReceiver, produced, "implemented"))).Code);
        Assert.Equal(ErrorCodes.AccessDenied, (await Refused(project.ActingAs(project.Reader), new ChangePainPointStatusCommand(givenReceiver, received, "implemented"))).Code);
        Assert.Equal(ErrorCodes.AccessDenied, (await Refused(project.ActingAs(project.Contributor), new ChangePainPointStatusCommand(freeReceiver, produced, "implemented"))).Code);
        Assert.Equal(ErrorCodes.NotFound, (await Refused(project.ActingAs(project.Stranger), new ChangePainPointStatusCommand(givenReceiver, received, "implemented"))).Code);
        Assert.Equal((PainPointStatus.Reported, PainPointStatus.Reported), (PointOf(project, produced).Status, PointOf(project, received).Status));

        await project.ActingAs(project.Contributor).Runner.RunAsync(new ChangePainPointStatusCommand(givenReceiver, received, "in_progress"), Token);
        Assert.Equal((PainPointStatus.InProgress, project.Contributor.Id), (PointOf(project, received).Status, HistoryOf(project, received).Single(change => change.StatusAfter == PainPointStatus.InProgress).ChangedBy));
        await project.ActingAs(project.Facilitator).Runner.RunAsync(new ChangePainPointStatusCommand(freeReceiver, produced, "implemented"), Token);
        Assert.Equal(PainPointStatus.Implemented, PointOf(project, produced).Status);
        await project.ActingAs(project.Admin).Runner.RunAsync(new ChangePainPointStatusCommand(freeReceiver, produced, "dropped"), Token);
        Assert.Equal((PainPointStatus.Dropped, project.Admin.Id), (PointOf(project, produced).Status, HistoryOf(project, produced).Single(change => change.StatusAfter == PainPointStatus.Dropped).ChangedBy));
    }

    /// <summary>The use and its pain point carry different versions: the If-Match is read on the pain point, never on the use.</summary>
    [Fact]
    public async Task The_version_read_is_the_version_of_the_pain_point()
    {
        var project = new TestProject();
        var (producer, receiver) = (project.Step(0), project.Step(1));
        var (point, document) = ActionPlanFixture.Raise(project, producer, receiver, "PRJ-I001", 2m);
        project.Database.Add(PointOf(project, point) with { Version = 4 });
        var use = project.Database.Get<DocumentUse>(DocumentUse.KeyOf(document, receiver));
        project.Database.Add(use with { Version = 2 });

        var onTheUse = await Refused(project, ChangePainPointStatusCommand.WithVersion(receiver, point, "implemented", 2));
        Assert.Equal((ErrorCodes.VersionConflict, (object?)"pain_point"), (onTheUse.Code, onTheUse.Details["object"]));
        Assert.Equal((PainPointStatus.Reported, 4), (PointOf(project, point).Status, PointOf(project, point).Version));

        await project.Runner.RunAsync(ChangePainPointStatusCommand.WithVersion(receiver, point, "implemented", 4), Token);
        Assert.Equal((PainPointStatus.Implemented, 5), (PointOf(project, point).Status, PointOf(project, point).Version));

        var stale = await Refused(project.ActingAs(project.Admin), ChangePainPointStatusCommand.WithVersion(receiver, point, "reported", 4));
        Assert.Equal((ErrorCodes.VersionConflict, (object?)project.Facilitator.Id), (stale.Code, stale.Details["updated_by"]));
        Assert.Equal(PainPointStatus.Implemented, PointOf(project, point).Status);
        Assert.Single(HistoryOf(project, point));
    }

    [Fact]
    public async Task The_change_is_one_unit_and_logs_no_operation_nor_audit()
    {
        var project = new TestProject();
        var (producer, receiver) = (project.Step(0), project.Step(1));
        var point = await RaisedAsync(project, producer, receiver, "Saisie en double", 2m);
        var created = Assert.Single(HistoryOf(project, point));
        project.Clock.UtcNow = TestProject.Now.AddDays(3);
        var (commits, calls, records) = (project.Database.Commits, project.Database.Calls.Count, project.Database.All<object>().Count());

        var outcome = await project.Runner.RunAsync(new ChangePainPointStatusCommand(receiver, point, "implemented"), Token);

        Assert.Equal(commits + 1, project.Database.Commits);
        Assert.Single(project.Database.Calls.Skip(calls), call => call == "save");
        Assert.Null(outcome.OperationId);
        Assert.Empty(project.Database.All<AuditEvent>());
        Assert.Equal(records + 1, project.Database.All<object>().Count());
        Assert.Equal((PainPointStatus.Implemented, 2), (PointOf(project, point).Status, PointOf(project, point).Version));
        var history = HistoryOf(project, point);
        Assert.Equal(created, history[0]);
        Assert.Equal(((PainPointStatus?)PainPointStatus.Reported, PainPointStatus.Implemented, (string?)null, project.Facilitator.Id, TestProject.Now.AddDays(3)),
            (history[1].StatusBefore, history[1].StatusAfter, history[1].Comment, history[1].ChangedBy, history[1].ChangedAt));
        // The reply carries the view of the map: a pain point dealt with leaves the markers of its step.
        Assert.Empty(outcome.View!.Steps.Single(step => step.Id == producer).Markers.PainPoints);

        // A refusal writes nothing; the status it has writes nothing either.
        var unknown = await Refused(project, new ChangePainPointStatusCommand(receiver, point, "closed"));
        Assert.Equal((ErrorCodes.InvalidValue, (object?)PainPointRules.StatusField), (unknown.Code, unknown.Details["field"]));
        await project.Runner.RunAsync(new ChangePainPointStatusCommand(receiver, point, "implemented"), Token);
        Assert.Equal((2, 2), (PointOf(project, point).Version, HistoryOf(project, point).Count));
        await AssertNothingToUndo(project);
    }

    [Fact]
    public async Task A_pain_point_named_under_another_step_or_unknown_is_not_found_and_nothing_changes()
    {
        var project = new TestProject();
        var (producer, receiver, other) = (project.Step(0), project.Step(1), project.Step(2));
        var point = await RaisedAsync(project, producer, receiver, "Saisie en double");
        foreach (var (step, id) in new[] { (producer, point), (other, point), (receiver, project.NextId()) })
            Assert.Equal(ErrorCodes.NotFound, (await Refused(project, new ChangePainPointStatusCommand(step, id, "implemented"))).Code);
        Assert.Equal(ErrorCodes.StepWithoutContent, (await Refused(project, new ChangePainPointStatusCommand(StepOfType(project, 3, StepType.End), point, "implemented"))).Code);
        Assert.Equal((PainPointStatus.Reported, 1), (PointOf(project, point).Status, HistoryOf(project, point).Count));
    }

    /// <summary>
    /// A pain point of the remainder of a dissolution assigned to a step of the frame (an operation of the log, « Affecter »),
    /// then its status chosen on the sheet of the step that receives it: the undo of the assignment finds the pain point
    /// changed since and is refused, instead of putting it back in silence (spec 4.6, ADR 116).
    /// </summary>
    [Fact]
    public async Task An_undo_of_a_structure_operation_that_touched_the_pain_point_is_outdated_after_its_status_changed()
    {
        var flow = new DetailedFlow();
        flow.Project.Database.Add(new DocumentUse(flow.Document, flow.After, null, null, flow.PainPoint, TestProject.Now, flow.Project.Facilitator.Id, null, null));
        var frame = (await flow.RunAsync(new DissolveCommand(flow.Macro))).Result.DissolutionId;
        await flow.RunAsync(new AssignRemainderCommand(frame, new AssignRequest("pain_point", flow.PainPoint, flow.First)));
        Assert.Equal(flow.First, PointOf(flow.Project, flow.PainPoint).StepId);

        await flow.RunAsync(new ChangePainPointStatusCommand(flow.After, flow.PainPoint, "implemented"));

        var refusal = await Assert.ThrowsAsync<DomainException>(() => Selection.SelectionFixture.UndoAsync(flow.Project));
        Assert.Equal((ErrorCodes.OperationOutdated, (object?)"pain_point"), (refusal.Code, refusal.Details["object"]));
        Assert.Equal((flow.First, PainPointStatus.Implemented), (PointOf(flow.Project, flow.PainPoint).StepId, PointOf(flow.Project, flow.PainPoint).Status));
    }
}
