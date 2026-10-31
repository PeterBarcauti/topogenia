// ProposalApi.cs
// The routes of a proposal of the assistant on the map open turned into their requests: its preview compared with the map, its application in one operation with If-Match (spec 3.0 sections 3.1-5 to 7, ADR 079).

using Topogenia.Application.Assistant;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Domain.Model;
using Topogenia.Domain.Raci;
using Topogenia.Domain.Values;

namespace Topogenia.Host.Api.Assistant;

/// <summary>
/// `POST /api/maps/{id}/assistant/preview` and `/apply`: the text of the `.pmap` proposed, and for a translation by the
/// assistant its language (ADR 083: only the translations into it are taken).
/// </summary>
public sealed record ProposalBody(string? Pmap, string? Language = null);

/// <summary>
/// Reads the `.pmap` of a proposal into the state wanted of the map (FICHIER_INVALIDE with its faults otherwise), then
/// previews it, or applies it under the structure version of the preview given by If-Match (CONFLIT_VERSION when the
/// map changed since). The application replies with the map, as every write of the map.
/// </summary>
public sealed class ProposalApi(QueryRunner queries, CommandRunner commands, IPmapFormat format, LanguageSettings languages, AppSettings settings)
{
    public Task<ProposalPreviewView> PreviewAsync(Guid mapId, ProposalBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        return queries.RunAsync(new PreviewProposalQuery(mapId, DesiredMaps.Read(format, body.Pmap, languages, RaciRules.Letters(settings)), languages)
        {
            Language = body.Language,
        }, cancellationToken);
    }

    public async Task<StructureReply> ApplyAsync(Guid mapId, ProposalBody body, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var desired = DesiredMaps.Read(format, body.Pmap, languages, RaciRules.Letters(settings));
        var outcome = await commands.RunAsync(new ApplyProposalCommand(mapId, desired, ApiInput.Version(ifMatch), languages) { Language = body.Language }, cancellationToken);
        return StructureReply.Of(outcome, mapId);
    }
}
