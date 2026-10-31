// SelectionApi.cs
// The routes of a selection turned into their commands: delete, paste, cut and paste, detail the selection (annex A 1 cartes, A 2.8).

using Topogenia.Application.Commands;
using Topogenia.Application.Selection;
using Topogenia.Domain.Selection;
using Topogenia.Domain.Structure;
using Topogenia.Domain.SubMaps;

namespace Topogenia.Host.Api.Selection;

/// <summary>
/// `POST /api/maps/{id}/deletion` (1.x `SuppressionDemande`: `etapes`, `liens`, `recoudre`, `sous_cartes=supprimer`):
/// the steps and flows deleted in one batch, `stitch`, and `sub_maps` = `delete` for the filled sub-maps.
/// </summary>
public sealed record DeleteSelectionBody(IReadOnlyList<Guid>? Steps, IReadOnlyList<Guid>? Flows, bool? Stitch, string? SubMaps);

/// <summary>The insertion of a block in the flow (1.x `AncreDemande`: `etape_id`, `position`: `before` or `after`).</summary>
public sealed record AnchorBody(Guid? StepId, string? Position);

/// <summary>A step of a pasted copy (1.x `EtapeCollee`: `cle`, `libelle`, `type`, `couloir_id`, `colonne`, `rangee`).</summary>
public sealed record PastedStepBody(string? Key, string? Label, string? Type, Guid? LaneId, int? Column, int? Row);

/// <summary>A flow of a pasted copy, between two keys of the paste (1.x `LienColle`: `source`, `cible`, `libelle`).</summary>
public sealed record PastedFlowBody(string? Source, string? Target, string? Label);

/// <summary>`POST /api/maps/{id}/paste` (1.x `CollageDemande`: `etapes`, `liens`, `ancre` facultative).</summary>
public sealed record PasteBody(IReadOnlyList<PastedStepBody>? Steps, IReadOnlyList<PastedFlowBody>? Flows, AnchorBody? Anchor);

/// <summary>The cell of arrival of a transferred step (1.x `PositionEtape`: `id`, `couloir_id`, `colonne`, `rangee`).</summary>
public sealed record StepPlacementBody(Guid? Id, Guid? LaneId, int? Column, int? Row);

/// <summary>`POST /api/maps/{id}/transfer` (1.x `TransfertDemande`: `etapes`, `couper`, `ancre` facultative).</summary>
public sealed record TransferBody(IReadOnlyList<StepPlacementBody>? Steps, bool? Cut, AnchorBody? Anchor);

/// <summary>`POST /api/maps/{id}/group`, « Detail the selection » (1.x `RegroupementDemande`: `etapes`, `libelle`).</summary>
public sealed record GroupBody(IReadOnlyList<Guid>? Steps, string? Label);

/// <summary>Reads the bodies of the selection routes into the requests of the domain and runs their commands.</summary>
public sealed class SelectionApi
{
    private readonly CommandRunner runner;

    public SelectionApi(CommandRunner runner)
    {
        this.runner = runner;
    }

    /// <summary>`ids`: the steps deleted (1.x: the batch, gone with the undo driven by the client, AR-50).</summary>
    public async Task<StructureReply> DeleteAsync(Guid mapId, DeleteSelectionBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var choice = ApiInput.Code<SubMapChoice>(body.SubMaps, "sub_maps");
        var request = new GroupDeletionRequest(body.Steps, body.Flows, body.Stitch ?? false, choice == SubMapChoice.Delete);
        var outcome = await runner.RunAsync(new DeleteSelectionCommand(mapId, request), cancellationToken);
        return StructureReply.Of(outcome, ids: outcome.Result.Ids);
    }

    /// <summary>`ids`: the steps created, in the order of the request (the client selects them).</summary>
    public async Task<StructureReply> PasteAsync(Guid mapId, PasteBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var steps = body.Steps?.Select(step => new PastedStep(step.Key, step.Label, step.Type, ApiInput.Required(step.LaneId, "lane_id"),
            ApiInput.Required(step.Column, "column"), step.Row ?? 0)).ToList();
        var flows = body.Flows?.Select(flow => new PastedFlow(flow.Source, flow.Target, flow.Label)).ToList();
        var outcome = await runner.RunAsync(new PasteCommand(mapId, new PasteRequest(steps, flows, AnchorOf(body.Anchor))), cancellationToken);
        return StructureReply.Of(outcome, ids: outcome.Result.Ids);
    }

    /// <summary>`ids`: the steps moved, in the order of the request; they keep their identifiers and codes.</summary>
    public async Task<StructureReply> TransferAsync(Guid mapId, TransferBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var steps = body.Steps?.Select(step => new StepPlacement(ApiInput.Required(step.Id, "id"), ApiInput.Required(step.LaneId, "lane_id"),
            ApiInput.Required(step.Column, "column"), step.Row ?? 0)).ToList();
        var request = new TransferRequest(steps, body.Cut ?? false, AnchorOf(body.Anchor));
        var outcome = await runner.RunAsync(new TransferBlockCommand(mapId, request), cancellationToken);
        return StructureReply.Of(outcome, ids: outcome.Result.Ids);
    }

    /// <summary>`id` and `code`: the new step; `ids`: its sub-map; PROFONDEUR_DEPASSEE in `warnings`.</summary>
    public async Task<StructureReply> GroupAsync(Guid mapId, GroupBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var outcome = await runner.RunAsync(new GroupSelectionCommand(mapId, new GroupRequest(body.Steps, body.Label)), cancellationToken);
        return StructureReply.Of(outcome, outcome.Result.StepId, outcome.Result.Code, outcome.Result.Ids);
    }

    private static AnchorRequest? AnchorOf(AnchorBody? anchor) =>
        anchor is null ? null : new AnchorRequest(ApiInput.Required(anchor.StepId, "step_id"), anchor.Position);
}
