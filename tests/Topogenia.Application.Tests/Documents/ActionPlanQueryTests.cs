// ActionPlanQueryTests.cs
// The action plan read for the panel (ADR 112): open as the map says it, the exact total of the open hours, the use that carries each pain point, the right to fill its plan for each role, the lines of the core, the names through the index, nothing written.

using System.Text.Json;
using Topogenia.Application.Documents;
using Topogenia.Application.Ports;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Documents;
using Topogenia.Domain.Maps;
using Topogenia.Domain.Model;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using static Topogenia.Application.Tests.Documents.ActionPlanFixture;

namespace Topogenia.Application.Tests.Documents;

public sealed class ActionPlanQueryTests
{
    private static readonly PainPointStatus[] Statuses = Enum.GetValues<PainPointStatus>();

    private static async Task<ProjectWorkspace> LoadAsync(TestProject project) =>
        (await new InMemoryWorkspaceStore(project.Database).LoadAsync(project.ProjectId, WorkspaceContent.Documents | WorkspaceContent.Translations, Token))!;

    /// <summary>A line written without its right to fill, to compare what two roles read.</summary>
    private static string Shared(ActionPlanView plan) =>
        JsonSerializer.Serialize(new { lines = plan.Lines.Select(line => line with { CanFill = false }), total = plan.OpenLostHoursPerMonth });

    [Fact]
    public async Task A_line_is_open_as_the_markers_of_the_map_say_it_scored_and_in_progress_open_implemented_adopted_dropped_not()
    {
        var project = new TestProject();
        var (producer, receiver) = (project.Step(0), project.Step(1));
        foreach (var (status, index) in Statuses.Select((status, index) => (status, index)))
            Raise(project, producer, receiver, $"PRJ-I{index + 1:D3}", 1m, status);

        var plan = await PlanAsync(project.ActingAs(project.Reader));

        Assert.Equal(Statuses, plan.Lines.Select(line => line.Status));
        Assert.Equal([true, true, true, true, false, false, false], plan.Lines.Select(line => line.Open));
        var points = project.Database.All<PainPoint>().ToDictionary(point => point.Id);
        Assert.All(plan.Lines, line => Assert.Equal(MapMarkers.IsOpen(points[line.PainPointId]), line.Open));
    }

    [Fact]
    public async Task The_total_adds_the_hours_of_the_open_lines_only_exactly()
    {
        var project = new TestProject();
        var (producer, receiver) = (project.Step(0), project.Step(1));
        Assert.Equal(0m, (await PlanAsync(project)).OpenLostHoursPerMonth);
        Raise(project, producer, receiver, "PRJ-I001", 0.1m);
        Raise(project, producer, receiver, "PRJ-I002", 0.2m, PainPointStatus.InProgress);
        Raise(project, producer, receiver, "PRJ-I003", null, PainPointStatus.Scored);
        Raise(project, producer, receiver, "PRJ-I004", 5m, PainPointStatus.Implemented);
        Raise(project, producer, receiver, "PRJ-I005", 7m, PainPointStatus.Adopted);
        Raise(project, producer, receiver, "PRJ-I006", 11m, PainPointStatus.Dropped);

        var plan = await PlanAsync(project.ActingAs(project.Reader));

        Assert.Equal(6, plan.Lines.Count);
        Assert.Equal(0.3m, plan.OpenLostHoursPerMonth);
        Assert.Equal("0.3", JsonSerializer.Serialize(plan.OpenLostHoursPerMonth));
    }

    [Fact]
    public async Task A_line_names_the_step_that_receives_the_document_and_the_document()
    {
        var project = new TestProject();
        var (producer, receiver, gone) = (project.Step(0), project.Step(1), project.Step(2));
        var document = (await project.Runner.RunAsync(new AddDocumentCommand(producer, new DocumentInput("output", "Facture", null, null, false)), Token)).Result;
        await project.Runner.RunAsync(new UseDocumentCommand(receiver, document, new DocumentUseInput(1m, null, "Illisible", 2m)), Token);
        Alone(project, producer, "PRJ-I050");
        Raise(project, producer, gone, "PRJ-I060");
        DeleteStep(project, gone);

        var lines = ByCode(await PlanAsync(project.ActingAs(project.Facilitator)));

        Assert.Equal(["PRJ-I001", "PRJ-I050", "PRJ-I060"], lines.Keys.Order(StringComparer.Ordinal));
        var used = lines["PRJ-I001"];
        Assert.Equal((producer, (Guid?)receiver, (Guid?)document, true, (Guid?)receiver), (used.StepId, used.ReceivingStepId, used.DocumentId, used.CanFill, used.StatusStepId));
        // A pain point without its use (a package written by hand), or whose receiving step is gone: no sheet to write its
        // plan in; its status is chosen on the sheet of its own step (R10).
        foreach (var code in new[] { "PRJ-I050", "PRJ-I060" })
        {
            Assert.Equal(((Guid?)null, (Guid?)null, (Guid?)null), (lines[code].ReceivingStepId, lines[code].ReceivingMapId, lines[code].DocumentId));
            Assert.Equal((producer, true), (lines[code].StatusStepId, lines[code].CanFill));
        }
    }

