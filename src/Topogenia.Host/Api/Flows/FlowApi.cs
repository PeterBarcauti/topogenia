// FlowApi.cs
// The routes of the flows turned into their commands: create, relabel, reverse, delete, command bar, manual translations (annex A 1 liens, A 2.6, AR-155).

using Topogenia.Application.Commands;
using Topogenia.Application.Flows;
using Topogenia.Application.Translations;
using Topogenia.Domain.Flows;
using Topogenia.Domain.Values;

namespace Topogenia.Host.Api.Flows;

/// <summary>`POST /api/maps/{id}/flows` (1.x `LienCreation`: `source_etape_id`, `cible_etape_id`, `libelle`).</summary>
public sealed record CreateFlowBody(Guid? SourceStepId, Guid? TargetStepId, string? Label);

/// <summary>`PATCH /api/flows/{id}` (1.x `libelle`): the label, a null or blank label removes it.</summary>
public sealed record RelabelFlowBody(string? Label);

/// <summary>`PUT /api/flows/{id}/share` (3.0, ADR 085): the share of the cases of an exit, in percent, or null to clear it.</summary>
public sealed record ShareFlowBody(decimal? Share);

/// <summary>`POST /api/maps/{id}/command` (1.x `CommandeDemande`: `texte`).</summary>
public sealed record CommandBarBody(string? Text);

/// <summary>`PUT /api/flows/{id}/translations` (new in 2.0): the label by language, a null text deletes the translation.</summary>
public sealed record FlowTranslationsBody(Dictionary<string, string?>? Translations);

/// <summary>Reads the bodies of the flow routes into the requests of the domain and runs their commands.</summary>
public sealed class FlowApi
{
    private readonly CommandRunner runner;
    private readonly LanguageSettings languages;

    public FlowApi(CommandRunner runner, LanguageSettings languages)
    {
        this.runner = runner;
        this.languages = languages;
    }

    public async Task<StructureReply> CreateAsync(Guid mapId, CreateFlowBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var request = new CreateFlowRequest(mapId, ApiInput.Required(body.SourceStepId, "source_step_id"),
            ApiInput.Required(body.TargetStepId, "target_step_id"), body.Label);
        var outcome = await runner.RunAsync(new CreateFlowCommand(request), cancellationToken);
        return StructureReply.Of(outcome, outcome.Result.Id);
    }

    public async Task<StructureReply> RelabelAsync(Guid flowId, RelabelFlowBody body, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var request = new RelabelFlowRequest(flowId, body.Label);
        var outcome = await runner.RunAsync(RelabelFlowCommand.WithVersion(request, ApiInput.Version(ifMatch)), cancellationToken);
        return StructureReply.Of(outcome, outcome.Result.Flow.Id);
    }

    /// <summary>`POST /api/flows/{id}/reverse`: `id` is the flow, the same in the other direction (AR-155).</summary>
    public async Task<StructureReply> ShareAsync(Guid flowId, ShareFlowBody body, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var outcome = await runner.RunAsync(ShareFlowCommand.WithVersion(new ShareFlowRequest(flowId, body.Share), ApiInput.Version(ifMatch)), cancellationToken);
        return StructureReply.Of(outcome, outcome.Result.Id);
    }

    public async Task<StructureReply> ReverseAsync(Guid flowId, string? ifMatch, CancellationToken cancellationToken)
    {
        var outcome = await runner.RunAsync(ReverseFlowCommand.WithVersion(flowId, ApiInput.Version(ifMatch)), cancellationToken);
        return StructureReply.Of(outcome, outcome.Result.Id);
    }

    /// <summary>`DELETE /api/flows/{id}`: `id` is the deleted flow (its batch is taken back by undo, AR-50).</summary>
    public async Task<StructureReply> DeleteAsync(Guid flowId, string? ifMatch, CancellationToken cancellationToken)
    {
        var outcome = await runner.RunAsync(DeleteFlowCommand.WithVersion(flowId, ApiInput.Version(ifMatch)), cancellationToken);
        return StructureReply.Of(outcome, flowId);
    }

    /// <summary>`POST /api/maps/{id}/command`: `ids` are the flows drawn, in the order of the lines.</summary>
    public async Task<StructureReply> CommandAsync(Guid mapId, CommandBarBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var outcome = await runner.RunAsync(new ApplyCommandBarCommand(mapId, body.Text), cancellationToken);
        return StructureReply.Of(outcome, mapId, ids: outcome.Result.FlowIds);
    }

    public async Task<StructureReply> TranslateAsync(Guid flowId, FlowTranslationsBody body, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var command = UpdateFlowTranslationsCommand.WithVersion(flowId, body.Translations, languages, ApiInput.Version(ifMatch));
        var outcome = await runner.RunAsync(command, cancellationToken);
        return StructureReply.Of(outcome, flowId);
    }
}
