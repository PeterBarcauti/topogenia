// IMapViewBuilder.cs
// The view of a map built from the workspace of the command that changed it, so the reply needs no second read (spec 3.4 step 6).

using Topogenia.Application.Ports;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Commands;

/// <summary>Who reads the view: the rights and the visible RACI depend on the user and the role he acts with.</summary>
public sealed record MapViewer(Guid UserId, MemberRole Role);

/// <summary>
/// Builds the view of a map (annex A 2.4, `GET /api/maps/{id}`) from a workspace. The command template calls it on the
/// workspace it has just written, inside the same transaction, so that the reply of a structure command carries the view
/// up to date (spec 3.4 step 6, AR-50); the read query of a map uses the same builder (P4-T02).
/// </summary>
public interface IMapViewBuilder
{
    /// <summary>The groups of records the view reads besides the structure (sheets for the summaries, documents for the markers...).</summary>
    WorkspaceContent Content { get; }

    /// <summary>The view of a live map; INTROUVABLE when the map is not live.</summary>
    Maps.MapView Build(ProjectWorkspace workspace, Guid mapId, MapViewer viewer);
}