    /// <summary>
    /// R10: a pain point no live use carries takes its status on the sheet of its own step, under the right to fill that
    /// sheet: the contributor holds the first lane only; a step without content gives no right; a pain point a use carries
    /// keeps the receiving step.
    /// </summary>
    [Fact]
    public async Task A_line_no_live_use_carries_takes_its_status_on_the_sheet_of_its_own_step_under_the_right_to_fill_it()
    {
        var project = new TestProject();
        Sheets.SheetFixture.HoldLane(project, project.Contributor.Id);
        var other = AddLane(project, project.RootMapId, "Comptabilité");
        var (held, free, gone, start) = (project.Step(0), project.Step(1, laneId: other), project.Step(2), project.Step(3));
        Alone(project, held, "PRJ-I050");
        Alone(project, free, "PRJ-I051");
        Alone(project, start, "PRJ-I052");
        Raise(project, held, free, "PRJ-I001");
        Raise(project, free, gone, "PRJ-I060");
        DeleteStep(project, gone);
        Retype(project, start, StepType.Start);

        var contributor = ByCode(await PlanAsync(project.ActingAs(project.Contributor)));
        var facilitator = ByCode(await PlanAsync(project.ActingAs(project.Facilitator)));

        string[] codes = ["PRJ-I001", "PRJ-I050", "PRJ-I051", "PRJ-I052", "PRJ-I060"];
        Assert.Equal([free, held, free, start, free], codes.Select(code => contributor[code].StatusStepId));
        Assert.Equal([false, true, false, false, false], codes.Select(code => contributor[code].CanFill));
        Assert.Equal([true, true, true, false, true], codes.Select(code => facilitator[code].CanFill));
        Assert.Equal([(Guid?)free, null, null, null, null], codes.Select(code => facilitator[code].ReceivingStepId));
    }

    [Fact]
    public async Task A_line_names_the_map_of_the_receiving_step_which_differs_from_the_map_of_the_producer_when_a_step_was_moved()
    {
        var project = new TestProject();
        var (producer, receiver, parent, lost) = (project.Step(0), project.Step(1), project.Step(2), project.Step(3));
        var (detail, detailLane) = project.SubMap(parent);
        Raise(project, producer, receiver, "PRJ-I001");
        Raise(project, producer, parent, "PRJ-I002");
        Alone(project, producer, "PRJ-I003");
        Raise(project, producer, lost, "PRJ-I004");
        project.Database.Add(project.Database.Get<Step>(Step.KeyOf(receiver)) with { MapId = detail, LaneId = detailLane });
        DeleteStep(project, lost);

        var lines = ByCode(await PlanAsync(project.ActingAs(project.Facilitator)));

        Assert.Equal(
            [(Guid?)detail, project.RootMapId, null, null],
            [lines["PRJ-I001"].ReceivingMapId, lines["PRJ-I002"].ReceivingMapId, lines["PRJ-I003"].ReceivingMapId, lines["PRJ-I004"].ReceivingMapId]);
        Assert.All(lines.Values, line => Assert.Equal(project.RootMapId, line.MapId));
    }

    [Fact]
    public async Task The_facilitator_and_the_administrator_may_fill_every_receiving_sheet_with_content()
    {
        var project = new TestProject();
        var other = AddLane(project, project.RootMapId, "Comptabilité");
        var (first, second) = (project.Step(0), project.Step(1, laneId: other));
        Raise(project, first, second, "PRJ-I001");
        Raise(project, second, first, "PRJ-I002", status: PainPointStatus.Dropped);
        foreach (var user in new[] { project.Facilitator, project.Admin })
        {
            var lines = ByCode(await PlanAsync(project.ActingAs(user)));
            Assert.Equal([true, true], lines.Values.Select(line => line.CanFill));
            Assert.Equal(((Guid?)second, (Guid?)first), (lines["PRJ-I001"].ReceivingStepId, lines["PRJ-I002"].ReceivingStepId));
        }
    }

