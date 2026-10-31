// CheckConstraintTests.Cases.cs
// For each CHECK constraint, a row that breaks it and nothing else; for each bound, a row exactly on it (spec 4.2, spec 3.0 section 4).

using Topogenia.Application.Assistant;
using Topogenia.Domain.Dissolutions;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Persistence.Assistant;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Model;

public sealed partial class CheckConstraintTests
{
    /// <summary>Rows written after the graph; the last one breaks the constraint named by the key, and only it.</summary>
    private static readonly Dictionary<string, Func<ProjectGraph, object[]>> Violations = new()
    {
        ["CK_app_user_version"] = graph => [OtherUser(graph) with { Version = 0 }],
        ["CK_project_classification"] = graph => [OtherProject(graph) with { Classification = Unknown<Classification>() }],
        ["CK_project_deletion"] = graph => [OtherProject(graph) with { DeletedAt = graph.At(20) }],
        ["CK_project_structure_version"] = graph => [OtherProject(graph) with { StructureVersion = -1 }],
        ["CK_project_version"] = graph => [OtherProject(graph) with { Version = 0 }],
        ["CK_project_workday_hours"] = graph => [OtherProject(graph) with { WorkdayHours = WorkTime.MaxWorkdayHours + 0.01m }],
        ["CK_project_fte_hours_per_month"] = graph => [OtherProject(graph) with { FteHoursPerMonth = 0.99m }],
        ["CK_project_counter_counter"] = graph => [new ProjectCounter(graph.Project.Id, Unknown<CounterKind>(), 1)],
        ["CK_project_counter_last_value"] = graph => InOtherProject(graph, project => new ProjectCounter(project.Id, CounterKind.Step, -1)),
        ["CK_project_member_role"] = graph => OtherMember(graph, member => member with { Role = Unknown<MemberRole>() }),
        ["CK_project_member_version"] = graph => OtherMember(graph, member => member with { Version = 0 }),
        ["CK_deletion_batch_restored"] = graph => [graph.Batch with { Id = Guid.CreateVersion7(), RestoredAt = graph.At(20) }],
        ["CK_process_map_deletion"] = graph => [graph.SubMap with { Id = Guid.CreateVersion7(), ParentStepId = graph.Steps[1].Id, DeletedAt = graph.At(20) }],
        ["CK_process_map_root_alive"] = graph => InOtherProject(graph, project =>
            graph.RootMap with { Id = Guid.CreateVersion7(), ProjectId = project.Id, DeletedAt = graph.At(20), DeletionBatchId = graph.Batch.Id }),
        ["CK_process_map_version"] = graph => [graph.SubMap with { Id = Guid.CreateVersion7(), ParentStepId = graph.Steps[1].Id, Version = 0 }],
        ["CK_lane_lane_type"] = graph => [OtherLane(graph) with { LaneType = Unknown<LaneType>() }],
        ["CK_lane_deletion"] = graph => [OtherLane(graph) with { DeletionBatchId = graph.Batch.Id }],
        ["CK_lane_version"] = graph => [OtherLane(graph) with { Version = 0 }],
        ["CK_lane_headcount"] = graph => [OtherLane(graph) with { LaneType = LaneType.System, Headcount = 1 }],
        ["CK_step_step_type"] = graph => [OtherStep(graph) with { StepType = Unknown<StepType>() }],
        ["CK_step_column_index"] = graph => [OtherStep(graph) with { Column = -1 }],
        ["CK_step_row_index"] = graph => [OtherStep(graph) with { Row = Cell.MaxRow + 1 }],
        ["CK_step_automation_status"] = graph => [OtherStep(graph) with { AutomationStatus = Unknown<AutomationStatus>() }],
        ["CK_step_deletion"] = graph => [OtherStep(graph) with { DeletedAt = graph.At(20) }],
        ["CK_step_version"] = graph => [OtherStep(graph) with { Version = 0 }],
        ["CK_step_volume"] = graph => [OtherStart(graph) with { VolumeQuantity = 5m }],
        ["CK_step_volume_period"] = graph => [OtherStart(graph) with { VolumeQuantity = 5m, VolumePeriod = Unknown<VolumePeriod>() }],
        ["CK_step_volume_quantity"] = graph => [OtherStep(graph) with { VolumeQuantity = 5m, VolumePeriod = VolumePeriod.Month }],
        ["CK_flow_not_reflexive"] = graph => [OtherFlow(graph) with { TargetStepId = graph.Steps[3].Id }],
        ["CK_flow_deletion"] = graph => [OtherFlow(graph) with { DeletedAt = graph.At(20) }],
        ["CK_flow_version"] = graph => [OtherFlow(graph) with { Version = 0 }],
        ["CK_flow_share_percent"] = graph => [OtherFlow(graph) with { SharePercent = CaseShare.Max + 0.01m }],
        ["CK_step_raci_role"] = graph => [OtherRaci(graph) with { Role = Unknown<RaciRole>(7) }],
        ["CK_step_raci_version"] = graph => [OtherRaci(graph) with { Version = 0 }],
        ["CK_dissolution_closed"] = graph => [ClosedDissolution(graph) with { ClosedBy = null }],
        ["CK_dissolution_version"] = graph => [ClosedDissolution(graph) with { Version = 0 }],
        ["CK_dissolution_object_object_type"] = graph => [new DissolutionObject(graph.Dissolution.Id, "step", graph.Steps[0].Id)],
        ["CK_step_sheet_status"] = graph => [OtherSheet(graph) with { Status = Unknown<SheetStatus>() }],
        ["CK_step_sheet_frequency"] = graph => [OtherSheet(graph) with { Frequency = Unknown<Frequency>() }],
        ["CK_step_sheet_duration_minutes"] = graph => [OtherSheet(graph) with { DurationMinutes = Duration.MaxMinutes + 0.01m }],
        ["CK_step_sheet_version"] = graph => [OtherSheet(graph) with { Version = 0 }],
        ["CK_step_sheet_lead_time"] = graph => [OtherSheet(graph) with { LeadTime = 1m }],
        ["CK_step_sheet_lead_time_unit"] = graph => [OtherSheet(graph) with { LeadTime = 1m, LeadTimeUnit = Unknown<LeadTimeUnit>() }],
        ["CK_step_sheet_lead_time_value"] = graph => [OtherSheet(graph) with { LeadTime = LeadTime.MaxValue + 0.01m, LeadTimeUnit = LeadTimeUnit.Day }],
        ["CK_step_operation_position"] = graph => [OtherOperation(graph, 51)],
        ["CK_step_operation_nature"] = graph => [OtherOperation(graph, 2) with { Nature = Unknown<OperationNature>() }],
        ["CK_step_sheet_version_version_number"] = graph => [OtherVersion(graph, 0)],
        ["CK_step_sheet_version_frequency"] = graph => [OtherVersion(graph, 3) with { Frequency = Unknown<Frequency>() }],
        ["CK_step_sheet_version_duration_minutes"] = graph => [OtherVersion(graph, 4) with { DurationMinutes = -0.01m }],
        ["CK_step_sheet_version_lead_time"] = graph => [OtherVersion(graph, 5) with { LeadTime = null, LeadTimeUnit = LeadTimeUnit.Hour }],
        ["CK_step_sheet_version_lead_time_unit"] = graph => [OtherVersion(graph, 6) with { LeadTime = 1m, LeadTimeUnit = Unknown<LeadTimeUnit>() }],
        ["CK_step_sheet_version_lead_time_value"] = graph => [OtherVersion(graph, 7) with { LeadTime = -0.01m, LeadTimeUnit = LeadTimeUnit.Minute }],
        ["CK_document_direction"] = graph => [OtherDocument(graph) with { Direction = Unknown<DocumentDirection>() }],
        ["CK_document_location"] = graph => [OtherDocument(graph) with { LocationType = null }],
        ["CK_document_location_type"] = graph => [OtherDocument(graph) with { LocationType = Unknown<LocationType>() }],
        ["CK_document_wait_hours"] = graph => [OtherDocument(graph) with { WaitHours = 1m }],
        ["CK_document_version"] = graph => [OtherDocument(graph) with { Version = 0 }],
        ["CK_pain_point_status"] = graph => [OtherPainPoint(graph) with { Status = Unknown<PainPointStatus>() }],
        ["CK_pain_point_lost_hours_per_month"] = graph => [OtherPainPoint(graph) with { LostHoursPerMonth = -0.1m }],
        ["CK_pain_point_version"] = graph => [OtherPainPoint(graph) with { Version = 0 }],
        ["CK_pain_point_status_history_status_before"] = graph => [OtherChange(graph) with { StatusBefore = Unknown<PainPointStatus>() }],
        ["CK_pain_point_status_history_status_after"] = graph => [OtherChange(graph) with { StatusAfter = Unknown<PainPointStatus>() }],
        ["CK_document_use_wait_hours"] = graph => [OtherUse(graph) with { WaitHours = -0.01m }],
        ["CK_document_use_percent_complete_accurate"] = graph => [OtherUse(graph) with { PercentCompleteAccurate = 101 }],
        ["CK_document_use_version"] = graph => [OtherUse(graph) with { Version = 0 }],
        ["CK_translation_object_field"] = graph => [OtherTranslation(graph) with { Field = TranslationField.Name }],
        ["CK_translation_source"] = graph => [OtherTranslation(graph) with { Source = Unknown<TranslationSource>() }],
        ["CK_translation_version"] = graph => [OtherTranslation(graph) with { Version = 0 }],
        ["CK_operation_log_sequence"] = graph => [OtherEntry(graph) with { Sequence = -1 }],
        ["CK_assistant_settings_single"] = _ => [Settings with { Id = AssistantSettingsRow.SingleId + 1 }],
        ["CK_assistant_settings_connector"] = _ => [Settings with { Connector = Unknown<AssistantConnector>() }],
        ["CK_assistant_settings_max_output_tokens"] = _ => [Settings with { MaxOutputTokens = AssistantRules.MinOutputTokens - 1 }],
        ["CK_assistant_settings_version"] = _ => [Settings with { Version = 0 }],
    };

