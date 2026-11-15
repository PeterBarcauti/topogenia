// RaciApi.cs
// The route of the RACI turned into its command: set or remove the role of a lane on a step (annex A 1 etapes, A 2.13).

using Topogenia.Application.Commands;
using Topogenia.Application.Raci;
using Topogenia.Domain.Raci;

namespace Topogenia.Host.Api.Raci;

/// <summary>`PUT /api/steps/{id}/raci` (1.x `RaciDemande`: `couloir_id`, `role`): a null role removes the role of the lane.</summary>
public sealed record SetRaciBody(Guid? LaneId, string? Role);

/// <summary>Reads the body of the RACI route into the request of the domain and runs its command.</summary>
public sealed class RaciApi
{
    private readonly CommandRunner runner;

    public RaciApi(CommandRunner runner)
    {
        this.runner = runner;
    }

    /// <summary>`id` is the step, as the 1.x `ResultatStructure.Objet(etapeId)`; the If-Match is the version of the step.</summary>
    public async Task<StructureReply> SetAsync(Guid stepId, SetRaciBody body, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var request = new SetRaciRequest(stepId, ApiInput.Required(body.LaneId, "lane_id"), body.Role);
        var outcome = await runner.RunAsync(SetRaciCommand.WithVersion(request, ApiInput.Version(ifMatch)), cancellationToken);
        return StructureReply.Of(outcome, stepId);
    }
}
