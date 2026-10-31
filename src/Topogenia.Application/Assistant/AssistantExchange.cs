// AssistantExchange.cs
// The exchange of the panel of the assistant: its practices, scopes, forms and phases, what the person asks, what the panel shows, the bounds of what is sent (spec 3.0 sections 3.4, 6, 7).

using System.Text.Json.Serialization;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;

namespace Topogenia.Application.Assistant;

/// <summary>
/// The practice chosen by a tile of the panel (spec 3.0 section 3.4), or none when the person writes directly: the model
/// then chooses the form of its reply among those the scope and the right admit.
/// </summary>
public enum AssistantIntent { Free, Create, Modify, Analyze, Ask, Translate }

/// <summary>What the exchange acts on: the map open, the whole project from its root map, or a project yet to create (from the home page).</summary>
public enum AssistantScope { Map, Project, NewProject }

/// <summary>The four forms of a reply (spec 3.0 section 3.4): an answer, questions, findings, a proposal.</summary>
public enum AssistantForm { Answer, Questions, Findings, Proposal }

/// <summary>
/// The format of the schema of the project given to the model: the entries of the package without the people for who may
/// export it, the `.pmap` of the whole project with the sheets as a reader sees them otherwise (spec 5.6-1: a reader never
/// sees a draft, which the package holds); none for a project yet to create.
/// </summary>
public enum AssistantSchemaFormat { None, Package, Pmap }

/// <summary>Where the model is: waiting for its first word, thinking, writing its reply, correcting a proposal refused by the format.</summary>
public enum AssistantPhase { Waiting, Thinking, Writing, Correcting }

/// <summary>The kinds of the findings of an analysis (spec 3.0 section 3.4), `other` for the rest.</summary>
public enum FindingKind { Wait, ReEntry, RepeatedTask, NoOwner, NoSheet, Loop, Break, Other }

/// <summary>
/// What the person asks: the practice of the tile (`free` without one), the text of the request, the codes of the steps
/// selected on the map, and the former turns of the panel, which the page keeps (spec 3.0 section 3.1-8: never kept by the
/// host); the files of the conversation, each with the turn it was joined to (lot 4).
/// </summary>
public sealed record AssistantAsk(string? Intent, string? Request, IReadOnlyList<string>? Selection, IReadOnlyList<AssistantTurn>? History)
{
    public IReadOnlyList<AssistantFileInput>? Files { get; init; }

    /// <summary>The language a translation is asked into (practice `translate`, ADR 083): a configured one, not the language of origin.</summary>
    public string? Language { get; init; }
}

/// <summary>A question of the model, with the choices it offers (none for a free answer).</summary>
public sealed record AssistantQuestionView(string Question, IReadOnlyList<string> Choices);

/// <summary>A finding of an analysis: its kind, title and detail, the codes of the objects in question, the suggestion of the model.</summary>
public sealed record AssistantFindingView(string Kind, string Title, string Detail, IReadOnlyList<string> Objects, string? Suggestion);

/// <summary>
/// An object of the project named by a reply, by its code: a step or a lane, the map it lives on, its identifier; the panel
/// selects it and shows it on its map when the code is clicked.
/// </summary>
public sealed record AssistantObjectView(string Code, string Kind, Guid MapId, Guid Id, string Label);

/// <summary>
/// A fault of the `.pmap` of a proposal, as the opening of a file reports it (A-1.2-19): its place in the file and its code;
/// the host writes the message in the language of the person from the values of the fault.
/// </summary>
public sealed record AssistantFaultView(string Path, string Code, string Message, [property: JsonIgnore] IReadOnlyDictionary<string, object?>? Values = null);

/// <summary>
/// The proposal of the model (spec 3.0 section 7): the `.pmap` 1.1 of the state wanted, what it holds, the points it asks a
/// person to check, and the faults that made the format refuse it after the correction asked (empty when it opens).
/// </summary>
public sealed record AssistantProposalView(
    string Pmap,
    string? ProjectCode,
    string ProjectName,
    int Maps,
    int Lanes,
    int Steps,
    int Flows,
    int Sheets,
    IReadOnlyList<string> Uncertainties,
    IReadOnlyList<AssistantFaultView> Faults);

/// <summary>
/// The tokens of an exchange, and its cost estimated at the public prices of the API of Anthropic (null for another
/// connector or an unknown model), shown to the administrator only (spec 3.0 section 3.4).
/// </summary>
public sealed record AssistantCostView(string Model, int InputTokens, int CacheReadTokens, int CacheWriteTokens, int OutputTokens, decimal? EstimatedUsd);

