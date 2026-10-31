// DocumentApi.cs
// The routes of the documents of a step and of their uses turned into their requests: add, update, delete, tick, untick, "Other" (annex A 1, A 2.11).

using System.Collections.ObjectModel;
using Topogenia.Application.Commands;
using Topogenia.Application.Documents;
using Topogenia.Application.Queries;
using Topogenia.Application.Sheets;
using Topogenia.Domain.Documents;

namespace Topogenia.Host.Api.Documents;

/// <summary>
/// `POST /api/steps/{id}/documents` and `PUT .../documents/{documentId}` (1.x `DocumentDemande`: `sens`, `libelle`,
/// `adresse`, `attente_heures`, `partage_confirme`), named as the fields of the refusals of the domain.
/// </summary>
public sealed record DocumentBody(string? Direction, string? Name, string? Location, decimal? WaitHours, bool? ShareConfirmed);

/// <summary>
/// `PUT /api/steps/{id}/uses/{documentId}` (1.x `UsageDemande`: `attente_heures`, `pct_ca`, `irritant`,
/// `temps_perdu_h_mois`), named as the fields of the refusals of the domain.
/// </summary>
public sealed record DocumentUseBody(decimal? WaitHours, int? PercentCompleteAccurate, string? PainPoint, decimal? LostHoursPerMonth);

/// <summary>The "Other" window: the outputs of the other steps of the map, grouped by step (1.x `DisponiblesVue[]`).</summary>
public sealed class AvailableDocuments(IList<AvailableOutputs> outputs) : ReadOnlyCollection<AvailableOutputs>(outputs);

/// <summary>
/// Reads the bodies of the document and use routes into the requests of the domain and runs them. Every write answers the
/// sheet of the step read again after the command, as the 1.x answered `FicheVue`.
/// </summary>
public sealed class DocumentApi
{
    private readonly CommandRunner commands;
    private readonly QueryRunner queries;

    public DocumentApi(CommandRunner commands, QueryRunner queries)
    {
        this.commands = commands;
        this.queries = queries;
    }

    public async Task<SheetView> AddAsync(Guid stepId, DocumentBody body, CancellationToken cancellationToken)
    {
        await commands.RunAsync(new AddDocumentCommand(stepId, Input(body)), cancellationToken);
        return await SheetAsync(stepId, cancellationToken);
    }

    public async Task<SheetView> UpdateAsync(Guid stepId, Guid documentId, DocumentBody body, string? ifMatch, CancellationToken cancellationToken)
    {
        await commands.RunAsync(UpdateDocumentCommand.WithVersion(stepId, documentId, Input(body), ApiInput.Version(ifMatch)), cancellationToken);
        return await SheetAsync(stepId, cancellationToken);
    }

    public async Task<SheetView> DeleteAsync(Guid stepId, Guid documentId, string? ifMatch, CancellationToken cancellationToken)
    {
        await commands.RunAsync(DeleteDocumentCommand.WithVersion(stepId, documentId, ApiInput.Version(ifMatch)), cancellationToken);
        return await SheetAsync(stepId, cancellationToken);
    }

    public async Task<AvailableDocuments> AvailableAsync(Guid stepId, CancellationToken cancellationToken) =>
        new([.. await queries.RunAsync(new GetAvailableDocumentsQuery(stepId), cancellationToken)]);

    public async Task<SheetView> UseAsync(Guid stepId, Guid documentId, DocumentUseBody body, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var input = new DocumentUseInput(body.WaitHours, body.PercentCompleteAccurate, body.PainPoint, body.LostHoursPerMonth);
        await commands.RunAsync(UseDocumentCommand.WithVersion(stepId, documentId, input, ApiInput.Version(ifMatch)), cancellationToken);
        return await SheetAsync(stepId, cancellationToken);
    }

    public async Task<SheetView> StopUsingAsync(Guid stepId, Guid documentId, string? ifMatch, CancellationToken cancellationToken)
    {
        await commands.RunAsync(StopUsingDocumentCommand.WithVersion(stepId, documentId, ApiInput.Version(ifMatch)), cancellationToken);
        return await SheetAsync(stepId, cancellationToken);
    }

    private static DocumentInput Input(DocumentBody body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return new DocumentInput(body.Direction, body.Name, body.Location, body.WaitHours, body.ShareConfirmed);
    }

    private Task<SheetView> SheetAsync(Guid stepId, CancellationToken cancellationToken) =>
        queries.RunAsync(new GetSheetQuery(stepId), cancellationToken);
}
