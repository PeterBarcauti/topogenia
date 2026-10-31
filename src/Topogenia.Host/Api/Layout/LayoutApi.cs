// LayoutApi.cs
// The routes of the layout turned into their commands: the grouped layout of a map and the grouped move of dragged steps (annex A 2.4, AR-44).

using Topogenia.Application.Commands;
using Topogenia.Application.Layout;
using Topogenia.Domain.Layout;
using Topogenia.Domain.Structure;

namespace Topogenia.Host.Api.Layout;

/// <summary>The exact cell of a step (1.x `PositionEtape`: `id`, `couloir_id`, `colonne`, `rangee`, row 0 by default).</summary>
public sealed record StepCellBody(Guid? Id, Guid? LaneId, int? Column, int? Row);

/// <summary>
/// `PUT /api/maps/{id}/layout` (1.x `DispositionDemande`: `ordre_couloirs`, `etapes`), with `purpose` (`move` by default,
/// `align` for the Align wand), which names the operation in the log as the 1.x client did.
/// </summary>
public sealed record LayoutBody(List<Guid>? LaneOrder, List<StepCellBody>? Steps, string? Purpose);

/// <summary>`POST /api/maps/{id}/move` (no 1.x route, AR-44): the dragged steps and their cells, one operation.</summary>
public sealed record MoveStepsBody(List<StepCellBody>? Steps);

/// <summary>Reads the bodies of the layout routes into the requests of the domain and runs their commands.</summary>
public sealed class LayoutApi
{
    private readonly CommandRunner runner;

    public LayoutApi(CommandRunner runner)
    {
        this.runner = runner;
    }

    /// <summary>`ids` are the steps placed, as the 1.x `Disposer`.</summary>
    public async Task<StructureReply> LayOutAsync(Guid mapId, LayoutBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var request = new LayoutRequest(mapId, body.LaneOrder, body.Steps is { } steps ? Placements(steps) : null);
        var purpose = ApiInput.Code<LayoutPurpose>(body.Purpose, "purpose") ?? LayoutPurpose.Move;
        var outcome = await runner.RunAsync(new ApplyLayoutCommand(request, purpose), cancellationToken);
        return StructureReply.Of(outcome, mapId, ids: outcome.Result);
    }

    /// <summary>`ids` are the steps moved.</summary>
    public async Task<StructureReply> MoveAsync(Guid mapId, MoveStepsBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var request = new GroupMoveRequest(mapId, Placements(ApiInput.Required(body.Steps, "steps")));
        var outcome = await runner.RunAsync(new MoveStepsCommand(request), cancellationToken);
        return StructureReply.Of(outcome, mapId, ids: outcome.Result);
    }

    private static List<StepPlacement> Placements(IReadOnlyList<StepCellBody> cells) =>
        [.. cells.Select(cell => new StepPlacement(ApiInput.Required(cell?.Id, "id"), ApiInput.Required(cell?.LaneId, "lane_id"),
            ApiInput.Required(cell?.Column, "column"), cell?.Row ?? 0))];
}
