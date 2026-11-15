// AssistantPainPointsTests.cs
// The pain points a report of a map is given (plan 3.1, L4): those the sheets of its steps show, from the uses of the outputs, each once, with the step it was raised on, its text, its hours and its status; none of another map.

using Topogenia.Application.Assistant;
using Topogenia.Application.Authorization;
using Topogenia.Application.Documents;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Documents;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using static Topogenia.Application.Tests.Sheets.SheetFixture;

namespace Topogenia.Application.Tests.Assistant;

public sealed class AssistantPainPointsTests
{
    /// <summary>The pain points of a map read as a query reads the workspace.</summary>
    [RequiresRole(MemberRole.Reader)]
    private sealed class PainPointsOfMap(Guid mapId) : ProjectQuery<IReadOnlyList<AssistantPainPointView>>(AccessTarget.Map(mapId))
    {
        public override WorkspaceContent Content => WorkspaceContent.All;

        public override IReadOnlyList<AssistantPainPointView> Read(ProjectWorkspace workspace, QueryContext context) => AssistantPainPoints.Of(workspace, mapId);
    }

    [Fact]
    public async Task A_map_gives_the_pain_points_of_the_uses_of_its_steps_once_with_the_step_they_were_raised_on()
    {
        var project = new TestProject();
        var producer = project.Step(0);
        var first = project.Step(1, assignee: project.Contributor.Id);
        var second = project.Step(2, assignee: project.Contributor.Id);
        Flow(project, producer, first);
        Flow(project, producer, second);
        var document = (await project.Runner.RunAsync(new AddDocumentCommand(producer, Output("Facture")), Token)).Result;
        var contributor = project.ActingAs(project.Contributor).Runner;
        await contributor.RunAsync(new UseDocumentCommand(first, document, new DocumentUseInput(3m, 80, "Souvent en retard", 2.25m)), Token);
        await contributor.RunAsync(new UseDocumentCommand(second, document, new DocumentUseInput(1m, 90, null, null)), Token);

        var points = await project.Queries.RunAsync(new PainPointsOfMap(project.RootMapId), Token);
        var producerCode = project.Database.Get<Step>(Step.KeyOf(producer)).Code;
        Assert.Equal([new AssistantPainPointView("PRJ-I001", producerCode, "Souvent en retard", 2.3m, Codes.Of(PainPointStatus.Reported))], points);

        var (detail, lane) = project.SubMap(producer);
        project.Step(0, laneId: lane, mapId: detail);
        Assert.Empty(await project.Queries.RunAsync(new PainPointsOfMap(detail), Token));
    }

    [Fact]
    public async Task A_pain_point_of_a_document_whose_producing_step_is_in_the_trash_is_not_given_as_no_sheet_shows_it_any_more()
    {
        var project = new TestProject();
        var producer = project.Step(0);
        var user = project.Step(1, assignee: project.Contributor.Id);
        Flow(project, producer, user);
        var document = (await project.Runner.RunAsync(new AddDocumentCommand(producer, Output("Facture")), Token)).Result;
        await project.ActingAs(project.Contributor).Runner.RunAsync(new UseDocumentCommand(user, document, new DocumentUseInput(3m, 80, "Souvent en retard", 2.25m)), Token);
        Assert.Single(await project.Queries.RunAsync(new PainPointsOfMap(project.RootMapId), Token));

        await project.ActingAs(project.Facilitator).Runner.RunAsync(new Topogenia.Application.Steps.DeleteStepCommand(producer), Token);

        Assert.Empty(await project.Queries.RunAsync(new PainPointsOfMap(project.RootMapId), Token));
    }
}
