// ModelGuards.cs
// The guards kept in the database (spec 4.2): filters of the partial unique indexes and CHECK constraints, one list for both providers.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Topogenia.Domain.Model;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using static Topogenia.Persistence.Model.Guards;

namespace Topogenia.Persistence.Model;

/// <summary>
/// The structural guards of spec 4.2, described once without SQL; each provider renders them in its syntax and adds them
/// to the model (ADR 025), so both databases refuse the same rows. Closed lists come from the codes of the domain
/// (<see cref="Codes"/>); bounds from its constants. The format checks of the 1.x (`LIKE '[A-Z]...'`, `ISJSON`) are
/// not kept: the domain owns them (spec 4.2, A-5-25).
/// </summary>
public static class ModelGuards
{
    public static IReadOnlyList<PartialIndexGuard> PartialIndexes { get; } =
    [
        new(typeof(Step), IndexNames.LiveStepOfCell, new IsNull(nameof(Step.DeletedAt))),
        new(typeof(Flow), IndexNames.LiveFlowOfPair, new IsNull(nameof(Flow.DeletedAt))),
        new(typeof(ProcessMap), IndexNames.LiveSubMapOfStep, All(new IsNotNull(nameof(ProcessMap.ParentStepId)), new IsNull(nameof(ProcessMap.DeletedAt)))),
        new(typeof(ProcessMap), IndexNames.RootMapOfProject, new IsNull(nameof(ProcessMap.ParentStepId))),
        new(typeof(StepRaci), IndexNames.SingleApproverOfStep, new EqualsCode(nameof(StepRaci.Role), Codes.Of(RaciRole.A))),
        new(typeof(Dissolution), IndexNames.OpenDissolutionOfMap, new IsNull(nameof(Dissolution.ClosedAt))),
        new(typeof(DocumentUse), IndexNames.PainPointOfUse, new IsNotNull(nameof(DocumentUse.PainPointId))),
    ];

    public static IReadOnlyList<CheckGuard> Checks { get; } =
    [
        .. ProjectChecks(),
        .. StructureChecks(),
        .. SheetChecks(),
        .. JournalChecks(),
        .. VersionChecks(),
        .. AssistantChecks(),
        Check<Application.Presentation.PresentationComment>(TableNames.PresentationComment, "version",
            Counted(nameof(Application.Presentation.PresentationComment.Version), 1)),
        ClosedList<Application.Presentation.PresentationComment, Application.Presentation.CommentSource>(TableNames.PresentationComment,
            nameof(Application.Presentation.PresentationComment.Source)),
        // The comments of the flows (ADR 099, lot J10): the guards of the comments of the steps.
        Check<Application.Presentation.FlowPresentationComment>(TableNames.FlowPresentationComment, "version",
            Counted(nameof(Application.Presentation.FlowPresentationComment.Version), 1)),
        ClosedList<Application.Presentation.FlowPresentationComment, Application.Presentation.CommentSource>(TableNames.FlowPresentationComment,
            nameof(Application.Presentation.FlowPresentationComment.Source)),
        // The voices of the projects (ADR 103): a version that starts at 1, as every line kept beside the closed core.
        Check<Application.Presentation.PresentationVoice>(TableNames.PresentationVoice, "version",
            Counted(nameof(Application.Presentation.PresentationVoice.Version), 1)),
    ];

    private static IEnumerable<CheckGuard> ProjectChecks()
    {
        yield return ClosedList<Project, Classification>(TableNames.Project, nameof(Project.Classification));
        yield return Check<Project>(TableNames.Project, "deletion", Together(nameof(Project.DeletedAt), nameof(Project.DeletedBy)));
        yield return Check<Project>(TableNames.Project, "structure_version", Counted(nameof(Project.StructureVersion), 0));
        yield return Check<Project>(TableNames.Project, "workday_hours", new Between(nameof(Project.WorkdayHours), 1, WorkTime.MaxWorkdayHours));
        yield return Check<Project>(TableNames.Project, "fte_hours_per_month", new Between(nameof(Project.FteHoursPerMonth), 1, WorkTime.MaxFteHoursPerMonth));
        yield return ClosedList<ProjectCounter, CounterKind>(TableNames.ProjectCounter, nameof(ProjectCounter.Counter));
        yield return Check<ProjectCounter>(TableNames.ProjectCounter, "last_value", Counted(nameof(ProjectCounter.LastValue), 0));
        yield return ClosedList<ProjectMember, MemberRole>(TableNames.ProjectMember, nameof(ProjectMember.Role));
        yield return Check<DeletionBatch>(TableNames.DeletionBatch, "restored", Together(nameof(DeletionBatch.RestoredAt), nameof(DeletionBatch.RestoredBy)));
    }

