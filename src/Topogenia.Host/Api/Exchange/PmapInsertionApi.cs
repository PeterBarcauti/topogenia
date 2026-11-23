// PmapInsertionApi.cs
// The routes of a .pmap file inserted as a detailed step, or replacing the detail of a step (3.3, F-208), turned into their requests: the file read, previewed, then copied in one operation with If-Match.

using Topogenia.Application.Commands;
using Topogenia.Application.Exchange;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Raci;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Values;

namespace Topogenia.Host.Api.Exchange;

/// <summary>`POST /api/maps/{id}/insert-pmap/preview`: the text of the file, and the step whose detail it would replace, if any.</summary>
public sealed record InsertPmapPreviewBody(string? Content, Guid? StepId = null);

/// <summary>
/// `POST /api/maps/{id}/insert-pmap`: the text of the file, the lane of the new step, its label (the one proposed when
/// empty), the sheets taken or not; its place, a column and a row, or a step of the map it goes `before` or `after` in the
/// flow (`anchor_step_id`, `position`, after by default), or a flow of the map it goes on, cut in two (`flow_id`, which
/// prevails), the column not read then.
/// </summary>
public sealed record InsertPmapBody(string? Content, Guid? LaneId, int? Column, int? Row, string? Label, bool? WithSheets)
{
    public Guid? FlowId { get; init; }

    public Guid? AnchorStepId { get; init; }

    public string? Position { get; init; }
}

/// <summary>`POST /api/steps/{id}/replace-pmap`: the text of the file and the sheets taken or not.</summary>
public sealed record ReplaceDetailBody(string? Content, bool? WithSheets);

/// <summary>
/// Reads the file of an insertion as an opening reads it, its translations in a language that is not configured left out
/// (FICHIER_INVALIDE with its faults otherwise, FICHIER_TROP_GRAND beyond the content of an opening), every letter of the
/// RACI kept, as a copy opens again as it was; then previews it, or copies it under the structure version of the preview
/// given by If-Match. The application replies with the map, the new step and its new detailed map.
/// </summary>
public sealed class PmapInsertionApi(QueryRunner queries, CommandRunner commands, IPmapFormat format, LanguageSettings languages)
{
    public async Task<PmapInsertionPreview> PreviewAsync(Guid mapId, HttpRequest request, CancellationToken cancellationToken)
    {
        var body = await BodyAsync<InsertPmapPreviewBody>(request, typeof(PmapInsertionPreviewQuery), AccessTarget.Map(mapId), cancellationToken);
        return await queries.RunAsync(new PmapInsertionPreviewQuery(mapId, Read(body.Content), body.StepId), cancellationToken);
    }

    public async Task<StructureReply> InsertAsync(Guid mapId, HttpRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        var body = await BodyAsync<InsertPmapBody>(request, typeof(InsertPmapCommand), AccessTarget.Map(mapId), cancellationToken);
        var lane = ApiInput.Required(body.LaneId, "lane_id");
        var place = body switch
        {
            { FlowId: { } flow } => new InsertionPlace(lane, 0, 0) { Flow = flow },
            { AnchorStepId: { } anchor } => new InsertionPlace(lane, 0, 0)
            {
                Anchor = anchor, Position = body.Position is null ? InsertPosition.After : StepInsertion.ParsePosition(body.Position),
            },
            _ => new InsertionPlace(lane, ApiInput.Required(body.Column, "column"), body.Row ?? 0),
        };
        var outcome = await commands.RunAsync(new InsertPmapCommand(format, mapId, Read(body.Content), place, body.Label, body.WithSheets ?? false, languages)
        {
            StructureVersion = ApiInput.Version(ifMatch),
        }, cancellationToken);
        return StructureReply.Of(outcome, outcome.Result.StepId, outcome.Result.Code, [outcome.Result.DetailMapId]);
    }

    public async Task<StructureReply> ReplaceAsync(Guid stepId, HttpRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        var body = await BodyAsync<ReplaceDetailBody>(request, typeof(ReplaceDetailCommand), AccessTarget.Step(stepId), cancellationToken);
        var outcome = await commands.RunAsync(new ReplaceDetailCommand(format, stepId, Read(body.Content), body.WithSheets ?? false, languages)
        {
            StructureVersion = ApiInput.Version(ifMatch),
        }, cancellationToken);
        return StructureReply.Of(outcome, outcome.Result.StepId, outcome.Result.Code, [outcome.Result.DetailMapId]);
    }

    /// <summary>
    /// The JSON body of a route, read only once the right that <paramref name="request"/> declares is granted on its target
    /// (independent review of the 3.3, as the opening of a file since the audit 3.2.0): a reader, a contributor or a stranger
    /// is refused before a byte of the file is read. A body that is not JSON is REQUETE_INVALIDE, as the binding of a body says it.
    /// </summary>
    private async Task<TBody> BodyAsync<TBody>(HttpRequest http, Type request, AccessTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        if (!http.HasJsonContentType())
            throw new DomainException(ErrorCodes.InvalidRequest, RefusalKind.Invalid);
        await queries.DemandAsync(request, target, cancellationToken);
        return await http.ReadFromJsonAsync<TBody>(cancellationToken) ?? throw new DomainException(ErrorCodes.InvalidRequest, RefusalKind.Invalid);
    }

    private PmapInsertionFile Read(string? content) => format.ReadInsertion(OpenPmapCommand.Bounded(content), languages, RaciRules.KnownLetters);
}