    /// <summary>The one row of the settings of the assistant, valid; each case breaks one of its checks, and the row is refused.</summary>
    private static AssistantSettingsRow Settings => new(AssistantSettingsRow.SingleId, AssistantConnector.Anthropic, AssistantRules.DefaultAnthropicModel,
        null, null, AssistantRules.MaxOutputTokens, Codes.Of(Classification.Internal), null, null, 1, null, null);

    /// <summary>Rows exactly on the bounds: accepted on both engines (the decimals of SQLite are compared as numbers).</summary>
    private static readonly Dictionary<string, Func<ProjectGraph, object[]>> Bounds = new()
    {
        ["duration of 0 and of 100 000 minutes"] = graph =>
            [OtherSheet(graph) with { DurationMinutes = Duration.MaxMinutes }, OtherVersion(graph, 3) with { DurationMinutes = 0m }],
        ["durations that a text comparison would refuse"] = graph =>
            [OtherSheet(graph) with { DurationMinutes = 99_999.99m }, OtherVersion(graph, 3) with { DurationMinutes = 2.5m }],
        ["row 0 and row 50"] = graph => [OtherStep(graph) with { Row = Cell.MaxRow }, OtherStep(graph) with { Code = graph.ReadableCode('E', 61), Row = 0 }],
        ["column parked beyond 10 000"] = graph => [OtherStep(graph) with { Column = 1_000_000_000 }],
        ["percent of 0 and of 100"] = graph =>
            [OtherUse(graph) with { PercentCompleteAccurate = 100 }, OtherUse(graph) with { StepId = graph.Steps[5].Id, PercentCompleteAccurate = 0 }],
        ["no waiting time and no lost hours"] = graph =>
            [OtherUse(graph) with { WaitHours = 0m }, OtherPainPoint(graph) with { LostHoursPerMonth = 0m }],
        ["wait of an input"] = graph =>
            [OtherDocument(graph) with { Direction = DocumentDirection.Input, WaitHours = 99_999.99m, Location = null, LocationType = null }],
        ["procedure rows up to 50"] = graph => [OtherOperation(graph, 50), OtherOperation(graph, 2)],
        ["first version of a record"] = graph => [OtherFlow(graph) with { Version = 1 }],
        ["versions and counters at the end of an int"] = graph =>
        {
            var project = OtherProject(graph) with { StructureVersion = int.MaxValue, Version = int.MaxValue };
            return [project, new ProjectCounter(project.Id, CounterKind.Step, int.MaxValue), OtherStep(graph) with { Column = int.MaxValue },
                OtherEntry(graph) with { Sequence = int.MaxValue }];
        },
        ["objects of a dissolution"] = graph => [new DissolutionObject(graph.Dissolution.Id, DissolutionObjectTypes.Lane, graph.Lanes[1].Id)],
        ["work time on its bounds"] = graph =>
            [OtherProject(graph) with { WorkdayHours = 1m, FteHoursPerMonth = WorkTime.MaxFteHoursPerMonth },
                OtherProject(graph) with { WorkdayHours = WorkTime.MaxWorkdayHours, FteHoursPerMonth = 1m }],
        ["headcount of 0 and of 100 000 on lanes of people"] = graph =>
            [OtherLane(graph) with { Headcount = 0 }, OtherLane(graph) with { Code = graph.ReadableCode('C', 61), SortOrder = 6, LaneType = LaneType.Department, Headcount = Headcount.Max }],
        ["volume of 0 and of 1 000 000 on starts"] = graph =>
            [OtherStart(graph) with { VolumeQuantity = 0m, VolumePeriod = VolumePeriod.Day },
                OtherStart(graph) with { Code = graph.ReadableCode('E', 62), Column = 71, VolumeQuantity = IncomingVolume.MaxQuantity, VolumePeriod = VolumePeriod.Year }],
        ["share of 0 and of 100"] = graph =>
            [OtherFlow(graph) with { SharePercent = 0m }, OtherFlow(graph) with { SourceStepId = graph.Steps[4].Id, TargetStepId = graph.Steps[5].Id, SharePercent = CaseShare.Max }],
        ["lead time of 0 and of 100 000"] = graph =>
            [OtherSheet(graph) with { LeadTime = LeadTime.MaxValue, LeadTimeUnit = LeadTimeUnit.Day }, OtherVersion(graph, 3) with { LeadTime = 0m, LeadTimeUnit = LeadTimeUnit.Minute }],
    };

    /// <summary>A new project and one row of it.</summary>
    private static object[] InOtherProject(ProjectGraph graph, Func<Project, object> row)
    {
        var project = OtherProject(graph);
        return [project, row(project)];
    }

    /// <summary>A new user, member of the project, changed by <paramref name="change"/>.</summary>
    private static object[] OtherMember(ProjectGraph graph, Func<ProjectMember, ProjectMember> change)
    {
        var user = OtherUser(graph);
        return [user, change(new ProjectMember(graph.Project.Id, user.Id, MemberRole.Reader, false, false, 5, graph.At(20), graph.Owner.Id, null, null))];
    }
}
