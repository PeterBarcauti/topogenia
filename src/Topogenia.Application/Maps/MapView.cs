// MapView.cs
// The view of a map returned by its read and by every structure command (annex A 2.4, 1.x `CarteVue`, AR-50).

using Topogenia.Application.Sheets;
using Topogenia.Domain.Dissolutions;
using Topogenia.Domain.Maps;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;
using Topogenia.Domain.ValueStreams;

namespace Topogenia.Application.Maps;

/// <summary>A map of the breadcrumb, from the root map down to the map read (1.x `ArianeVue`), with the language its name was entered in.</summary>
public sealed record BreadcrumbView(Guid MapId, string Name, string SourceLanguage, IReadOnlyDictionary<string, string> Translations);

public sealed record LaneView(
    Guid Id,
    string Code,
    string Name,
    string SourceLanguage,
    IReadOnlyDictionary<string, string> Translations,
    LaneType Type,
    string Color,
    int SortOrder,
    Guid? ActorUserId,
    int Version)
{
    /// <summary>The headcount of a lane of people (spec 14.2, S4).</summary>
    public int? Headcount { get; init; }
}

/// <summary>The markers of a step (1.x `ReperesEtape`): documents produced or received, pain points still open.</summary>
public sealed record StepMarkersView(IReadOnlyList<DocumentMarker> Documents, IReadOnlyList<PainPointMarker> PainPoints);

public sealed record StepView(
    Guid Id,
    string Code,
    string Label,
    string SourceLanguage,
    StepType Type,
    Guid LaneId,
    int Column,
    int Row,
    string Path,
    Guid? SubMapId,
    bool SubMapEmpty,
    Guid? AssigneeUserId,
    AutomationStatus AutomationStatus,
    DateTime LastModified,
    SheetSummary Sheet,
    int Version,
    IReadOnlyDictionary<string, string> Translations,
    StepMarkersView Markers)
{
    /// <summary>The incoming volume of a start step (spec 14.2, S1).</summary>
    public IncomingVolume? Volume { get; init; }
}

/// <summary>A flow with the names of the documents it carries (outputs of the source used by the target).</summary>
public sealed record FlowView(
    Guid Id,
    Guid SourceStepId,
    Guid TargetStepId,
    string? Label,
    IReadOnlyList<string> Documents,
    int Version,
    IReadOnlyDictionary<string, string> Translations)
{
    /// <summary>The share of the cases of an exit of a decision or an inclusive gateway, in percent (spec 14.2, S2).</summary>
    public decimal? SharePercent { get; init; }
}

public sealed record WarningView(Guid StepId, string Code);

/// <summary>A visible RACI role of a step on a lane (the responsible lane R is the lane of the step and is not listed).</summary>
public sealed record RaciView(Guid StepId, Guid LaneId, RaciRole Role);

/// <summary>
/// The read-only sheet of the remainder of the open frame (1.x `RemonteeVue.fiche`), as the viewer may see it (weakness
/// 1.x 5.6-1, `SheetVisibility.ForRemainder`): the facilitator sees the draft, anyone else the latest validated version,
/// or an empty sheet. Nobody fills it: the dissolved step is gone.
/// </summary>
public sealed record RemainderSheetView(SheetStatus Status, SheetContent Content, bool IsDraft, SheetVersionView? Validated, string? ClarificationRequest);

/// <summary>What the viewer may do on the map: change its structure (facilitator), and the role he acts with.</summary>
public sealed record MapRightsView(bool Structure, MemberRole Role);

/// <summary>
/// The languages of the project (ADR 083): its language of origin, in which every text is created and which alone may
/// change the structure, the number of its texts, and for each language the number written in it (original or
/// translation), from which the client shows how far each language is translated.
/// </summary>
public sealed record ProjectLanguagesView(string Origin, int Total, IReadOnlyDictionary<string, int> Written);

/// <summary>
/// An object named by the completeness check of the value stream (ADR 085): the readable code and the label of a step or
/// a lane, in its language of origin, with its map; for a detailed map, those of the step it details, for the root map the
/// code of the project and the name of the map. Each mention thus names its object, a detailed map included.
/// </summary>
public sealed record StreamSubjectView(string Code, string Label, string SourceLanguage, Guid MapId);

/// <summary>
/// The whole map as the 1.x `GET /api/cartes/{id}` returned it (annex A 2.4), with integer versions (D06) and the structure
/// version of the project, so that the client knows whether its map is up to date (spec 4.7), and the languages of the
/// project (ADR 083).
/// </summary>
public sealed record MapView(
    Guid Id,
    Guid ProjectId,
    string ProjectCode,
    string Name,
    string SourceLanguage,
    IReadOnlyDictionary<string, string> Translations,
    Guid? ParentStepId,
    int Depth,
    int RecommendedDepth,
    IReadOnlyList<BreadcrumbView> Breadcrumb,
    IReadOnlyList<LaneView> Lanes,
    IReadOnlyList<StepView> Steps,
    IReadOnlyList<FlowView> Flows,
    IReadOnlyList<WarningView> Warnings,
    IReadOnlyList<RaciView> Raci,
    string RaciLetters,
    MapRightsView Rights,
    int Version,
    int StructureVersion,
    FrameView? Dissolution,
    RemainderSheetView? DissolutionSheet,
    ProjectLanguagesView Languages)
{
    /// <summary>
    /// The value stream of the map and its detailed maps (ADR 085), computed by the core at every reading with the sheets as
    /// the viewer sees them: the figures of its steps, flows and lanes, the main path, the totals and the completeness check.
    /// </summary>
    public required MapStream ValueStream { get; init; }

    /// <summary>The objects named by the completeness check of <see cref="ValueStream"/>, by identifier of the step, lane or map.</summary>
    public required IReadOnlyDictionary<Guid, StreamSubjectView> StreamSubjects { get; init; }
}
