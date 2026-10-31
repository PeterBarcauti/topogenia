// LaneApi.cs
// The routes of the lanes turned into their commands: add, update, reorder, delete (annex A 1 couloirs, A 2.3).

using Topogenia.Application.Commands;
using Topogenia.Application.Lanes;
using Topogenia.Domain.Lanes;
using Topogenia.Domain.Values;
using Topogenia.Host.Json;

namespace Topogenia.Host.Api.Lanes;

/// <summary>`POST /api/maps/{id}/lanes` (1.x `CouloirDemande`: `nom`, `type`, `couleur`, `acteur_utilisateur_id`, `traductions`).</summary>
public sealed record AddLaneBody(
    string? Name,
    string? Type,
    string? Color,
    int? SortOrder,
    Guid? ActorUserId,
    Dictionary<string, string?>? Translations);

/// <summary>`PATCH /api/lanes/{id}`: only the fields present change; a null actor removes him.</summary>
public sealed record UpdateLaneBody
{
    public Optional<string?> Name { get; init; }

    public Optional<string?> Type { get; init; }

    public Optional<string?> Color { get; init; }

    public Optional<Guid?> ActorUserId { get; init; }

    /// <summary>The headcount of a lane of people (3.0, ADR 085), or null to clear it.</summary>
    public Optional<int?> Headcount { get; init; }

    public Dictionary<string, string?>? Translations { get; init; }
}

/// <summary>Reads the bodies of the lane routes into the requests of the domain and runs their commands.</summary>
public sealed class LaneApi
{
    private readonly CommandRunner runner;
    private readonly LanguageSettings languages;

    public LaneApi(CommandRunner runner, LanguageSettings languages)
    {
        this.runner = runner;
        this.languages = languages;
    }

    public async Task<StructureReply> AddAsync(Guid mapId, AddLaneBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var request = new AddLaneRequest(mapId, body.Name, body.Type, body.Color, body.SortOrder, body.ActorUserId, body.Translations)
        {
            Languages = languages,
        };
        var outcome = await runner.RunAsync(new AddLaneCommand(request), cancellationToken);
        return StructureReply.Of(outcome, outcome.Result.Id, outcome.Result.Code);
    }

    public async Task<StructureReply> UpdateAsync(Guid laneId, UpdateLaneBody body, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var request = new UpdateLaneRequest(laneId)
        {
            Name = ApiInput.Change(body.Name),
            Type = ApiInput.Change(body.Type),
            Color = ApiInput.Change(body.Color),
            ActorUserId = ApiInput.Change(body.ActorUserId),
            Headcount = ApiInput.Change(body.Headcount),
            Translations = body.Translations,
            Languages = languages,
        };
        var outcome = await runner.RunAsync(UpdateLaneCommand.WithVersion(request, ApiInput.Version(ifMatch)), cancellationToken);
        return StructureReply.Of(outcome, outcome.Result.Id, outcome.Result.Code);
    }

    /// <summary>`PUT /api/maps/{id}/lane-order`: the body is the complete list of the lanes, as in the 1.x.</summary>
    public async Task<StructureReply> ReorderAsync(Guid mapId, IReadOnlyList<Guid>? order, CancellationToken cancellationToken)
    {
        var outcome = await runner.RunAsync(new ReorderLanesCommand(mapId, order), cancellationToken);
        return StructureReply.Of(outcome, mapId, ids: outcome.Result);
    }

    /// <summary>`DELETE /api/lanes/{id}?steps=move|delete&amp;to=`.</summary>
    public async Task<StructureReply> DeleteAsync(Guid laneId, string? steps, Guid? to, string? ifMatch, CancellationToken cancellationToken)
    {
        var request = new DeleteLaneRequest(laneId, ApiInput.Code<LaneContentChoice>(steps, "steps"), to);
        var outcome = await runner.RunAsync(DeleteLaneCommand.WithVersion(request, ApiInput.Version(ifMatch)), cancellationToken);
        return StructureReply.Of(outcome, outcome.Result);
    }
}