/// <summary>
/// A reply as the panel shows it, of one of the four forms: the message of the model (the answer, or the words that open
/// its questions, findings or proposal), its questions, findings or proposal, the objects of the project it names; the
/// JSON it wrote, that the page gives back with the next turns; its cost for the administrator; its time.
/// </summary>
public sealed record AssistantReplyView(
    string Form,
    string Message,
    IReadOnlyList<AssistantQuestionView> Questions,
    IReadOnlyList<AssistantFindingView> Findings,
    AssistantProposalView? Proposal,
    IReadOnlyList<AssistantObjectView> Objects,
    string Raw,
    AssistantCostView? Cost,
    int DurationMilliseconds);

/// <summary>
/// Where the data of an exchange go (spec 3.0 section 6: the panel says it in one line): the connector, its model and
/// region, the classification of the project, the scope and its map.
/// </summary>
public sealed record AssistantDestinationView(string Connector, string? Model, string? Region, string? Classification, string Scope, string? MapName);

/// <summary>
/// The state of the assistant for a map, read when the panel opens: a model to call or the copy and paste (the connector
/// None or Copier-coller), whether the classification of the project may be sent, what the person may ask of it (a
/// proposal needs the facilitator), whether the map is empty (the tiles then offer to create), and the selection
/// admitted.
/// </summary>
public sealed record AssistantStatusView(
    string Mode,
    AssistantDestinationView Destination,
    bool Allowed,
    bool CanPropose,
    bool EmptyMap,
    bool ShowsCost,
    string ProjectName,
    string ProjectCode,
    AssistantFileBoundsView Files);

/// <summary>The modes of the panel: a model called by the host, or the prompt copied into the AI of the person.</summary>
public static class AssistantModes
{
    public const string Model = "model";

    public const string CopyPaste = "copy_paste";
}

/// <summary>The codes of the refusals of the assistant before anything is sent (spec 3.0 sections 3.3, 6), with a message in each language of the host.</summary>
public static class AssistantErrors
{
    /// <summary>No connector that calls a model is chosen: the panel works by copy and paste.</summary>
    public const string Unavailable = "IA_INDISPONIBLE";

    /// <summary>The classification of the project may not be sent to the AI (spec 3.0 section 6).</summary>
    public const string ClassificationRefused = "IA_CLASSIFICATION_REFUSEE";

    /// <summary>What would be sent exceeds the bound of a prompt (spec 3.0 section 6: hostile inputs).</summary>
    public const string ContentTooLarge = "IA_CONTENU_TROP_GRAND";

    /// <summary>More files than a conversation admits.</summary>
    public const string TooManyFiles = "IA_FICHIERS_TROP_NOMBREUX";

    /// <summary>The files of a conversation together exceed their bound.</summary>
    public const string FilesTooLarge = "IA_FICHIERS_TROP_LOURDS";

    /// <summary>A file exceeds the bound of its kind for the connector.</summary>
    public const string FileTooLarge = "IA_FICHIER_TROP_GROS";

    /// <summary>A file of a kind the assistant does not read.</summary>
    public const string FileFormat = "IA_FICHIER_FORMAT";

    /// <summary>A file in a former format of Office (`.doc`, `.xls`, `.vsd`): to save in the recent one.</summary>
    public const string OutdatedFile = "IA_FICHIER_ANCIEN_FORMAT";

    /// <summary>A file empty, damaged, or whose content is not what its name says.</summary>
    public const string UnreadableFile = "IA_FICHIER_ILLISIBLE";

    /// <summary>A PDF protected by a password, which the providers refuse.</summary>
    public const string ProtectedFile = "IA_FICHIER_PROTEGE";
}

/// <summary>The bounds of what an exchange sends and of what the panel reads back (spec 3.0 section 6).</summary>
public static class AssistantBounds
{
    /// <summary>The longest request, in characters (a prompt prepared in a text file fits).</summary>
    public const int RequestMax = 100_000;

    /// <summary>The most former turns given back.</summary>
    public const int TurnsMax = 40;

    /// <summary>The longest JSON of a former reply given back (a proposal of 5 000 steps fits).</summary>
    public const int ReplyMax = 4 * 1024 * 1024;

    /// <summary>The most codes selected.</summary>
    public const int SelectionMax = 5000;

    /// <summary>
    /// The longest prompt in characters, all its parts together (about 700 000 tokens, within the context of the models of
    /// the list, the room of the reply kept).
    /// </summary>
    public const int PromptMax = 2_400_000;

    /// <summary>The longest reply pasted in the panel (the copy and paste).</summary>
    public const int PastedMax = ReplyMax;

    /// <summary>
    /// The longest text read from a Word, Excel or Visio file, in characters (about 100 000 tokens): beyond, the text is
    /// cut and the model told so.
    /// </summary>
    public const int FileTextMax = 400_000;

