// MapSummaryApi.cs
// The route of the summaries of the maps turned into its command: the summaries kept from a reply of the AI, or one written by hand (plan 3.1, lot R).

using Topogenia.Application.Commands;
using Topogenia.Application.Summaries;
using Topogenia.Domain.Summaries;
using Topogenia.Domain.Values;

namespace Topogenia.Host.Api.Maps;

/// <summary>
/// One summary of `PUT /api/maps/{id}/summaries`: its map, its sentence (empty erases it), who wrote it (`manual` when
/// absent), the model of the AI, and the version of the summary the client read (0 when it read none).
/// </summary>
public sealed record MapSummaryBody(Guid? MapId, string? Text, SummaryOrigin? Origin, string? Model, int? Version);

/// <summary>`PUT /api/maps/{id}/summaries` (plan 3.1, lot R): the summaries written at once.</summary>
public sealed record MapSummariesBody(IReadOnlyList<MapSummaryBody>? Summaries);

/// <summary>Reads the body of the route of the summaries into its command; the reply carries the view of the map open.</summary>
public sealed class MapSummaryApi
{
    private readonly CommandRunner runner;

    public MapSummaryApi(CommandRunner runner)
    {
        this.runner = runner;
    }

    public async Task<StructureReply> WriteAsync(Guid mapId, MapSummariesBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var writes = ApiInput.Required(body.Summaries, "summaries")
            .Select(item => new MapSummaryWrite(new MapSummaryEntry(ApiInput.Required(item.MapId, "map_id"), item.Text, item.Origin ?? SummaryOrigin.Manual,
                item.Model), item.Version))
            .ToList();
        var outcome = await runner.RunAsync(new WriteMapSummariesCommand(mapId, writes), cancellationToken);
        return StructureReply.Of(outcome, ids: [.. writes.Select(write => write.Entry.MapId)]);
    }
}