    /// <summary>
    /// The contributor holds the first lane and is given one step of the other lane: the right to fill a plan is the one of
    /// the receiving sheet, never the one of the producing step where the pain point is raised.
    /// </summary>
    [Fact]
    public async Task A_contributor_may_fill_the_plan_of_the_steps_he_is_given_and_of_the_lane_he_holds_on_the_receiving_side_only()
    {
        var project = new TestProject();
        Sheets.SheetFixture.HoldLane(project, project.Contributor.Id);
        var other = AddLane(project, project.RootMapId, "Comptabilité");
        var held = project.Step(0);
        var free = project.Step(1, laneId: other);
        var given = project.Step(2, assignee: project.Contributor.Id, laneId: other);
        var alsoFree = project.Step(3, laneId: other);
        Raise(project, free, held, "PRJ-I001");
        Raise(project, held, free, "PRJ-I002");
        Raise(project, alsoFree, given, "PRJ-I003");
        Raise(project, given, alsoFree, "PRJ-I004");

        var lines = ByCode(await PlanAsync(project.ActingAs(project.Contributor)));

        Assert.Equal([("PRJ-I001", true), ("PRJ-I002", false), ("PRJ-I003", true), ("PRJ-I004", false)],
            lines.Values.OrderBy(line => line.Code, StringComparer.Ordinal).Select(line => (line.Code, line.CanFill)));
    }

    [Fact]
    public async Task A_reader_reads_every_line_the_facilitator_reads_and_may_fill_none()
    {
        var project = new TestProject();
        var (producer, receiver) = (project.Step(0), project.Step(1));
        var (deep, deepLane) = project.SubMap(producer);
        var (deepProducer, deepReceiver) = (project.Step(0, laneId: deepLane, mapId: deep), project.Step(1, laneId: deepLane, mapId: deep));
        Raise(project, producer, receiver, "PRJ-I001", 1.5m);
        Raise(project, deepProducer, deepReceiver, "PRJ-I002", 2m, PainPointStatus.InProgress);
        Raise(project, receiver, producer, "PRJ-I003", 4m, PainPointStatus.Implemented);

        var facilitator = await PlanAsync(project.ActingAs(project.Facilitator));
        var reader = await PlanAsync(project.ActingAs(project.Reader));

        Assert.Equal(3, reader.Lines.Count);
        Assert.Equal([true, true, true], facilitator.Lines.Select(line => line.CanFill));
        Assert.Equal([false, false, false], reader.Lines.Select(line => line.CanFill));
        Assert.Equal(Shared(facilitator), Shared(reader));
        Assert.Equal(3.5m, reader.OpenLostHoursPerMonth);
    }

    [Fact]
    public async Task A_receiving_step_without_content_gives_no_right_to_fill()
    {
        var project = new TestProject();
        var producer = project.Step(0);
        var types = new[] { StepType.Start, StepType.End, StepType.Parallel, StepType.Inclusive, StepType.Decision, StepType.Wait };
        var receivers = types.Select((_, index) => project.Step(index + 1)).ToList();
        foreach (var (receiver, index) in receivers.Select((receiver, index) => (receiver, index)))
        {
            Raise(project, producer, receiver, $"PRJ-I{index + 1:D3}");
            Retype(project, receiver, types[index]);
        }

        var lines = (await PlanAsync(project.ActingAs(project.Facilitator))).Lines.OrderBy(line => line.Code, StringComparer.Ordinal).ToList();

        Assert.Equal([false, false, false, false, true, true], lines.Select(line => line.CanFill));
        Assert.Equal(receivers.Select(id => (Guid?)id), lines.Select(line => line.ReceivingStepId));
    }