    public const int MessageMax = 20_000;
    public const int QuestionsMax = 12;
    public const int QuestionMax = 1000;
    public const int ChoicesMax = 8;
    public const int ChoiceMax = 300;
    public const int FindingsMax = 40;
    public const int TitleMax = 300;
    public const int DetailMax = 4000;
    public const int ObjectsMax = 200;
}

/// <summary>The rules of what the person asks (spec 3.0 section 6: hostile inputs): the practice known, the texts bounded, the turns counted.</summary>
public static class AssistantAskRules
{
    /// <summary>The practice of a code, `free` when none is given; VALEUR_INVALIDE for an unknown code.</summary>
    public static AssistantIntent IntentOf(string? code) =>
        string.IsNullOrWhiteSpace(code) ? AssistantIntent.Free : Domain.Values.Codes.Parse<AssistantIntent>(code, "intent");

    /// <summary>The request trimmed: CHAMP_OBLIGATOIRE when empty, TEXTE_TROP_LONG past its bound; the control characters but the line ends and tabs removed.</summary>
    public static string RequestOf(string? request) => Text(request, "request", AssistantBounds.RequestMax)
        ?? throw DomainException.Invalid(ErrorCodes.RequiredField, "request");

    /// <summary>The former turns, each bounded; VALEUR_INVALIDE past their number.</summary>
    public static IReadOnlyList<AssistantTurn> HistoryOf(IReadOnlyList<AssistantTurn>? history)
    {
        if (history is null)
            return [];
        if (history.Count > AssistantBounds.TurnsMax)
            throw DomainException.Invalid(ErrorCodes.InvalidValue, "history", AssistantBounds.TurnsMax);
        return history.Select(turn => new AssistantTurn(
            Text(turn?.Request, "history.request", AssistantBounds.RequestMax) ?? throw DomainException.Invalid(ErrorCodes.RequiredField, "history.request"),
            Text(turn!.Reply, "history.reply", AssistantBounds.ReplyMax) ?? throw DomainException.Invalid(ErrorCodes.RequiredField, "history.reply"))).ToList();
    }

    /// <summary>The codes selected, trimmed and once each; VALEUR_INVALIDE past their number.</summary>
    public static IReadOnlyList<string> SelectionOf(IReadOnlyList<string>? selection)
    {
        if (selection is null)
            return [];
        if (selection.Count > AssistantBounds.SelectionMax)
            throw DomainException.Invalid(ErrorCodes.InvalidValue, "selection", AssistantBounds.SelectionMax);
        return selection.Where(code => !string.IsNullOrWhiteSpace(code)).Select(code => code.Trim()).Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>A text trimmed, without the control characters but the line ends and tabs; null when empty; TEXTE_TROP_LONG past its bound.</summary>
    public static string? Text(string? value, string field, int max)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
            return null;
        if (text.Length > max)
            throw DomainException.Invalid(ErrorCodes.TextTooLong, field, max);
        return text.Any(IsRemoved) ? new string(text.Where(c => !IsRemoved(c)).ToArray()) : text;
    }

    private static bool IsRemoved(char c) => char.IsControl(c) && c is not '\n' and not '\r' and not '\t';
}

/// <summary>
/// The public prices of the API of Anthropic in dollars per million tokens (spec 3.0 section 5), from which the cost of an
/// exchange is estimated for the administrator: input, output, and the reading of the cache; the writing of the cache
/// costs 1.25 times the input (cache of five minutes). An unknown model has no estimate.
/// </summary>
public static class AssistantPrices
{
    private static readonly Dictionary<string, (decimal Input, decimal Output, decimal CacheRead)> Prices = new(StringComparer.Ordinal)
    {
        ["claude-opus-5-5"] = (4m, 20m, 0.20m),
        ["claude-sonnet-5-5"] = (2m, 10m, 0.20m),
        ["claude-haiku-5-5"] = (0.10m, 0.50m, 0.01m),
        ["claude-opus-5"] = (5m, 25m, 0.50m),
        ["claude-fable-5-1"] = (10m, 50m, 0.25m),
    };

    /// <summary>The cost of the tokens of an exchange with a model, rounded to the ten-thousandth of a dollar; null for an unknown model.</summary>
    public static decimal? Estimate(string? model, AssistantUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);
        if (model is null || !Prices.TryGetValue(model, out var price))
            return null;
        var dollars = (usage.InputTokens * price.Input + usage.CacheWriteTokens * price.Input * 1.25m + usage.CacheReadTokens * price.CacheRead
            + usage.OutputTokens * price.Output) / 1_000_000m;
        return Math.Round(dollars, 4, MidpointRounding.AwayFromZero);
    }
}