    private static IEnumerable<CheckGuard> StructureChecks()
    {
        yield return Deletion<ProcessMap>(TableNames.ProcessMap);
        // The root map of a project is never deleted.
        yield return Check<ProcessMap>(TableNames.ProcessMap, "root_alive",
            Any(new IsNotNull(nameof(ProcessMap.ParentStepId)), new IsNull(nameof(ProcessMap.DeletedAt))));
        yield return ClosedList<Lane, LaneType>(TableNames.Lane, nameof(Lane.LaneType));
        // A headcount only on a lane of people, within bounds (spec 14.2, S4).
        yield return Check<Lane>(TableNames.Lane, "headcount", Any(new IsNull(nameof(Lane.Headcount)),
            All(new Between(nameof(Lane.Headcount), 0, Headcount.Max), OneOf(nameof(Lane.LaneType), LaneType.Person, LaneType.Department))));
        yield return Deletion<Lane>(TableNames.Lane);
        yield return ClosedList<Step, StepType>(TableNames.Step, nameof(Step.StepType));
        // Columns 0 to 10 000 for the domain; beyond, the temporary parking of a move inside a transaction (ADR 016).
        yield return Check<Step>(TableNames.Step, "column_index", Counted(nameof(Step.Column), 0));
        yield return Check<Step>(TableNames.Step, "row_index", new Between(nameof(Step.Row), 0, Cell.MaxRow));
        yield return ClosedList<Step, AutomationStatus>(TableNames.Step, nameof(Step.AutomationStatus));
        // The value class (Makigami M1): a code of its list, only on a step with content.
        yield return ClosedList<Step, ValueClass>(TableNames.Step, nameof(Step.ValueClass));
        yield return Check<Step>(TableNames.Step, "value_class_content", Any(new IsNull(nameof(Step.ValueClass)),
            OneOf(nameof(Step.StepType), StepType.Task, StepType.Decision, StepType.Wait)));
        // The incoming volume: its quantity and its period together, within bounds, only on a start step (spec 14.2, S1).
        yield return ClosedList<Step, VolumePeriod>(TableNames.Step, nameof(Step.VolumePeriod));
        yield return Check<Step>(TableNames.Step, "volume", Together(nameof(Step.VolumeQuantity), nameof(Step.VolumePeriod)));
        yield return Check<Step>(TableNames.Step, "volume_quantity", Any(new IsNull(nameof(Step.VolumeQuantity)),
            All(new Between(nameof(Step.VolumeQuantity), 0, IncomingVolume.MaxQuantity), new EqualsCode(nameof(Step.StepType), Codes.Of(StepType.Start)))));
        yield return Deletion<Step>(TableNames.Step);
        yield return Check<Flow>(TableNames.Flow, "not_reflexive", new Differ(nameof(Flow.SourceStepId), nameof(Flow.TargetStepId)));
        // The share of the cases of an exit, in percent; its source is a gateway, which the domain checks (spec 14.2, S2).
        yield return Check<Flow>(TableNames.Flow, "share_percent", new Between(nameof(Flow.SharePercent), 0, CaseShare.Max));
        // The route chosen by the user (ADR 091): sides from their list; one passage at most, an aisle, the middle of a
        // column or a row; a row named by its lane and its number, through its middle or along its band at the top or the
        // bottom, never an edge without them; within the bounds of the grid.
        yield return ClosedList<Flow, FlowSide>(TableNames.Flow, nameof(Flow.SourceSide));
        yield return ClosedList<Flow, FlowSide>(TableNames.Flow, nameof(Flow.TargetSide));
        yield return Check<Flow>(TableNames.Flow, "via_edge", OneOf(nameof(Flow.ViaEdge), FlowSide.Top, FlowSide.Bottom));
        yield return Check<Flow>(TableNames.Flow, "via_band", Any(
            All(new IsNull(nameof(Flow.ViaLaneId)), new IsNull(nameof(Flow.ViaRow)), new IsNull(nameof(Flow.ViaEdge))),
            All(new IsNotNull(nameof(Flow.ViaLaneId)), new IsNotNull(nameof(Flow.ViaRow)))));
        yield return Check<Flow>(TableNames.Flow, "via", All(
            Any(new IsNull(nameof(Flow.ViaAisle)), new IsNull(nameof(Flow.ViaColumn))),
            Any(new IsNull(nameof(Flow.ViaAisle)), new IsNull(nameof(Flow.ViaLaneId))),
            Any(new IsNull(nameof(Flow.ViaColumn)), new IsNull(nameof(Flow.ViaLaneId)))));
        yield return Check<Flow>(TableNames.Flow, "via_aisle", new Between(nameof(Flow.ViaAisle), FlowLinks.FirstAisle, Cell.MaxColumn));
        yield return Check<Flow>(TableNames.Flow, "via_column", new Between(nameof(Flow.ViaColumn), 0, Cell.MaxColumn));
        yield return Check<Flow>(TableNames.Flow, "via_row", new Between(nameof(Flow.ViaRow), 0, Cell.MaxRow));
        yield return Deletion<Flow>(TableNames.Flow);
        yield return ClosedList<StepRaci, RaciRole>(TableNames.StepRaci, nameof(StepRaci.Role));
        yield return Check<Dissolution>(TableNames.Dissolution, "closed", Together(nameof(Dissolution.ClosedAt), nameof(Dissolution.ClosedBy)));
        yield return Check<DissolutionObject>(TableNames.DissolutionObject, "object_type",
            new InList(nameof(DissolutionObject.ObjectType), [Domain.Dissolutions.DissolutionObjectTypes.Flow, Domain.Dissolutions.DissolutionObjectTypes.Lane]));
    }

