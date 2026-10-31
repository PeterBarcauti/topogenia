// IfMatchCommandsTests.cs
// The If-Match of the deletions, translations, members and move to the trash: the version read of the object of the route, checked before the domain (spec 5.3, A-2.16-05, ADR 045).

using Topogenia.Application.Documents;
using Topogenia.Application.Flows;
using Topogenia.Application.Lanes;
using Topogenia.Application.Members;
using Topogenia.Application.Steps;
using Topogenia.Application.Tests.Steps;
using Topogenia.Application.Tests.Support;
using Topogenia.Application.Tests.Users;
using Topogenia.Application.Translations;
using Topogenia.Application.Trash;
using Topogenia.Domain.Documents;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Lanes;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;
using static Topogenia.Application.Tests.Sheets.SheetFixture;

namespace Topogenia.Application.Tests.Versions;

/// <summary>
/// The commands whose route reads `If-Match` since the porte P4 (M2): without a version nothing is checked (the desktop
/// mode, AR-07; the server mode refuses 428 in the template, `CommandTemplateTests`), a version other than the one of the
/// object is CONFLIT_VERSION on that object and writes nothing, the version read lets the command run.
/// </summary>
public sealed class IfMatchCommandsTests
{
    private static async Task<DomainException> ConflictAsync(Func<Task> run, string type)
    {
        var refusal = await Assert.ThrowsAsync<DomainException>(run);
        Assert.Equal((ErrorCodes.VersionConflict, (object?)type), (refusal.Code, refusal.Details["object"]));
        return refusal;
    }

    [Fact]
    public async Task A_deleted_lane_checks_the_version_read_of_the_lane()
    {
        var project = new TestProject();
        var lane = project.SecondLane();
        Assert.Empty(DeleteLaneCommand.WithVersion(new DeleteLaneRequest(lane), null).Versions);
        await ConflictAsync(() => project.Runner.RunAsync(DeleteLaneCommand.WithVersion(new DeleteLaneRequest(lane), 2), Token), "lane");
        Assert.Null(project.Database.Get<Lane>(Lane.KeyOf(lane)).DeletedAt);
        Assert.Empty(project.Database.Entries);
        await project.Runner.RunAsync(DeleteLaneCommand.WithVersion(new DeleteLaneRequest(lane), 1), Token);
        Assert.NotNull(project.Database.Get<Lane>(Lane.KeyOf(lane)).DeletedAt);
    }

    [Fact]
    public async Task A_deleted_step_checks_the_version_read_of_the_step()
    {
        var project = new TestProject();
        var step = project.Step(0);
        Assert.Empty(DeleteStepCommand.WithVersion(step, null, null, false, null).Versions);
        await ConflictAsync(() => project.Runner.RunAsync(DeleteStepCommand.WithVersion(step, null, null, false, 2), Token), "step");
        Assert.True(project.IsLiveStep(step));
        Assert.Empty(project.Database.Entries);
        await project.Runner.RunAsync(DeleteStepCommand.WithVersion(step, null, null, false, 1), Token);
        Assert.False(project.IsLiveStep(step));
    }

    [Fact]
    public async Task A_deleted_flow_checks_the_version_read_of_the_flow()
    {
        var project = new TestProject();
        var flow = project.Flow(project.Step(0), project.Step(1), "oui");
        Assert.Empty(DeleteFlowCommand.WithVersion(flow, null).Versions);
        await ConflictAsync(() => project.Runner.RunAsync(DeleteFlowCommand.WithVersion(flow, 2), Token), "flow");
        Assert.True(project.IsLiveFlow(flow));
        await project.Runner.RunAsync(DeleteFlowCommand.WithVersion(flow, 1), Token);
        Assert.False(project.IsLiveFlow(flow));
    }

