// MapSummaryCommands.cs
// The summaries of the maps written at once: those kept from a reply of the AI after review, or the one of a map written by hand (plan 3.1, lot R).

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Summaries;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Summaries;

/// <summary>A summary to write and the version of it the client read: 0 when it read none, null when it gives none.</summary>
public sealed record MapSummaryWrite(MapSummaryEntry Entry, int? Version);

/// <summary>
/// `PUT /api/maps/{id}/summaries` (plan 3.1, lot R): the summaries of maps of the project, written at once; the map of the
/// route is the one open, whose view the reply carries. Animateur, as for any text of the structure. Not undoable, as the
/// sheets and the translations: a summary has its own versions. Each summary carries the version the client read, checked
/// on the summary of its map when there is one (as the If-Match of a sheet): a summary written by someone else since is a
/// conflict, and a client that read none sends <see cref="VersionCheck.NoRecord"/>.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class WriteMapSummariesCommand(Guid mapId, IReadOnlyList<MapSummaryWrite> writes) : ProjectCommand<int>(AccessTarget.Map(mapId))
{
    public override WorkspaceContent Content => WorkspaceContent.Translations;

    public override Guid? MapOf(ProjectWorkspace workspace) => Target.Id;

    public override int Execute(ProjectWorkspace workspace, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        workspace.LiveMap(Target.Id);
        foreach (var write in writes)
        {
            if (write.Version is { } version && workspace.SummaryOf(write.Entry.MapId) is { } summary)
                VersionCheck.Of<MapSummary>(summary.Key, version).Ensure(workspace);
        }
        return MapSummaryRules.Write(workspace, context, new WriteMapSummariesRequest([.. writes.Select(write => write.Entry)]));
    }
}