    private static IEnumerable<CheckGuard> SheetChecks()
    {
        yield return ClosedList<StepSheet, SheetStatus>(TableNames.StepSheet, nameof(StepSheet.Status));
        yield return ClosedList<StepSheet, Frequency>(TableNames.StepSheet, nameof(StepSheet.Frequency));
        yield return Check<StepSheet>(TableNames.StepSheet, "duration_minutes", new Between(nameof(StepSheet.DurationMinutes), 0, Duration.MaxMinutes));
        // The shortest and the longest duration (Makigami M5): durations, in order around the duration.
        yield return Check<StepSheet>(TableNames.StepSheet, "duration_min_minutes", new Between(nameof(StepSheet.DurationMinMinutes), 0, Duration.MaxMinutes));
        yield return Check<StepSheet>(TableNames.StepSheet, "duration_max_minutes", new Between(nameof(StepSheet.DurationMaxMinutes), 0, Duration.MaxMinutes));
        yield return Check<StepSheet>(TableNames.StepSheet, "duration_bounds", All(new NotAbove(nameof(StepSheet.DurationMinMinutes), nameof(StepSheet.DurationMinutes)),
            new NotAbove(nameof(StepSheet.DurationMinutes), nameof(StepSheet.DurationMaxMinutes)),
            new NotAbove(nameof(StepSheet.DurationMinMinutes), nameof(StepSheet.DurationMaxMinutes))));
        // The lead time: its value and its unit together, within bounds (spec 14.2, S3).
        yield return ClosedList<StepSheet, LeadTimeUnit>(TableNames.StepSheet, nameof(StepSheet.LeadTimeUnit));
        yield return Check<StepSheet>(TableNames.StepSheet, "lead_time", Together(nameof(StepSheet.LeadTime), nameof(StepSheet.LeadTimeUnit)));
        yield return Check<StepSheet>(TableNames.StepSheet, "lead_time_value", new Between(nameof(StepSheet.LeadTime), 0, LeadTime.MaxValue));
        yield return Check<SheetOperation>(TableNames.StepOperation, "position", new Between(nameof(SheetOperation.Position), 1, SheetContentReader.ProcedureMax));
        yield return ClosedList<SheetOperation, OperationNature>(TableNames.StepOperation, nameof(SheetOperation.Nature));
        yield return Check<StepSheetVersion>(TableNames.StepSheetVersion, "version_number", Counted(nameof(StepSheetVersion.VersionNumber), 1));
        yield return ClosedList<StepSheetVersion, Frequency>(TableNames.StepSheetVersion, nameof(StepSheetVersion.Frequency));
        yield return Check<StepSheetVersion>(TableNames.StepSheetVersion, "duration_minutes",
            new Between(nameof(StepSheetVersion.DurationMinutes), 0, Duration.MaxMinutes));
        yield return ClosedList<StepSheetVersion, LeadTimeUnit>(TableNames.StepSheetVersion, nameof(StepSheetVersion.LeadTimeUnit));
        yield return Check<StepSheetVersion>(TableNames.StepSheetVersion, "lead_time",
            Together(nameof(StepSheetVersion.LeadTime), nameof(StepSheetVersion.LeadTimeUnit)));
        yield return Check<StepSheetVersion>(TableNames.StepSheetVersion, "lead_time_value",
            new Between(nameof(StepSheetVersion.LeadTime), 0, LeadTime.MaxValue));
        yield return ClosedList<Document, DocumentDirection>(TableNames.Document, nameof(Document.Direction));
        yield return Check<Document>(TableNames.Document, "location", Together(nameof(Document.Location), nameof(Document.LocationType)));
        yield return ClosedList<Document, LocationType>(TableNames.Document, nameof(Document.LocationType));
        // The medium a document is carried on (Makigami M3).
        yield return ClosedList<Document, DocumentMedium>(TableNames.Document, nameof(Document.Medium));
        // The waiting time of an output is carried by each of its uses (1.x).
        yield return Check<Document>(TableNames.Document, "wait_hours", Any(new IsNull(nameof(Document.WaitHours)),
            All(new AtLeast(nameof(Document.WaitHours), 0), new EqualsCode(nameof(Document.Direction), Codes.Of(DocumentDirection.Input)))));
        yield return ClosedList<PainPoint, PainPointStatus>(TableNames.PainPoint, nameof(PainPoint.Status));
        yield return Check<PainPoint>(TableNames.PainPoint, "lost_hours_per_month", new AtLeast(nameof(PainPoint.LostHoursPerMonth), 0));
        yield return ClosedList<PainPointStatusChange, PainPointStatus>(TableNames.PainPointStatusHistory, nameof(PainPointStatusChange.StatusBefore));
        yield return ClosedList<PainPointStatusChange, PainPointStatus>(TableNames.PainPointStatusHistory, nameof(PainPointStatusChange.StatusAfter));
        yield return Check<DocumentUse>(TableNames.DocumentUse, "wait_hours", new AtLeast(nameof(DocumentUse.WaitHours), 0));
        yield return Check<DocumentUse>(TableNames.DocumentUse, "percent_complete_accurate",
            new Between(nameof(DocumentUse.PercentCompleteAccurate), 0, Domain.Documents.DocumentRules.PercentMax));
    }

