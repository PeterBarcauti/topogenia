// MapViewBuilderTests.cs
// The view of a map built from a workspace: order, paths, breadcrumb, rights, warnings, RACI, texts, the objects named by the check of the value stream (annex A 2.4, 2.5, ADR 085).

using Topogenia.Application.Commands;
using Topogenia.Application.Maps;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Tests.Maps;

public sealed class MapViewBuilderTests
{
    private static readonly MapViewBuilder Builder = new(AppSettings.Default);

    private static ProjectWorkspace Workspace(TestProject project) =>
        new(project.ProjectId, project.Database.All<IRecord>().Where(record => record is not AppUser));

    private static MapView View(TestProject project, Guid? mapId = null, MemberRole role = MemberRole.Facilitator) =>
        Builder.Build(Workspace(project), mapId ?? project.RootMapId, new MapViewer(project.Facilitator.Id, role));

    private static Guid SecondLane(TestProject project, string name = "Comptabilité")
    {
        var id = project.NextId();
        project.Database.Add(new Lane(id, project.RootMapId, "PRJ-C02", name, "fr", LaneType.Department, null, "#FCE4EC", 1, null,
            TestProject.Now, project.Facilitator.Id, null, null, null, null, 1));
        return id;
    }

    private static Guid AddFlow(TestProject project, Guid source, Guid target)
    {
        var id = project.NextId();
        project.Database.Add(new Flow(id, project.RootMapId, source, target, null, TestProject.Now, project.Facilitator.Id, null, null, null, null, 1));
        return id;
    }

    [Fact]
    public void The_steps_come_in_reading_order_with_their_path_and_the_lanes_in_their_order()
    {
        var project = new TestProject();
        var second = SecondLane(project);
        var late = project.Step(3);
        var early = project.Step(1, laneId: second);
        var first = project.Step(1);
        AddFlow(project, first, early);
        AddFlow(project, early, late);
        var view = View(project);
        Assert.Equal([project.LaneId, second], view.Lanes.Select(lane => lane.Id));
        Assert.Equal([(first, "1"), (early, "2"), (late, "3")], view.Steps.Select(step => (step.Id, step.Path)));
        Assert.Equal(2, view.Flows.Count);
        Assert.Empty(view.Warnings);
    }

    [Fact]
    public void A_sub_map_has_its_breadcrumb_from_the_root_its_depth_and_the_path_of_its_parent()
    {
        var project = new TestProject();
        var parent = project.Step(0);
        var (subMap, subLane) = project.SubMap(parent);
        project.Step(0, laneId: subLane, mapId: subMap);
        var view = View(project, subMap);
        Assert.Equal([project.RootMapId, subMap], view.Breadcrumb.Select(item => item.MapId));
        Assert.Equal(2, view.Depth);
        Assert.Equal(parent, view.ParentStepId);
        Assert.Equal("1.1", view.Steps.Single().Path);
        var root = View(project);
        Assert.Equal((subMap, false), (root.Steps.Single().SubMapId, root.Steps.Single().SubMapEmpty));
    }

    [Fact]
    public void Each_map_of_the_breadcrumb_gives_the_language_its_name_was_entered_in()
    {
        var project = new TestProject();
        var (subMap, subLane) = project.SubMap(project.Step(0), language: "en");
        project.Step(0, laneId: subLane, mapId: subMap);
        var view = View(project, subMap);
        Assert.Equal([(project.RootMapId, "fr"), (subMap, "en")], view.Breadcrumb.Select(item => (item.MapId, item.SourceLanguage)));
    }

    /// <summary>
    /// Each object the completeness check of the value stream names is in the view with its code and label (spec 14.4), a
    /// step and a lane of a detailed map included; a detailed map is named by the step it details, the root map by the
    /// project. The detailed map here has two starts without volume (VOLUME_DEBUTS_MULTIPLES).
    /// </summary>
    [Fact]
    public void Each_object_named_by_the_check_of_the_value_stream_has_its_code_and_label_a_detailed_map_by_its_step()
    {
        var project = new TestProject();
        var parent = project.Step(0);
        var (subMap, subLane) = project.SubMap(parent);
        var detail = project.Step(1, laneId: subLane, mapId: subMap);
        foreach (var row in new[] { 1, 2 })
            project.Database.Add(new Step(project.NextId(), subMap, subLane, $"PRJ-E01{row}", $"Début {row}", "fr", StepType.Start, 0, row,
                null, AutomationStatus.Manual, null, TestProject.Now, project.Facilitator.Id, null, null, null, null, 1));
        var view = View(project);

        Assert.All(view.ValueStream.Issues, issue => Assert.True(view.StreamSubjects.ContainsKey(issue.StepId ?? issue.LaneId ?? issue.MapId)));
        Assert.Contains(view.ValueStream.Issues, issue => issue.Code == "VOLUME_DEBUTS_MULTIPLES" && issue.MapId == subMap);
        Assert.Equal(new StreamSubjectView("PRJ-E002", "Étape 1", "fr", subMap), view.StreamSubjects[detail]);
        Assert.Equal(new StreamSubjectView("PRJ-C99", "Acteur 1", "fr", subMap), view.StreamSubjects[subLane]);
        Assert.Equal(new StreamSubjectView("PRJ-E001", "Étape 0", "fr", subMap), view.StreamSubjects[subMap]);
        Assert.Equal(new StreamSubjectView("PRJ", "Achats fournisseurs", "fr", project.RootMapId), view.StreamSubjects[project.RootMapId]);
    }

