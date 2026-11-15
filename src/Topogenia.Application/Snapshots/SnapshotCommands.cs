// SnapshotCommands.cs
// The photos of the indicators of a project and its target state (Makigami M6, M8, ADR 095): read them with the comparison, take one, delete one, create the target state, keep its reference again.

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Domain.Model;
using Topogenia.Domain.Snapshots;
using Topogenia.Domain.Values;
using Topogenia.Domain.ValueStreams;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Snapshots;

/// <summary>
/// A photo as the panel shows it: its label, origin, date, author, version and indicators, and the comparison of the
/// indicators now with it (P3e: before and after a workshop), as the core makes it.
/// </summary>
public sealed record SnapshotView(Guid Id, string Label, SnapshotOrigin Origin, DateTime CreatedAt, Guid CreatedBy, DateTime? UpdatedAt, int Version,
    StreamIndicators Indicators, IReadOnlyList<IndicatorChange> Changes);

/// <summary>
/// The photos of a project in the order taken, its indicators now as the reader sees them, and for a target state its
/// reference, the project it comes from and the comparison of now with the reference.
/// </summary>
public sealed record SnapshotsView(IReadOnlyList<SnapshotView> Photos, StreamIndicators Current, Guid? BaselineProjectId, SnapshotView? Baseline,
    IReadOnlyList<IndicatorChange>? Comparison)
{
    /// <summary>Where the source of a target state stands for the reader (<see cref="BaselineSourceQuery"/>), null for another project.</summary>
    public BaselineSource? Source { get; init; }
}

/// <summary>The indicators of the root map of a project and its details, its sheets as the viewer sees them (the single reading of the value stream).</summary>
internal static class ProjectIndicators
{
    public static StreamIndicators Of(ProjectWorkspace workspace, MapViewer viewer) => StreamFacts.IndicatorsOf(workspace, viewer.Role, viewer.UserId);

    public static SnapshotView ViewOf(StreamSnapshot snapshot, StreamIndicators current)
    {
        var kept = StreamIndicators.Of(snapshot);
        return new(snapshot.Id, snapshot.Label, snapshot.Origin, snapshot.CreatedAt, snapshot.CreatedBy, snapshot.UpdatedAt, snapshot.Version, kept,
            IndicatorComparison.Of(kept, current));
    }

    public static SnapshotsView Read(ProjectWorkspace workspace, MapViewer viewer)
    {
        var current = Of(workspace, viewer);
        var baseline = SnapshotRules.BaselineOf(workspace);
        return new SnapshotsView(
            [.. SnapshotRules.Of(workspace).Where(snapshot => snapshot.Origin == SnapshotOrigin.Photo).Select(snapshot => ViewOf(snapshot, current))], current,
            workspace.Project.BaselineProjectId, baseline is null ? null : ViewOf(baseline, current),
            baseline is null ? null : IndicatorComparison.Of(StreamIndicators.Of(baseline), current));
    }
}

/// <summary>`GET /api/projects/{id}/snapshots`: every member reads the photos and, for a target state, its comparison.</summary>
[RequiresRole(MemberRole.Reader)]
public sealed class SnapshotsQuery(Guid projectId) : ProjectQuery<SnapshotsView>(AccessTarget.Project(projectId))
{
    public override WorkspaceContent Content => WorkspaceContent.All;

    public override SnapshotsView Read(ProjectWorkspace workspace, QueryContext context)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(context);
        return ProjectIndicators.Read(workspace, context.Viewer);
    }
}

/// <summary>
/// `POST /api/projects/{id}/snapshots` (facilitator): a photo of the indicators as the facilitator sees them, under its label.
/// Not undoable, as a summary: a photo has its own record, outside the journal of the operations.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class TakeSnapshotCommand(Guid projectId, string? label) : ProjectCommand<Guid>(AccessTarget.Project(projectId))
{
    public override WorkspaceContent Content => WorkspaceContent.All;

    /// <summary>A photo touches no map: no view of a map in the reply, nothing in the journal of the operations.</summary>
    public override Guid? MapOf(ProjectWorkspace workspace) => null;

    public override Guid Execute(ProjectWorkspace workspace, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(context);
        return SnapshotRules.Take(workspace, context, label, ProjectIndicators.Of(workspace, new MapViewer(context.ActorId, MemberRole.Facilitator)));
    }
}

/// <summary>`DELETE /api/projects/{id}/snapshots/{snapshotId}` (facilitator): a photo goes; the reference of a target state stays.</summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class DeleteSnapshotCommand(Guid projectId, Guid snapshotId) : ProjectCommand<Guid>(AccessTarget.Project(projectId))
{
    public override Guid? MapOf(ProjectWorkspace workspace) => null;

    public override Guid Execute(ProjectWorkspace workspace, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        SnapshotRules.Delete(workspace, snapshotId);
        return snapshotId;
    }
}