    [Fact]
    public async Task The_translations_of_a_step_and_of_a_flow_check_the_version_read_of_their_object()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var flow = project.Flow(step, project.Step(1), "oui");
        var english = new Dictionary<string, string?> { ["en"] = "Order" };
        Assert.Empty(UpdateStepTranslationsCommand.WithVersion(step, english, Languages, null).Versions);
        Assert.Empty(UpdateFlowTranslationsCommand.WithVersion(flow, english, Languages, null).Versions);
        await ConflictAsync(() => project.Runner.RunAsync(UpdateStepTranslationsCommand.WithVersion(step, english, Languages, 2), Token), "step");
        await ConflictAsync(() => project.Runner.RunAsync(UpdateFlowTranslationsCommand.WithVersion(flow, english, Languages, 2), Token), "flow");
        Assert.Empty(project.Database.All<Translation>());
        await project.Runner.RunAsync(UpdateStepTranslationsCommand.WithVersion(step, english, Languages, 1), Token);
        await project.Runner.RunAsync(UpdateFlowTranslationsCommand.WithVersion(flow, english, Languages, 1), Token);
        Assert.Equal([step, flow], project.Database.All<Translation>().Select(translation => translation.ObjectId).Order());
    }

    [Fact]
    public async Task A_deleted_document_checks_the_version_read_of_the_document()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var document = (await project.Runner.RunAsync(new AddDocumentCommand(step, Output("Facture")), Token)).Result;
        Assert.Empty(DeleteDocumentCommand.WithVersion(step, document, null).Versions);
        await ConflictAsync(() => project.Runner.RunAsync(DeleteDocumentCommand.WithVersion(step, document, 2), Token), "document");
        Assert.Single(project.Database.All<Document>());
        await project.Runner.RunAsync(DeleteDocumentCommand.WithVersion(step, document, 1), Token);
        Assert.Empty(project.Database.All<Document>());
    }

    [Fact]
    public async Task Unticking_checks_the_version_read_of_the_use_zero_while_there_is_none_and_any_once_it_is_gone()
    {
        var project = new TestProject();
        var producer = project.Step(0);
        var step = project.Step(1);
        var document = (await project.Runner.RunAsync(new AddDocumentCommand(producer, Output("Facture")), Token)).Result;
        await project.Runner.RunAsync(UseDocumentCommand.WithVersion(step, document, new DocumentUseInput(1m), 0), Token);
        var read = Assert.Single(project.Database.All<DocumentUse>()).Version;
        Assert.Empty(StopUsingDocumentCommand.WithVersion(step, document, null).Versions);
        await ConflictAsync(() => project.Runner.RunAsync(StopUsingDocumentCommand.WithVersion(step, document, 0), Token), "use");
        await ConflictAsync(() => project.Runner.RunAsync(StopUsingDocumentCommand.WithVersion(step, document, read + 1), Token), "use");
        Assert.Single(project.Database.All<DocumentUse>());
        await project.Runner.RunAsync(StopUsingDocumentCommand.WithVersion(step, document, read), Token);
        Assert.Empty(project.Database.All<DocumentUse>());
        await project.Runner.RunAsync(StopUsingDocumentCommand.WithVersion(step, document, 0), Token);
        Assert.Empty(project.Database.All<DocumentUse>());
        // A second untick with the version read before the first: the use is gone, the state asked is reached (AR-107).
        await project.Runner.RunAsync(StopUsingDocumentCommand.WithVersion(step, document, read), Token);
        Assert.Empty(project.Database.All<DocumentUse>());
        await project.Runner.RunAsync(UseDocumentCommand.WithVersion(step, document, new DocumentUseInput(1m), 0), Token);
        await ConflictAsync(() => project.Runner.RunAsync(StopUsingDocumentCommand.WithVersion(step, document, read), Token), "use");
        Assert.Single(project.Database.All<DocumentUse>());
    }

    /// <summary>
    /// AR-111, the probe of the porte P4 round 2: A reads the use, B unticks it then ticks it again with other values, A
    /// unticks or changes it with the version it read. The use ticked again is another record: both are CONFLIT_VERSION
    /// and B's entry stays.
    /// </summary>
    [Fact]
    public async Task A_use_ticked_again_after_an_untick_refuses_the_version_read_of_the_use_unticked()
    {
        var project = new TestProject();
        var producer = project.Step(0);
        var step = project.Step(1);
        var document = (await project.Runner.RunAsync(new AddDocumentCommand(producer, Output("Facture")), Token)).Result;
        await project.Runner.RunAsync(UseDocumentCommand.WithVersion(step, document, new DocumentUseInput(1m), 0), Token);
        var readByA = Assert.Single(project.Database.All<DocumentUse>()).Version;

        await project.Runner.RunAsync(StopUsingDocumentCommand.WithVersion(step, document, readByA), Token);
        await project.Runner.RunAsync(UseDocumentCommand.WithVersion(step, document, new DocumentUseInput(7m, 80), 0), Token);
        var tickedByB = Assert.Single(project.Database.All<DocumentUse>());
        Assert.True(tickedByB.Version > readByA, $"The use ticked again took the version {tickedByB.Version}, read before as {readByA}.");

        await ConflictAsync(() => project.Runner.RunAsync(StopUsingDocumentCommand.WithVersion(step, document, readByA), Token), "use");
        await ConflictAsync(() => project.Runner.RunAsync(UseDocumentCommand.WithVersion(step, document, new DocumentUseInput(2m), readByA), Token), "use");
        Assert.Equal((7m, (int?)80), (Assert.Single(project.Database.All<DocumentUse>()).WaitHours, project.Database.All<DocumentUse>().Single().PercentCompleteAccurate));
        await project.Runner.RunAsync(StopUsingDocumentCommand.WithVersion(step, document, tickedByB.Version), Token);
        Assert.Empty(project.Database.All<DocumentUse>());
    }

    [Fact]
    public async Task A_replacement_of_the_members_checks_the_version_of_the_project_and_moves_it_on_only_when_the_list_changes()
    {
        var fixture = new UsersFixture();
        var project = fixture.Project;
        ReplaceMembersCommand Replace(string role, int? version) => ReplaceMembersCommand.WithVersion(project.ProjectId,
            [new MemberEntry(project.Facilitator.Login, "facilitator", true), new MemberEntry(project.Reader.Login, role, false)], PeopleSource.Of(fixture.Ports),
            version);
        Assert.Empty(Replace("reader", null).Versions);
        await ConflictAsync(() => fixture.Commands.RunAsync(Replace("contributor", 2), Token), "project");
        Assert.Equal(3, fixture.Database.All<ProjectMember>().Count());
        Assert.Equal(1, project.Project.Version);

        await fixture.Commands.RunAsync(Replace("contributor", 1), Token);
        Assert.Equal(2, fixture.Database.All<ProjectMember>().Count());
        Assert.Equal((2, (Guid?)project.Facilitator.Id), (project.Project.Version, project.Project.UpdatedBy));
        var concurrent = await ConflictAsync(() => fixture.Commands.RunAsync(Replace("reader", 1), Token), "project");
        Assert.Equal(project.Facilitator.Id, concurrent.Details["updated_by"]);
        Assert.Equal(MemberRole.Contributor, fixture.Database.All<ProjectMember>().Single(member => member.UserId == project.Reader.Id).Role);

        var events = fixture.Database.All<AuditEvent>().Count();
        await fixture.Commands.RunAsync(Replace("contributor", 2), Token);
        Assert.Equal((2, events), (project.Project.Version, fixture.Database.All<AuditEvent>().Count()));
    }

    [Fact]
    public async Task A_move_to_the_trash_checks_the_version_read_of_the_project()
    {
        var fixture = new UsersFixture();
        var project = fixture.Project;
        Assert.Empty(MoveToTrashCommand.WithVersion(project.ProjectId, "PRJ", null).Versions);
        await ConflictAsync(() => fixture.Commands.RunAsync(MoveToTrashCommand.WithVersion(project.ProjectId, "PRJ", 2), Token), "project");
        Assert.Null(project.Project.DeletedAt);
        await fixture.Commands.RunAsync(MoveToTrashCommand.WithVersion(project.ProjectId, "PRJ", 1), Token);
        Assert.Equal(TestProject.Now, project.Project.DeletedAt);
    }
}
