// MapQueries.cs
// The read of a map with the history of the reader on it (annex A 1 `GET /api/cartes/{id}`, A 2.4, annex B 2.6).

using Topogenia.Application.Authorization;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Maps;

/// <summary>The map as the reader sees it, and what he can undo and redo on it (the history survives a reload, B-2.6-03).</summary>
public sealed record MapRead(MapView View, HistoryView History);

/// <summary>`GET /api/maps/{id}` (1.x `GET /api/cartes/{id}`, Lecteur): INTROUVABLE for a map that is not live.</summary>
[RequiresRole(MemberRole.Reader)]
public sealed class GetMapQuery(Guid mapId) : ProjectQuery<MapRead>(AccessTarget.Map(mapId))
{
    public override bool ReadsMapView => true;

    public override Guid? HistoryMap => Target.Id;

    public override MapRead Read(ProjectWorkspace workspace, QueryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new MapRead(context.Views.Build(workspace, Target.Id, context.Viewer), context.History!);
    }
}

/// <summary>`GET /api/maps/{id}/history` (new in 2.0, spec 3.6, Lecteur): what the reader can undo and redo on the map.</summary>
[RequiresRole(MemberRole.Reader)]
public sealed class GetHistoryQuery(Guid mapId) : ProjectQuery<HistoryView>(AccessTarget.Map(mapId))
{
    public override Guid? HistoryMap => Target.Id;

    /// <summary>INTROUVABLE for a map that is not live, as its read.</summary>
    public override HistoryView Read(ProjectWorkspace workspace, QueryContext context)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(context);
        workspace.LiveMap(Target.Id);
        return context.History!;
    }
}