    private static IEnumerable<CheckGuard> JournalChecks()
    {
        // The translated field follows the type of object (1.x `db/005`): the name of projects, maps and lanes, the label of steps,
        // flows and summaries of maps (plan 3.1, lot R).
        yield return Check<Translation>(TableNames.Translation, "object_field", Any(
            All(OneOf<TranslationObject>(nameof(Translation.ObjectType), TranslationObject.Project, TranslationObject.Map, TranslationObject.Lane),
                new EqualsCode(nameof(Translation.Field), Codes.Of(TranslationField.Name))),
            All(OneOf<TranslationObject>(nameof(Translation.ObjectType), TranslationObject.Step, TranslationObject.Flow, TranslationObject.Summary),
                new EqualsCode(nameof(Translation.Field), Codes.Of(TranslationField.Label)))));
        yield return ClosedList<Translation, TranslationSource>(TableNames.Translation, nameof(Translation.Source));
        yield return ClosedList<MapSummary, SummaryOrigin>(TableNames.MapSummary, nameof(MapSummary.Origin));
        foreach (var guard in SnapshotChecks())
            yield return guard;
        yield return Check<OperationLogEntry>(TableNames.OperationLog, "sequence", Counted(nameof(OperationLogEntry.Sequence), 0));
    }

    /// <summary>
    /// The settings of the assistant (3.0): one row only, a connector of the list, a ceiling of a reply within the bounds of
    /// the application, a version that starts at 1.
    /// </summary>
    private static IEnumerable<CheckGuard> AssistantChecks()
    {
        yield return Check<Assistant.AssistantSettingsRow>(TableNames.AssistantSettings, "single",
            new Between(nameof(Assistant.AssistantSettingsRow.Id), Assistant.AssistantSettingsRow.SingleId, Assistant.AssistantSettingsRow.SingleId));
        yield return ClosedList<Assistant.AssistantSettingsRow, Application.Assistant.AssistantConnector>(TableNames.AssistantSettings,
            nameof(Assistant.AssistantSettingsRow.Connector));
        yield return Check<Assistant.AssistantSettingsRow>(TableNames.AssistantSettings, "max_output_tokens",
            new Between(nameof(Assistant.AssistantSettingsRow.MaxOutputTokens), Application.Assistant.AssistantRules.MinOutputTokens,
                Application.Assistant.AssistantRules.MaxOutputTokens));
        yield return Check<Assistant.AssistantSettingsRow>(TableNames.AssistantSettings, "version", Counted(nameof(Assistant.AssistantSettingsRow.Version), 1));
    }