    /// <summary>Fifty projects drawn from a fixed seed, for each role: the lines are the entries of the plan of the core, in its order, with the pain point as it is.</summary>
    [Fact]
    public async Task The_lines_are_those_of_the_plan_of_the_core_in_its_order_for_every_role()
    {
        var random = new Random(112);
        var (lines, dealtWith, fillable) = (0, 0, 0);
        for (var round = 0; round < 50; round++)
        {
            var project = Draw(random);
            var workspace = await LoadAsync(project);
            var expected = ActionPlan.Of(workspace, project.RootMapId)
                .Select(entry => (Point: entry.PainPoint, StepId: entry.Step.Id, StepCode: entry.Step.Code, MapId: entry.Map.Id, LaneId: entry.Lane?.Id)).ToList();
            foreach (var user in new[] { project.Facilitator, project.Contributor, project.Reader, project.Admin })
            {
                var plan = await PlanAsync(project.ActingAs(user));
                Assert.Equal(expected.Select(entry => (entry.Point.Id, entry.StepId, entry.StepCode, entry.MapId)),
                    plan.Lines.Select(line => (line.PainPointId, line.StepId, line.StepCode, line.MapId)));
                Assert.Equal(expected.Select(entry => entry.Point).Select(point =>
                        (point.Code, point.Description, point.Status, point.LostHoursPerMonth, point.Action, point.Owner, point.DueDate)),
                    plan.Lines.Select(line => (line.Code, line.Description, line.Status, line.LostHoursPerMonth, line.Action, line.Owner, line.DueDate)));
                Assert.Equal(expected.Select(entry => entry.LaneId is not null), plan.Lines.Select(line => line.Lane is not null));
                fillable += plan.Lines.Count(line => line.CanFill);
            }
            lines += expected.Count;
            dealtWith += expected.Count(entry => !MapMarkers.IsOpen(entry.Point));
        }
        // The property is not empty: lines on every round or nearly, some dealt with, some that may be filled.
        Assert.InRange(lines, 100, int.MaxValue);
        Assert.InRange(dealtWith, 20, int.MaxValue);
        Assert.InRange(fillable, 50, int.MaxValue);
    }

