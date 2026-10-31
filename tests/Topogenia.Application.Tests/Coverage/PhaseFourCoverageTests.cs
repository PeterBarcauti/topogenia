// PhaseFourCoverageTests.cs
// Lines of annexes A and B still open at the gate of phase 4, proved through the handlers: translations list, undo of a batch, members, finish (P4-T10).

using Topogenia.Application.Dissolutions;
using Topogenia.Application.Flows;
using Topogenia.Application.Members;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Steps;
using Topogenia.Application.SubMaps;
using Topogenia.Application.Tests.Dissolutions;
using Topogenia.Application.Tests.Support;
using Topogenia.Application.Tests.Users;
using Topogenia.Application.Translations;
using Topogenia.Domain.Dissolutions;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;
using static Topogenia.Application.Tests.Selection.SelectionFixture;

namespace Topogenia.Application.Tests.Coverage;

public sealed class PhaseFourCoverageTests
{
    private static readonly LanguageSettings Languages = Sheets.SheetFixture.Languages;

    private static Task<IReadOnlyList<TranslatableText>> TranslationsAsync(TestProject project) =>
        project.Queries.RunAsync(new GetTranslationsQuery(project.ProjectId, "all", Languages), Token);

    private static DeletionBatch BatchOf(TestProject project, Guid batchId) => project.Database.Get<DeletionBatch>(DeletionBatch.KeyOf(batchId));

    /// <summary>A sub-map of a step of the root map, named as given, without lane (the list reads its name only).</summary>
    private static Guid NamedSubMap(TestProject project, Guid stepId, string name)
    {
        var id = project.NextId();
        project.Database.Add(new ProcessMap(id, project.ProjectId, stepId, name, "fr", TestProject.Now, project.Facilitator.Id, null, null, null, null, 1));
        return id;
    }

    /// <summary>A-2.14-09 (R1283): the list of the workbook leaves out a detailed map that still bears the label of its step.</summary>
    [Fact]
    public async Task The_translations_list_leaves_out_a_detailed_map_named_as_its_step_and_lists_it_once_renamed()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var subMap = NamedSubMap(project, step, "Étape 0");
        var listed = await TranslationsAsync(project);
        Assert.DoesNotContain(listed, text => text.Id == subMap);
        Assert.Contains(listed, text => text.Type == TranslationObject.Step && text.Id == step);