    /// <summary>The photos of the indicators (Makigami M8): an origin of its list, shares within 0 and 1, counts, times and hours never negative.</summary>
    private static IEnumerable<CheckGuard> SnapshotChecks()
    {
        const string table = TableNames.StreamSnapshot;
        yield return ClosedList<StreamSnapshot, SnapshotOrigin>(table, nameof(StreamSnapshot.Origin));
        foreach (var (rule, share) in new[] { ("value_added_share", nameof(StreamSnapshot.ValueAddedShare)), ("coverage", nameof(StreamSnapshot.Coverage)),
            ("rolled_complete_accurate", nameof(StreamSnapshot.RolledCompleteAccurate)) })
            yield return Check<StreamSnapshot>(table, rule, new Between(share, 0, 1));
        foreach (var (rule, count) in new[] { ("handoffs", nameof(StreamSnapshot.Handoffs)), ("media_breaks", nameof(StreamSnapshot.MediaBreaks)),
            ("open_pain_points", nameof(StreamSnapshot.OpenPainPoints)) })
            yield return Check<StreamSnapshot>(table, rule, Counted(count, 0));
        foreach (var (rule, amount) in new[] { ("lead_hours", nameof(StreamSnapshot.LeadHours)), ("handoff_wait_hours", nameof(StreamSnapshot.HandoffWaitHours)),
            ("other_wait_hours", nameof(StreamSnapshot.OtherWaitHours)), ("work_minutes", nameof(StreamSnapshot.WorkMinutes)),
            ("handoffs_per_case", nameof(StreamSnapshot.HandoffsPerCase)), ("lost_hours_per_month", nameof(StreamSnapshot.LostHoursPerMonth)) })
            yield return Check<StreamSnapshot>(table, rule, new AtLeast(amount, 0));
    }