    /// <summary>
    /// Fifty projects drawn from a fixed seed, with translations in three languages and decoys (another field, another type
    /// of object, an object that does not exist): the names of the map, the step and the lane of each line are those that
    /// the rule of the translations gives, read one object at a time.
    /// </summary>
    [Fact]
    public async Task The_names_read_through_the_index_are_those_read_one_object_at_a_time()
    {
        var random = new Random(1120);
        var (translated, decoys) = (0, 0);
        for (var round = 0; round < 50; round++)
        {
            var project = Draw(random);
            var workspace = await LoadAsync(project);
            NamedText Named(TranslationObject type, Guid id, string text, string language) =>
                new(text, language, TranslationRules.Of(workspace, type, id).ToDictionary(pair => pair.Key, pair => pair.Value.Text, StringComparer.Ordinal));
            var expected = ActionPlan.Of(workspace, project.RootMapId).Select(entry => new
            {
                Map = Named(TranslationObject.Map, entry.Map.Id, entry.Map.Name, entry.Map.SourceLanguage),
                Step = Named(TranslationObject.Step, entry.Step.Id, entry.Step.Label, entry.Step.SourceLanguage),
                Lane = entry.Lane is null ? null : Named(TranslationObject.Lane, entry.Lane.Id, entry.Lane.Name, entry.Lane.SourceLanguage),
            }).ToList();

            var plan = await PlanAsync(project.ActingAs(project.Reader));

            Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(plan.Lines.Select(line => new { line.Map, line.Step, line.Lane })));
            translated += expected.Sum(names => names.Map.Translations.Count + names.Step.Translations.Count + (names.Lane?.Translations.Count ?? 0));
            decoys += workspace.All<Translation>().Count(translation => translation.Text.StartsWith("Leurre", StringComparison.Ordinal));
        }
        // The equivalence is not empty: many names are translated, and the decoys are many.
        Assert.InRange(translated, 200, int.MaxValue);
        Assert.InRange(decoys, 60, int.MaxValue);
    }

    /// <summary>
    /// ADR 116: a line carries the version of its pain point (the If-Match of a change of its status), never the one of the
    /// use that carries it, and the instant of the last record of its history; null for a pain point without history.
    /// </summary>
    [Fact]
    public async Task A_line_carries_the_version_of_its_pain_point_and_the_instant_of_its_last_status_change()
    {
        var project = new TestProject();
        var (producer, receiver) = (project.Step(0), project.Step(1));
        var document = (await project.Runner.RunAsync(new AddDocumentCommand(producer, new DocumentInput("output", "Facture", null, null, false)), Token)).Result;
        var raised = (await project.Runner.RunAsync(new UseDocumentCommand(receiver, document, new DocumentUseInput(1m, null, "Illisible", 2m)), Token)).Result;
        var (hand, handDocument) = Raise(project, producer, receiver, "PRJ-I050", status: PainPointStatus.Scored);
        project.Database.Add(project.Database.Get<DocumentUse>(DocumentUse.KeyOf(handDocument, receiver)) with { Version = 7 });

        var before = ByCode(await PlanAsync(project.ActingAs(project.Reader)));
        Assert.Equal((1, (DateTime?)TestProject.Now), (before["PRJ-I001"].Version, before["PRJ-I001"].StatusChangedAt));
        Assert.Equal((1, (DateTime?)null), (before["PRJ-I050"].Version, before["PRJ-I050"].StatusChangedAt));

        project.Clock.UtcNow = TestProject.Now.AddDays(2);
        await project.ActingAs(project.Facilitator).Runner.RunAsync(new ChangePainPointStatusCommand(receiver, raised.PainPointId!.Value, "in_progress"), Token);
        project.Clock.UtcNow = TestProject.Now.AddDays(5);
        await project.Runner.RunAsync(new ChangePainPointStatusCommand(receiver, hand, "implemented"), Token);

        var after = ByCode(await PlanAsync(project.ActingAs(project.Reader)));
        Assert.Equal((2, (DateTime?)TestProject.Now.AddDays(2)), (after["PRJ-I001"].Version, after["PRJ-I001"].StatusChangedAt));
        Assert.Equal((2, (DateTime?)TestProject.Now.AddDays(5)), (after["PRJ-I050"].Version, after["PRJ-I050"].StatusChangedAt));
        var points = project.Database.All<PainPoint>().ToDictionary(point => point.Code);
        Assert.All(after.Values, line => Assert.Equal(points[line.Code].Version, line.Version));
    }

    /// <summary>ADR 116: a status chosen in the application closes or opens the line as the map says it, and the total follows exactly.</summary>
    [Fact]
    public async Task A_line_dealt_with_in_the_application_is_no_longer_open_and_leaves_the_total()
    {
        var project = new TestProject();
        var (producer, receiver) = (project.Step(0), project.Step(1));
        var (first, _) = Raise(project, producer, receiver, "PRJ-I001", 1.5m);
        var (second, _) = Raise(project, producer, receiver, "PRJ-I002", 2m, PainPointStatus.InProgress);
        Assert.Equal(3.5m, (await PlanAsync(project)).OpenLostHoursPerMonth);

        foreach (var (status, open, total) in new[] { ("implemented", false, 2m), ("adopted", false, 2m), ("dropped", false, 2m), ("reported", true, 3.5m), ("scored", true, 3.5m) })
        {
            await project.ActingAs(project.Facilitator).Runner.RunAsync(new ChangePainPointStatusCommand(receiver, first, status), Token);
            var plan = await PlanAsync(project.ActingAs(project.Reader));
            Assert.Equal((Codes.Parse<PainPointStatus>(status, "status"), open), (ByCode(plan)["PRJ-I001"].Status, ByCode(plan)["PRJ-I001"].Open));
            Assert.Equal(total, plan.OpenLostHoursPerMonth);
        }
        await project.ActingAs(project.Facilitator).Runner.RunAsync(new ChangePainPointStatusCommand(receiver, second, "implemented"), Token);
        await project.Runner.RunAsync(new ChangePainPointStatusCommand(receiver, first, "adopted"), Token);
        var closed = await PlanAsync(project.ActingAs(project.Reader));
        Assert.Equal((0m, 2), (closed.OpenLostHoursPerMonth, closed.Lines.Count));
        Assert.All(closed.Lines, line => Assert.False(line.Open));
    }

    [Fact]
    public async Task The_plan_loads_once_writes_nothing_and_logs_nothing()
    {
        var project = new TestProject();
        var (producer, receiver) = (project.Step(0), project.Step(1));
        Raise(project, producer, receiver, "PRJ-I001", 1m);
        var (calls, commits, entries, records) = (project.Database.Calls.Count, project.Database.Commits, project.Database.Entries.Count,
            project.Database.All<object>().Count());

        await PlanAsync(project.ActingAs(project.Contributor));

        Assert.Equal(["access", "load"], project.Database.Calls.Skip(calls));
        Assert.Equal((commits, entries, records), (project.Database.Commits, project.Database.Entries.Count, project.Database.All<object>().Count()));
        Assert.Equal(WorkspaceContent.Documents | WorkspaceContent.Translations, project.Database.LoadedContents[^1]);
        Assert.False(new GetActionPlanQuery(project.RootMapId).ReadsMapView);
    }
}
