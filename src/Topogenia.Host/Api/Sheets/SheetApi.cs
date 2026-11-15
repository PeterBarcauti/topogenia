// SheetApi.cs
// The routes of the sheet of a step turned into their requests: read, save the draft, validate, ask for a clarification (annex A 1, A 2.10).

using Topogenia.Application.Commands;
using Topogenia.Application.Queries;
using Topogenia.Application.Sheets;
using Topogenia.Domain.Sheets;

namespace Topogenia.Host.Api.Sheets;

/// <summary>A simple field of the sheet as entered (1.x `ValeurChamp`: `valeur`, `nombre`): a text or a code, or a number.</summary>
public sealed record SheetFieldBody(string? Value, decimal? Number);

/// <summary>A line of the procedure as entered (1.x `Operation`: `texte`, `outil`, `nature`).</summary>
public sealed record ProcedureLineBody(string? Text, string? Tool, string? Nature);

/// <summary>`PUT /api/steps/{id}/sheet` (1.x `FicheDemande`: `champs`, `deroule`, `texte_libre`): the complete draft.</summary>
public sealed record SaveSheetBody(Dictionary<string, SheetFieldBody?>? Fields, List<ProcedureLineBody>? Procedure, string? FreeText);

/// <summary>`POST /api/steps/{id}/sheet/clarification` (1.x `PrecisionDemande`: `texte`).</summary>
public sealed record ClarificationBody(string? Text);

/// <summary>
/// Reads the bodies of the sheet routes into the requests of the domain and runs them. Every write answers the sheet read
/// again after the command, as the 1.x answered `FicheVue`: the command does not know the visibility of the role, the read
/// does (the next version to send back in `If-Match` included).
/// </summary>
public sealed class SheetApi
{
    private readonly CommandRunner commands;
    private readonly QueryRunner queries;

    public SheetApi(CommandRunner commands, QueryRunner queries)
    {
        this.commands = commands;
        this.queries = queries;
    }

    public Task<SheetView> GetAsync(Guid stepId, CancellationToken cancellationToken) =>
        queries.RunAsync(new GetSheetQuery(stepId), cancellationToken);

    public async Task<SheetView> SaveAsync(Guid stepId, SaveSheetBody body, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var input = new SheetInput(
            body.Fields?.ToDictionary(pair => pair.Key, pair => pair.Value is { } field ? new SheetFieldValue(field.Value, field.Number) : null),
            body.Procedure?.Select(line => new ProcedureLine(line.Text, line.Tool, line.Nature)).ToList(),
            body.FreeText);
        await commands.RunAsync(SaveSheetCommand.WithVersion(stepId, input, ApiInput.Version(ifMatch)), cancellationToken);
        return await GetAsync(stepId, cancellationToken);
    }

    public async Task<SheetView> ValidateAsync(Guid stepId, string? ifMatch, CancellationToken cancellationToken)
    {
        await commands.RunAsync(ValidateSheetCommand.WithVersion(stepId, ApiInput.Version(ifMatch)), cancellationToken);
        return await GetAsync(stepId, cancellationToken);
    }

    public async Task<SheetView> RequestClarificationAsync(Guid stepId, ClarificationBody body, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        await commands.RunAsync(RequestClarificationCommand.WithVersion(stepId, body.Text, ApiInput.Version(ifMatch)), cancellationToken);
        return await GetAsync(stepId, cancellationToken);
    }

    /// <summary>`PUT /api/steps/{id}/sheet/clarification`: the question asked rewritten by the facilitator (spec 3.0 section 16).</summary>
    public async Task<SheetView> ChangeClarificationAsync(Guid stepId, ClarificationBody body, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        await commands.RunAsync(ChangeClarificationCommand.WithVersion(stepId, body.Text, ApiInput.Version(ifMatch)), cancellationToken);
        return await GetAsync(stepId, cancellationToken);
    }

    /// <summary>`DELETE /api/steps/{id}/sheet/clarification`: the question asked withdrawn by the facilitator (spec 3.0 section 16).</summary>
    public async Task<SheetView> WithdrawClarificationAsync(Guid stepId, string? ifMatch, CancellationToken cancellationToken)
    {
        await commands.RunAsync(WithdrawClarificationCommand.WithVersion(stepId, ApiInput.Version(ifMatch)), cancellationToken);
        return await GetAsync(stepId, cancellationToken);
    }
}
