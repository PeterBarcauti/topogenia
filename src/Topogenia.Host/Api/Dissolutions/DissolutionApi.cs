// DissolutionApi.cs
// The routes of the dissolution of a detail turned into their commands: dissolve, assign, remove, finish (annex A 1 remontees, A 2.9).

using Topogenia.Application.Commands;
using Topogenia.Application.Dissolutions;
using Topogenia.Application.Maps;
using Topogenia.Application.OperationLog;
using Topogenia.Domain.Dissolutions;

namespace Topogenia.Host.Api.Dissolutions;

/// <summary>
/// `POST /api/dissolutions/{id}/assign` (1.x `AffectationDemande`: `genre`, `objet_id`, `etape_id`, `depuis_etape_id`):
/// the kind of the element (`document`, `usage`, `pain_point`), the element, the step of arrival (null: the map, for a
/// pain point; the dissolved step: back to the remainder) and the step that holds it when it comes back.
/// </summary>
public sealed record AssignBody(string? Kind, Guid? ObjectId, Guid? StepId, Guid? FromStepId);

/// <summary>`POST /api/dissolutions/{id}/remove` (1.x: the same `AffectationDemande`, of which only `genre` and `objet_id` are read).</summary>
public sealed record RemoveBody(string? Kind, Guid? ObjectId);

/// <summary>
/// The reply of « assign » and « remove »: the fields of <see cref="StructureReply"/> (`id` is the dissolution) and the
/// `frame` that the 1.x returned alone (`RemonteeVue`): the open frame of the map with its remainder, null once closed.
/// </summary>
public sealed record FrameReply(
    Guid? Id,
    string? Code,
    IReadOnlyList<Guid> Ids,
    Guid? OperationId,
    IReadOnlyList<string> Warnings,
    int StructureVersion,
    MapView? Map,
    HistoryView? History,
    FrameView? Frame)
{
    public static FrameReply Of(CommandOutcome<FrameView?> outcome, Guid dissolutionId)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return new FrameReply(dissolutionId, null, [], outcome.OperationId, outcome.Warnings, outcome.StructureVersion, outcome.View,
            outcome.History, outcome.Result);
    }
}

/// <summary>
/// The reply of « finish »: the fields of <see cref="StructureReply"/> (`id` is the dissolution) and the `arrangements`
/// that the 1.x returned alone (`Rangement`: kind, object, name, step of arrival), none for a frame already closed.
/// </summary>
public sealed record FinishReply(
    Guid? Id,
    string? Code,
    IReadOnlyList<Guid> Ids,
    Guid? OperationId,
    IReadOnlyList<string> Warnings,
    int StructureVersion,
    MapView? Map,
    HistoryView? History,
    IReadOnlyList<Arrangement> Arrangements)
{
    public static FinishReply Of(CommandOutcome<IReadOnlyList<Arrangement>> outcome, Guid dissolutionId)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return new FinishReply(dissolutionId, null, [], outcome.OperationId, outcome.Warnings, outcome.StructureVersion, outcome.View,
            outcome.History, outcome.Result);
    }
}

/// <summary>Reads the bodies of the dissolution routes into the requests of the domain and runs their commands.</summary>
public sealed class DissolutionApi
{
    private readonly CommandRunner runner;

    public DissolutionApi(CommandRunner runner)
    {
        this.runner = runner;
    }

    /// <summary>`POST /api/steps/{id}/dissolve`: `id` is the dissolution (the frame), `code` the dissolved step, `ids` the steps of the frame.</summary>
    public async Task<StructureReply> DissolveAsync(Guid stepId, string? ifMatch, CancellationToken cancellationToken)
    {
        var outcome = await runner.RunAsync(DissolveCommand.WithVersion(stepId, ApiInput.Version(ifMatch)), cancellationToken);
        return StructureReply.Of(outcome, outcome.Result.DissolutionId, outcome.Result.StepCode, outcome.Result.FrameStepIds);
    }

    public async Task<FrameReply> AssignAsync(Guid dissolutionId, AssignBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var request = new AssignRequest(body.Kind, ApiInput.Required(body.ObjectId, "object_id"), body.StepId, body.FromStepId);
        var outcome = await runner.RunAsync(new AssignRemainderCommand(dissolutionId, request), cancellationToken);
        return FrameReply.Of(outcome, dissolutionId);
    }

    public async Task<FrameReply> RemoveAsync(Guid dissolutionId, RemoveBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var request = new RemoveRequest(body.Kind, ApiInput.Required(body.ObjectId, "object_id"));
        var outcome = await runner.RunAsync(new RemoveRemainderCommand(dissolutionId, request), cancellationToken);
        return FrameReply.Of(outcome, dissolutionId);
    }

    public async Task<FinishReply> FinishAsync(Guid dissolutionId, CancellationToken cancellationToken)
    {
        var outcome = await runner.RunAsync(new FinishDissolutionCommand(dissolutionId), cancellationToken);
        return FinishReply.Of(outcome, dissolutionId);
    }
}