        project.Database.Add(project.Database.Get<ProcessMap>(ProcessMap.KeyOf(subMap)) with { Name = "Archivage" });
        var renamed = Assert.Single(await TranslationsAsync(project), text => text.Id == subMap);
        Assert.Equal((TranslationObject.Map, "Archivage", "PRJ-E001"), (renamed.Type, renamed.Original, renamed.Code));
    }

    /// <summary>A-2.14-10 (R1283): a step of the list carries the context « map / lane » of where it stands.</summary>
    [Fact]
    public async Task A_step_of_the_translations_list_carries_the_name_of_its_map_and_of_its_lane_as_context()
    {
        var project = new TestProject();
        var root = project.Step(0);
        var (subMap, subLane) = project.SubMap(root);
        project.Database.Add(project.Database.Get<Lane>(Lane.KeyOf(subLane)) with { Name = "Archiviste" });
        var inner = project.Step(1, laneId: subLane, mapId: subMap);
        var texts = (await TranslationsAsync(project)).Where(text => text.Type == TranslationObject.Step).ToDictionary(text => text.Id);
        Assert.Equal(("PRJ-E001", "Achats fournisseurs / Acteur 1", "Étape 0"), (texts[root].Code, texts[root].Context, texts[root].Original));
        Assert.Equal(("PRJ-E002", "Détail / Archiviste", "Étape 1"), (texts[inner].Code, texts[inner].Context, texts[inner].Original));
    }

    /// <summary>
    /// A-2.15-05 (R1297, AR-43): the undo of « Delete the detail » after another user deleted the parent step is refused
    /// CARTE_MODIFIEE, gives nothing back and clears the stack (AR-05).
    /// </summary>
    [Fact]
    public async Task The_undo_of_a_deleted_detail_whose_parent_step_was_deleted_since_by_another_user_is_refused_with_map_modified()
    {
        var flow = new DetailedFlow(remainder: false);
        await flow.RunAsync(new DeleteSubMapCommand(flow.SubMap));
        flow.Project.ActingAs(flow.Project.Admin);
        await flow.RunAsync(new DeleteStepCommand(flow.Macro));
        flow.Project.ActingAs(flow.Project.Facilitator);

        var refusal = await Assert.ThrowsAsync<DomainException>(() => flow.RunAsync(new UndoCommand(flow.Project.RootMapId)));
        Assert.Equal((ErrorCodes.MapModified, RefusalKind.Conflict), (refusal.Code, refusal.Kind));
        Assert.False(IsLiveMap(flow.Project, flow.SubMap) || IsLiveStep(flow.Project, flow.Macro));
        Assert.Null(BatchOf(flow.Project, MapOf(flow.Project, flow.SubMap).DeletionBatchId!.Value).RestoredAt);
        Assert.DoesNotContain(flow.Project.Database.Entries, entry => entry.UserId == flow.Project.Facilitator.Id && entry.UndoneAt is null);
    }

    /// <summary>A-2.15-11 (R1300, AR-68): a batch given back whole by the undo of the command is marked restored, by whom and when.</summary>
    [Fact]
    public async Task The_undo_that_gives_back_a_whole_batch_writes_its_restorer_and_the_time_of_the_undo()
    {
        var project = new TestProject();
        var (source, target) = (project.Step(0), project.Step(1));
        Flow(project, source, target);
        await project.Runner.RunAsync(new DeleteStepCommand(target), Token);
        var batchId = StepOf(project, target).DeletionBatchId!.Value;
        Assert.Equal(((DateTime?)null, (Guid?)null), (BatchOf(project, batchId).RestoredAt, BatchOf(project, batchId).RestoredBy));

        project.Clock.UtcNow = TestProject.Now.AddHours(2);
        await UndoAsync(project);
        Assert.True(IsLiveStep(project, target));
        var restored = BatchOf(project, batchId);
        Assert.Equal((TestProject.Now.AddHours(2), project.Facilitator.Id), (restored.RestoredAt, restored.RestoredBy));
        Assert.Equal(TestProject.Now, restored.DeletedAt);
    }

    /// <summary>A-2.15-11 (R1300, AR-78): a batch the undo gives back in part (a flow whose step is gone) stays restorable, unmarked.</summary>
    [Fact]
    public async Task The_undo_that_gives_back_a_batch_in_part_leaves_it_restorable_without_restorer()
    {
        var project = new TestProject();
        var (source, target) = (project.Step(0), project.Step(1));
        var flow = Flow(project, source, target);
        await project.Runner.RunAsync(new DeleteFlowCommand(flow), Token);
        var batchId = project.Database.Get<Flow>(Domain.Model.Flow.KeyOf(flow)).DeletionBatchId!.Value;
        project.ActingAs(project.Admin);
        await project.Runner.RunAsync(new DeleteStepCommand(target), Token);
        project.ActingAs(project.Facilitator);

        project.Clock.UtcNow = TestProject.Now.AddHours(2);
        await UndoAsync(project);
        var kept = BatchOf(project, batchId);
        Assert.Equal(((DateTime?)null, (Guid?)null), (kept.RestoredAt, kept.RestoredBy));
        Assert.Equal(batchId, project.Database.Get<Flow>(Domain.Model.Flow.KeyOf(flow)).DeletionBatchId);
        Assert.Empty(LiveFlows(project));
    }

    /// <summary>A-6-24 (R1562): the replacement reads the known users of the entered logins in one call, for one or fifty members.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    public async Task Replacing_the_members_reads_the_known_users_in_one_call_whatever_their_number(int count)
    {
        var fixture = new UsersFixture();
        var entries = new List<MemberEntry> { new(fixture.Project.Facilitator.Login, "facilitator", true) };
        for (var rank = 1; rank < count; rank++)
        {
            var login = $@"FICTIF\membre{rank:D2}";
            fixture.Database.Add(new AppUser(fixture.Project.NextId(), login, login.ToUpperInvariant(), $"Membre {rank}", null, "fr", false, true, null,
                TestProject.Now, null, null, 1));
            entries.Add(new MemberEntry(login, "contributor", false));
        }
        var reader = new CountingUserReader(fixture.Users);
        var outcome = await fixture.Commands.RunAsync(new ReplaceMembersCommand(fixture.Project.ProjectId, entries,
            new PeopleSource(reader, fixture.Directory, fixture.Administrators)), Token);

        Assert.Equal((1, 0, 0), (reader.ByLogins, reader.ByLogin, reader.ById));
        Assert.Empty(fixture.Directory.Asked);
        Assert.Empty(outcome.Result.InvitedUserIds);
        Assert.Equal(count, outcome.Result.Members.Count);
        Assert.Equal(count, fixture.Database.All<ProjectMember>().Count(member => member.ProjectId == fixture.Project.ProjectId));
    }

    /// <summary>B-2.9-34 (Reliquat.tsx): finishing a frame of several steps stores a document received on the first step of the frame.</summary>
    [Fact]
    public async Task Finishing_a_frame_of_several_steps_stores_a_received_document_on_its_first_step_and_an_output_on_its_last()
    {
        var flow = new DetailedFlow();
        var received = Guid.CreateVersion7();
        flow.Project.Database.Add(new Document(received, flow.Macro, DocumentDirection.Input, "Demande d'achat", null, null, null, false, "fr",
            TestProject.Now, flow.Project.Facilitator.Id, null, null, 1));
        var frame = (await flow.RunAsync(new DissolveCommand(flow.Macro))).Result.DissolutionId;

        var finished = await flow.RunAsync(new FinishDissolutionCommand(frame));
        Document Stored(Guid id) => flow.Project.Database.All<Document>().Single(document => document.Id == id);
        Assert.Equal((flow.First, flow.Second), (Stored(received).StepId, Stored(flow.Document).StepId));
        var arranged = finished.Result.Where(item => item.Kind == RemainderKind.Document).ToDictionary(item => item.ObjectId, item => item.StepId);
        Assert.Equal((flow.First, flow.Second), (arranged[received], arranged[flow.Document]));
        Assert.Null(flow.OpenFrame);
    }
}
