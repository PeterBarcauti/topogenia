// MapApi.cs
// The routes of a map turned into their queries and commands: read, history, undo, redo, delete the detail, reattach (annex A 1 cartes, spec 4.6).

using Topogenia.Application.Commands;
using Topogenia.Application.Maps;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Queries;
using Topogenia.Application.SubMaps;

namespace Topogenia.Host.Api.Maps;

/// <summary>The read of a map: the map and what the reader can undo and redo on it (annex B 2.6: it survives a reload).</summary>
public sealed record MapReply(MapView Map, HistoryView History);

/// <summary>`POST /api/maps/{id}/reattach` (1.x `RattachementDemande`): the step that receives the sub-map.</summary>
public sealed record ReattachBody(Guid? StepId);

/// <summary>Runs the queries and the commands of the map routes.</summary>
public sealed class MapApi
{
    private readonly CommandRunner commands;
    private readonly QueryRunner queries;

    public MapApi(CommandRunner commands, QueryRunner queries)
    {
        this.commands = commands;
        this.queries = queries;
    }

    public async Task<MapReply> GetAsync(Guid mapId, CancellationToken cancellationToken)
    {
        var read = await queries.RunAsync(new GetMapQuery(mapId), cancellationToken);
        return new MapReply(read.View, read.History);
    }

    public Task<HistoryView> HistoryAsync(Guid mapId, CancellationToken cancellationToken) =>
        queries.RunAsync(new GetHistoryQuery(mapId), cancellationToken);

    /// <summary>`POST /api/maps/{id}/undo`: `id` is the operation taken back, `code` its kind.</summary>
    public async Task<StructureReply> UndoAsync(Guid mapId, CancellationToken cancellationToken)
    {
        var outcome = await commands.RunAsync(new UndoCommand(mapId), cancellationToken);
        return StructureReply.Of(outcome, outcome.Result.OperationId, outcome.Result.Kind);
    }

    /// <summary>`POST /api/maps/{id}/redo`: `id` is the operation replayed, `code` its kind.</summary>
    public async Task<StructureReply> RedoAsync(Guid mapId, CancellationToken cancellationToken)
    {
        var outcome = await commands.RunAsync(new RedoCommand(mapId), cancellationToken);
        return StructureReply.Of(outcome, outcome.Result.OperationId, outcome.Result.Kind);
    }

    /// <summary>`DELETE /api/maps/{id}` (« Delete the detail », without `si_vide`, AR-08): `ids` are the maps deleted.</summary>
    public async Task<StructureReply> DeleteDetailAsync(Guid mapId, string? ifMatch, CancellationToken cancellationToken)
    {
        var outcome = await commands.RunAsync(DeleteSubMapCommand.WithVersion(mapId, ApiInput.Version(ifMatch)), cancellationToken);
        return StructureReply.Of(outcome, mapId, ids: outcome.Result);
    }

    public async Task<StructureReply> ReattachAsync(Guid mapId, ReattachBody body, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var step = ApiInput.Required(body.StepId, "step_id");
        var outcome = await commands.RunAsync(ReattachSubMapCommand.WithVersion(mapId, step, ApiInput.Version(ifMatch)), cancellationToken);
        return StructureReply.Of(outcome, outcome.Result);
    }
}
