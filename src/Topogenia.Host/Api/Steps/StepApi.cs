// StepApi.cs
// The routes of the steps turned into their commands: create, update, delete, insert, detail, manual translations (annex A 1 etapes, A 2.4).

using Topogenia.Application.Commands;
using Topogenia.Application.Steps;
using Topogenia.Application.SubMaps;
using Topogenia.Application.Translations;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Structure;
using Topogenia.Domain.SubMaps;
using Topogenia.Domain.Values;
using Topogenia.Host.Json;

namespace Topogenia.Host.Api.Steps;

/// <summary>`POST /api/maps/{id}/steps` (1.x `EtapeDemande`: `couloir_id`, `colonne`, `libelle`, `type`, `rangee`).</summary>
public sealed record CreateStepBody(Guid? LaneId, int? Column, string? Label, string? Type, int? Row);

/// <summary>
/// `PATCH /api/steps/{id}` (1.x `libelle`, `type`, `etat_automatisation`, `assigne_a`, `couloir_id`, `colonne`, `rangee`):
/// only the fields present change; a null assignee removes him, a null lane, column or row is CHAMP_OBLIGATOIRE.
/// </summary>
public sealed record UpdateStepBody
{
    public Optional<string?> Label { get; init; }

    public Optional<string?> Type { get; init; }

    public Optional<string?> AutomationStatus { get; init; }

    public Optional<Guid?> AssigneeUserId { get; init; }

    public Optional<Guid?> LaneId { get; init; }

    public Optional<int?> Column { get; init; }

    public Optional<int?> Row { get; init; }

    /// <summary>The incoming volume of a start step (3.0, ADR 085), or null to clear it.</summary>
    public Optional<VolumeBody?> Volume { get; init; }
}

/// <summary>`POST /api/steps/{id}/insert` (1.x `InsertionDemande`: `position` before, after, below, above, parallel; `libelle`, `type`).</summary>
/// <summary>The incoming volume of a start step: a quantity and a period code (`day`, `week`, `month`, `year`).</summary>
public sealed record VolumeBody(decimal? Quantity, string? Period);

public sealed record InsertStepBody(string? Position, string? Label, string? Type);

/// <summary>`PUT /api/steps/{id}/translations` (new in 2.0): the label by language, a null text deletes the translation.</summary>
public sealed record StepTranslationsBody(Dictionary<string, string?>? Translations);

/// <summary>Reads the bodies of the step routes into the requests of the domain and runs their commands.</summary>
public sealed class StepApi
{
    /// <summary>The codes of the `sub_map` query string (1.x `sous_carte=supprimer|rattacher`, table of the routes).</summary>
    private static readonly IReadOnlyDictionary<string, SubMapChoice> SubMapCodes = new Dictionary<string, SubMapChoice>(StringComparer.Ordinal)
    {
        ["delete"] = SubMapChoice.Delete,
        ["reattach"] = SubMapChoice.Attach,
    };

    private readonly CommandRunner runner;
    private readonly LanguageSettings languages;

    public StepApi(CommandRunner runner, LanguageSettings languages)
    {
        this.runner = runner;
        this.languages = languages;
    }

    public async Task<StructureReply> CreateAsync(Guid mapId, CreateStepBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var request = new CreateStepRequest(mapId, ApiInput.Required(body.LaneId, "lane_id"), ApiInput.Required(body.Column, "column"), body.Label,
            body.Type, body.Row);
        var outcome = await runner.RunAsync(new CreateStepCommand(request), cancellationToken);
        return StructureReply.Of(outcome, outcome.Result.Id, outcome.Result.Code);
    }

    public async Task<StructureReply> UpdateAsync(Guid stepId, UpdateStepBody body, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var request = new UpdateStepRequest(stepId)
        {
            Label = ApiInput.Change(body.Label),
            Type = ApiInput.Change(body.Type),
            AutomationStatus = ApiInput.Change(body.AutomationStatus),
            AssigneeUserId = ApiInput.Change(body.AssigneeUserId),
            LaneId = RequiredChange(body.LaneId, "lane_id"),
            Column = RequiredChange(body.Column, "column"),
            Row = RequiredChange(body.Row, "row"),
            Volume = body.Volume.IsPresent
                ? new FieldChange<VolumeEntry?>(body.Volume.Value is { } volume ? new VolumeEntry(volume.Quantity, volume.Period) : null)
                : null,
        };
        var outcome = await runner.RunAsync(UpdateStepCommand.WithVersion(request, ApiInput.Version(ifMatch)), cancellationToken);
        return StructureReply.Of(outcome, outcome.Result.Id, outcome.Result.Code);
    }

    /// <summary>`DELETE /api/steps/{id}?sub_map=delete|reattach&amp;to=&amp;stitch=`: `id` is the deleted step.</summary>
    public async Task<StructureReply> DeleteAsync(Guid stepId, string? subMap, Guid? to, bool? stitch, string? ifMatch, CancellationToken cancellationToken)
    {
        var command = DeleteStepCommand.WithVersion(stepId, SubMapChoiceOf(subMap), to, stitch ?? false, ApiInput.Version(ifMatch));
        var outcome = await runner.RunAsync(command, cancellationToken);
        return StructureReply.Of(outcome, outcome.Result);
    }

    /// <summary>`POST /api/steps/{id}/insert`: a position outside the list, or none, is VALEUR_INVALIDE on `position` (1.x `ParmiListe`).</summary>
    public async Task<StructureReply> InsertAsync(Guid stepId, InsertStepBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var request = new InsertStepRequest(stepId, StepInsertion.ParsePosition(body.Position), body.Label, body.Type);
        var outcome = await runner.RunAsync(new InsertStepCommand(request), cancellationToken);
        return StructureReply.Of(outcome, outcome.Result.Id, outcome.Result.Code);
    }

    /// <summary>`POST /api/steps/{id}/detail`: `id` and `ids` name the sub-map to open, the warnings come with the outcome.</summary>
    public async Task<StructureReply> DetailAsync(Guid stepId, string? ifMatch, CancellationToken cancellationToken)
    {
        var outcome = await runner.RunAsync(DetailStepCommand.WithVersion(stepId, ApiInput.Version(ifMatch)), cancellationToken);
        return StructureReply.Of(outcome, outcome.Result.MapId, ids: [outcome.Result.MapId]);
    }

    public async Task<StructureReply> TranslateAsync(Guid stepId, StepTranslationsBody body, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var command = UpdateStepTranslationsCommand.WithVersion(stepId, body.Translations, languages, ApiInput.Version(ifMatch));
        var outcome = await runner.RunAsync(command, cancellationToken);
        return StructureReply.Of(outcome, stepId);
    }

    /// <summary>The change of a field that the domain cannot do without: absent, nothing; present and null, CHAMP_OBLIGATOIRE.</summary>
    private static FieldChange<T>? RequiredChange<T>(Optional<T?> field, string name)
        where T : struct =>
        field.IsPresent ? new FieldChange<T>(ApiInput.Required(field.Value, name)) : null;

    private static SubMapChoice? SubMapChoiceOf(string? code)
    {
        if (string.IsNullOrEmpty(code))
            return null;
        return SubMapCodes.TryGetValue(code, out var choice) ? choice : throw DomainException.InvalidValue("sub_map");
    }
}