    /// <summary>Every modifiable record starts at version 1 and only moves forward (D06).</summary>
    private static IEnumerable<CheckGuard> VersionChecks() =>
        new (Type Record, string Table)[]
        {
            (typeof(AppUser), TableNames.AppUser), (typeof(Project), TableNames.Project), (typeof(ProjectMember), TableNames.ProjectMember),
            (typeof(ProcessMap), TableNames.ProcessMap), (typeof(Lane), TableNames.Lane), (typeof(Step), TableNames.Step),
            (typeof(Flow), TableNames.Flow), (typeof(StepRaci), TableNames.StepRaci), (typeof(Dissolution), TableNames.Dissolution),
            (typeof(StepSheet), TableNames.StepSheet), (typeof(Document), TableNames.Document), (typeof(DocumentUse), TableNames.DocumentUse),
            (typeof(PainPoint), TableNames.PainPoint), (typeof(Translation), TableNames.Translation), (typeof(MapSummary), TableNames.MapSummary),
            (typeof(StreamSnapshot), TableNames.StreamSnapshot),
        }.Select(item => new CheckGuard(item.Record, $"CK_{item.Table}_version", Counted(nameof(IVersionedRecord.Version), 1)));

    /// <summary>
    /// The guard of every column stored as text, codes of the closed lists included (ADR 038): no trailing space, at most
    /// its width (exactly its width for a `char(n)`), printable ASCII for a technical column (`varchar`). SQL Server ignores
    /// the trailing spaces of a comparison and keeps its widths by itself; SQLite compares them and keeps any length: with
    /// these guards both engines accept and refuse the same texts. Derived from the model, which holds the widths.
    /// </summary>
    public static IEnumerable<CheckGuard> TextChecks(IReadOnlyModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        foreach (var entity in model.GetEntityTypes())
        {
            var table = entity.GetTableName() ?? throw new InvalidOperationException($"{entity.ClrType.Name} has no table name.");
            foreach (var property in entity.GetProperties().Where(IsStoredAsText))
                yield return new CheckGuard(entity.ClrType, TextCheckName(table, property.GetColumnName()), TextGuard(property));
        }
    }

    /// <summary>The name of the text guard of a column: `CK_table_column_text`.</summary>
    public static string TextCheckName(string table, string column) => $"CK_{table}_{column}_text";

    /// <summary>A column whose value reaches the engine as a string: a text, or a closed list stored as its code.</summary>
    public static bool IsStoredAsText(IReadOnlyProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return (property.GetValueConverter()?.ProviderClrType ?? property.ClrType) == typeof(string);
    }

    private static GuardCondition TextGuard(IReadOnlyProperty property)
    {
        var conditions = new List<GuardCondition> { new NoTrailingSpace(property.Name) };
        if (property.GetMaxLength() is { } width)
            conditions.Add(new TextLength(property.Name, width, property.IsFixedLength() == true));
        if (property.IsUnicode() == false)
            conditions.Add(new PrintableAscii(property.Name));
        return conditions.Count == 1 ? conditions[0] : new AllOf(conditions);
    }

    /// <summary>
    /// A counter or a version: at least <paramref name="minimum"/> and within an `int`. SQL Server refuses a value beyond
    /// (8115); SQLite would store it in its 64-bit integer, which no longer reads back as an `int` (`structure_version + 1`).
    /// </summary>
    private static Between Counted(string property, int minimum) => new(property, minimum, int.MaxValue);

    private static CheckGuard Check<T>(string table, string rule, GuardCondition condition) => new(typeof(T), $"CK_{table}_{rule}", condition);

    /// <summary>`deleted_at` and `deletion_batch_id` are both set or both empty (spec 4.2).</summary>
    private static CheckGuard Deletion<T>(string table)
        where T : ISoftDeletable =>
        Check<T>(table, "deletion", Together(nameof(ISoftDeletable.DeletedAt), nameof(ISoftDeletable.DeletionBatchId)));

    /// <summary>A closed list: the column takes one of the codes of the enumeration.</summary>
    private static CheckGuard ClosedList<T, TEnum>(string table, string property)
        where TEnum : struct, Enum =>
        Check<T>(table, ModelNaming.SnakeCase(property), new InList(property, Codes.All<TEnum>()));

    private static InList OneOf<TEnum>(string property, params TEnum[] values)
        where TEnum : struct, Enum =>
        new(property, values.Select(value => Codes.Of(value)).ToArray());
}