    [Fact]
    public void A_step_without_sub_map_has_no_empty_sub_map_and_a_sub_map_without_a_step_is_empty_as_in_the_1x()
    {
        var project = new TestProject();
        var alone = project.Step(0);
        var detailed = project.Step(1);
        var (subMap, _) = project.SubMap(detailed);
        var steps = View(project).Steps;
        Assert.Equal(((Guid?)null, false), (steps.Single(step => step.Id == alone).SubMapId, steps.Single(step => step.Id == alone).SubMapEmpty));
        Assert.Equal(((Guid?)subMap, true), (steps.Single(step => step.Id == detailed).SubMapId, steps.Single(step => step.Id == detailed).SubMapEmpty));
    }

    [Fact]
    public void Only_a_facilitator_has_the_right_to_change_the_structure()
    {
        var project = new TestProject();
        Assert.Equal(new MapRightsView(true, MemberRole.Facilitator), View(project).Rights);
        Assert.Equal(new MapRightsView(false, MemberRole.Contributor), View(project, role: MemberRole.Contributor).Rights);
        Assert.Equal(new MapRightsView(false, MemberRole.Reader), View(project, role: MemberRole.Reader).Rights);
    }

    [Fact]
    public void The_layout_warnings_of_the_map_are_in_the_view()
    {
        var project = new TestProject();
        var first = project.Step(0);
        var second = project.Step(1);
        Assert.Equal([(first, WarningCodes.IsolatedStep), (second, WarningCodes.IsolatedStep)],
            View(project).Warnings.Select(warning => (warning.StepId, warning.Code)));
    }

    [Fact]
    public void The_visible_raci_lists_the_roles_on_other_lanes_and_the_letters_of_the_settings()
    {
        var project = new TestProject();
        var second = SecondLane(project);
        var step = project.Step(0);
        project.Database.Add(new StepRaci(step, second, RaciRole.C, TestProject.Now, project.Facilitator.Id));
        var view = View(project);
        Assert.Equal([new RaciView(step, second, RaciRole.C)], view.Raci);
        Assert.Equal(AppSettings.Default.RaciLetters, view.RaciLetters);
    }

    [Fact]
    public void A_deleted_lane_step_or_flow_is_not_in_the_view()
    {
        var project = new TestProject();
        var second = SecondLane(project);
        project.Database.Add(project.Database.Get<Lane>(Lane.KeyOf(second)) with { DeletedAt = TestProject.Now });
        var kept = project.Step(0);
        var deleted = project.Step(1);
        var flow = AddFlow(project, kept, deleted);
        project.Database.Add(project.Database.Get<Step>(Step.KeyOf(deleted)) with { DeletedAt = TestProject.Now });
        project.Database.Add(project.Database.Get<Flow>(Flow.KeyOf(flow)) with { DeletedAt = TestProject.Now });
        var view = View(project);
        Assert.Equal([project.LaneId], view.Lanes.Select(lane => lane.Id));
        Assert.Equal([kept], view.Steps.Select(step => step.Id));
        Assert.Empty(view.Flows);
    }

    [Fact]
    public void The_translations_of_the_texts_are_in_the_view()
    {
        var project = new TestProject();
        project.Database.Add(new Translation(TranslationObject.Lane, project.LaneId, TranslationField.Name, "en", "Actor 1",
            TranslationSource.Auto, TestProject.Now, null));
        Assert.Equal("Actor 1", View(project).Lanes.Single().Translations["en"]);
    }

    [Fact]
    public void A_map_that_is_not_live_has_no_view()
    {
        var project = new TestProject();
        var map = project.Database.Get<ProcessMap>(ProcessMap.KeyOf(project.RootMapId));
        project.Database.Add(map with { DeletedAt = TestProject.Now });
        Assert.Equal(ErrorCodes.NotFound, Assert.Throws<DomainException>(() => View(project)).Code);
    }

    [Fact]
    public void The_view_carries_the_versions_and_the_structure_version_of_the_project()
    {
        var project = new TestProject();
        project.Database.Add(project.Project with { StructureVersion = 9 });
        var view = View(project);
        Assert.Equal((1, 9, "PRJ"), (view.Version, view.StructureVersion, view.ProjectCode));
        Assert.Null(view.Dissolution);
    }
}
