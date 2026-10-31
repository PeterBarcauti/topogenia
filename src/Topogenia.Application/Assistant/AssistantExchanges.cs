// AssistantExchanges.cs
// The exchanges of the panel of the assistant: its state for a map, the checks before anything is sent, the call to the connector, the reply validated and its proposal corrected once, the audit; the prompt copied and the reply pasted (spec 3.0 sections 3, 5, 6, 7).

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Topogenia.Application.Audit;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Raci;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Assistant;

/// <summary>
/// An exchange checked and ready to send: the connector and the settings it calls with, the prompt, the context it was
/// built from (null for a project yet to create), where the data go, whether the person sees the cost, who asks.
/// </summary>
public sealed record PreparedExchange(
    IAssistantConnector Connector,
    StoredAssistantSettings Settings,
    AssistantPrompt Prompt,
    AssistantContext? Context,
    AssistantDestinationView Destination,
    bool ShowsCost,
    Guid UserId);

/// <summary>The end of an exchange: the reply shown, or the code of its failure and the detail of the provider; its time and cost.</summary>
public sealed record AssistantExchangeResult(AssistantReplyView? Reply, string? Failure, string? Detail, int DurationMilliseconds, AssistantCostView? Cost);

/// <summary>
/// The panel of the assistant (spec 3.0 sections 3.4 to 7). Any member reads the state of the panel and asks a question
/// or an analysis; a proposal needs the facilitator; a project yet to create, the administrator. Before anything is sent:
/// a connector that calls a model (IA_INDISPONIBLE otherwise: the panel then copies the prompt), the classification of
/// the project allowed (IA_CLASSIFICATION_REFUSEE), the prompt within its bound (IA_CONTENU_TROP_GRAND). The reply is data:
/// read and bounded, its form checked against those the scope and the right admit, its `.pmap` opened by the format
/// (a proposal refused for its faults is sent back once to the model with them). Each exchange writes one event of the
/// audit, without any of its texts.
/// </summary>
public sealed class AssistantExchanges(
    QueryRunner queries,
    AssistantPorts ports,
    IPmapFormat format,
    IPackageExporter packages,
    IAuditStore audit,
    IClock clock,
    ICurrentUser currentUser,
    IAssistantInstructions instructions,
    IDocumentTextReader documents,
    AppSettings appSettings,
    LanguageSettings languages)
{
    /// <summary>The state of the panel for a map, read when it opens (`GET /api/maps/{id}/assistant`).</summary>
    public async Task<AssistantStatusView> StatusAsync(Guid mapId, CancellationToken cancellationToken)
    {
        var user = User();
        var context = await queries.RunAsync(new AssistantContextQuery(mapId, format, packages, [], withTexts: false), cancellationToken);
        var settings = (await SettingsAsync(cancellationToken)).Settings;
        return new AssistantStatusView(ModeOf(settings), Destination(settings, context.Classification, context.Scope, context.MapName),
            Allowed(settings, context.Classification), context.CanPropose, context.EmptyMap, user.IsAdmin, context.ProjectName, context.ProjectCode,
            AssistantAttachmentRules.ViewOf(AssistantFileBounds.Of(settings.Connector)));
    }

    /// <summary>An exchange on a map (or, from its root, the project) checked and built (`POST /api/maps/{id}/assistant/exchanges`).</summary>
    public async Task<PreparedExchange> PrepareAsync(Guid mapId, AssistantAsk ask, CancellationToken cancellationToken)
    {
        var (user, prompt, context, stored) = await PromptAsync(mapId, ask, cancellationToken);
        var connector = ConnectorOf(stored.Settings);
        return new PreparedExchange(connector, stored, prompt, context,
            Destination(stored.Settings, context.Classification, context.Scope, context.MapName), user.IsAdmin, user.Id);
    }

    /// <summary>
    /// An exchange that describes a project yet to create, from the home page (`POST /api/assistant/exchanges`,
    /// administrator, spec 3.0 section 3.2): no schema nor map; a new project is Internal until its properties change.
    /// </summary>
    public async Task<PreparedExchange> PrepareCreationAsync(AssistantAsk ask, CancellationToken cancellationToken)
    {
        var (user, prompt, stored) = await CreationPromptAsync(ask, cancellationToken);
        var connector = ConnectorOf(stored.Settings);
        return new PreparedExchange(connector, stored, prompt, null, Destination(stored.Settings, Classification.Internal, AssistantScope.NewProject, null),
            user.IsAdmin, user.Id);
    }

    /// <summary>
    /// The whole prompt of an exchange as one text, for the copy and paste (`POST /api/maps/{id}/assistant/prompt`): the same
    /// checks but the connector, the same text that a model would receive.
    /// </summary>
    public async Task<string> CopyPromptAsync(Guid? mapId, AssistantAsk ask, CancellationToken cancellationToken)
    {
        var prompt = mapId is { } map
            ? (await PromptAsync(map, ask, cancellationToken)).Prompt
            : (await CreationPromptAsync(ask, cancellationToken)).Prompt;
        return instructions.Render(prompt);
    }

    /// <summary>
    /// A reply pasted in the panel by the person, from the AI of its choice (`POST /api/maps/{id}/assistant/replies`): read
    /// and checked as the reply of a model, its proposal opened by the format; nothing is sent, nothing is audited.
    /// </summary>
    public async Task<AssistantExchangeResult> ReadPastedAsync(Guid? mapId, AssistantAsk ask, string? pasted, CancellationToken cancellationToken)
    {
        if (pasted is { Length: > AssistantBounds.PastedMax })
            throw DomainException.Invalid(ErrorCodes.TextTooLong, "reply", AssistantBounds.PastedMax);
        var (prompt, context) = mapId is { } map
            ? await PromptOfMapAsync(map, ask, cancellationToken)
            : ((await CreationPromptAsync(ask, cancellationToken)).Prompt, null);
        var reply = AssistantReplyReader.Read(pasted, out var reason);
        if (reply is null)
            return new AssistantExchangeResult(null, AssistantFailures.InvalidReply, reason, 0, null);
        if (!prompt.Forms.Contains(reply.Form))
            return new AssistantExchangeResult(null, AssistantFailures.FormRefused, Codes.Of(reply.Form), 0, null);
        return new AssistantExchangeResult(View(reply, Faults(reply.Pmap), context, null, 0), null, null, 0, null);
    }

    /// <summary>
    /// Sends a prepared exchange and reads its reply; its progress told as it comes. A proposal whose `.pmap` the format
    /// refuses is sent back once with its faults (the phase `correcting`). The failures of the provider and of the reply
    /// are results; the cancellation of the person is audited, then thrown.
    /// </summary>
    public async Task<AssistantExchangeResult> RunAsync(PreparedExchange prepared, IProgress<AssistantProgress> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(progress);
        var clock = Stopwatch.StartNew();
        var usage = AssistantUsage.None;
        string? model = null;
        var corrected = false;
        Task<AssistantExchangeResult> Failed(string failure, string? detail, AssistantForm? form = null) =>
            FinishAsync(prepared, new AssistantExchangeResult(null, failure, detail, Elapsed(clock), Cost(prepared, model, usage)), form, usage, corrected);
        try
        {
            var connection = ConnectionOf(prepared.Settings, out var refused);
            if (connection is null)
                return await Failed(refused!, null);
            var first = await prepared.Connector.ExchangeAsync(connection, prepared.Prompt, progress, cancellationToken);
            usage = usage.Plus(first.Usage);
            model = first.Model ?? prepared.Settings.Settings.Model;
            if (!first.Succeeded)
                return await Failed(first.Failure ?? AssistantFailures.Other, first.Detail);
            var reply = AssistantReplyReader.Read(first.Text, out var reason);
            if (reply is null)
                return await Failed(AssistantFailures.InvalidReply, reason);
            if (!prepared.Prompt.Forms.Contains(reply.Form))
                return await Failed(AssistantFailures.FormRefused, Codes.Of(reply.Form), reply.Form);
            var faults = Faults(reply.Pmap);
            if (faults.Count > 0)
            {
                progress.Report(new AssistantProgress(AssistantPhase.Correcting, null, 0));
                corrected = true;
                var correction = prepared.Prompt with
                {
                    History = [.. prepared.Prompt.History, new AssistantTurn(prepared.Prompt.Request, reply.Raw) { Attachments = prepared.Prompt.Attachments }],
                    Request = CorrectionRequest(faults),
                    Attachments = [],
                };
                var second = await prepared.Connector.ExchangeAsync(connection, correction, progress, cancellationToken);
                usage = usage.Plus(second.Usage);
                var again = second.Succeeded ? AssistantReplyReader.Read(second.Text, out _) : null;
                if (again is { Form: AssistantForm.Proposal })
                {
                    reply = again;
                    faults = Faults(again.Pmap);
                }
            }
            var cost = Cost(prepared, model, usage);
            var view = View(reply, faults, prepared.Context, cost, Elapsed(clock));
            return await FinishAsync(prepared, new AssistantExchangeResult(view, null, null, Elapsed(clock), cost), reply.Form, usage, corrected);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await FinishAsync(prepared, new AssistantExchangeResult(null, AssistantFailures.Cancelled, null, Elapsed(clock), null), null, usage, corrected);
            throw;
        }
    }

    private async Task<(CurrentUser User, AssistantPrompt Prompt, AssistantContext Context, StoredAssistantSettings Stored)> PromptAsync(
        Guid mapId, AssistantAsk ask, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ask);
        var user = User();
        var intent = AssistantAskRules.IntentOf(ask.Intent);
        var request = AssistantAskRules.RequestOf(ask.Request);
        var history = AssistantAskRules.HistoryOf(ask.History);
        var selection = AssistantAskRules.SelectionOf(ask.Selection);
        var translation = intent == AssistantIntent.Translate;
        var context = await queries.RunAsync(new AssistantContextQuery(mapId, format, packages, selection, withTexts: true) { WholeTree = translation },
            cancellationToken);
        if (intent is AssistantIntent.Create or AssistantIntent.Modify or AssistantIntent.Translate && !context.CanPropose)
            throw DomainException.Forbidden();
        var target = translation ? TargetOf(ask.Language, context.ProjectLanguage) : null;
        var stored = await SettingsAsync(cancellationToken);
        if (!Allowed(stored.Settings, context.Classification))
            throw ClassificationRefused(context.Classification);
        AssistantForm[] forms = translation
            ? [AssistantForm.Proposal, AssistantForm.Answer]
            : context.CanPropose
                ? [AssistantForm.Answer, AssistantForm.Questions, AssistantForm.Findings, AssistantForm.Proposal]
                : [AssistantForm.Answer, AssistantForm.Questions, AssistantForm.Findings];
        var files = AssistantAttachmentRules.Read(ask.Files, history.Count, AssistantFileBounds.Of(stored.Settings.Connector), documents);
        var prompt = new AssistantPrompt(intent, context.Scope, forms, context.Schema, context.SchemaFormat, Joined(history, files), context.MapName,
            context.OpenMap, context.Selection, request, stored.Settings.MaxOutputTokens)
        {
            Attachments = files.Current, Languages = new AssistantLanguages(context.ProjectLanguage, languages.Languages) { Target = target },
        };
        EnsureSize(prompt);
        return (user, prompt, context, stored);
    }

    /// <summary>
    /// The language a translation is asked into (ADR 083): a configured one (LANGUE_INVALIDE on `language`), never the
    /// language of origin of the project, which is not translated (VALEUR_INVALIDE on `language`).
    /// </summary>
    private string TargetOf(string? language, string origin)
    {
        var target = languages.Require(language, "language");
        return target == origin ? throw DomainException.InvalidValue("language") : target;
    }

    private async Task<(AssistantPrompt Prompt, AssistantContext? Context)> PromptOfMapAsync(Guid mapId, AssistantAsk ask, CancellationToken cancellationToken)
    {
        var (_, prompt, context, _) = await PromptAsync(mapId, ask, cancellationToken);
        return (prompt, context);
    }

    private async Task<(CurrentUser User, AssistantPrompt Prompt, StoredAssistantSettings Stored)> CreationPromptAsync(AssistantAsk ask, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ask);
        var user = User();
        if (!user.IsAdmin)
            throw DomainException.Forbidden();
        var request = AssistantAskRules.RequestOf(ask.Request);
        var history = AssistantAskRules.HistoryOf(ask.History);
        var stored = await SettingsAsync(cancellationToken);
        if (!Allowed(stored.Settings, Classification.Internal))
            throw ClassificationRefused(Classification.Internal);
        var files = AssistantAttachmentRules.Read(ask.Files, history.Count, AssistantFileBounds.Of(stored.Settings.Connector), documents);
        var prompt = new AssistantPrompt(AssistantIntent.Create, AssistantScope.NewProject, [AssistantForm.Questions, AssistantForm.Proposal, AssistantForm.Answer],
            null, AssistantSchemaFormat.None, Joined(history, files), null, null, [], request, stored.Settings.MaxOutputTokens)
        {
            Attachments = files.Current, Languages = new AssistantLanguages(null, languages.Languages),
        };
        EnsureSize(prompt);
        return (user, prompt, stored);
    }

    /// <summary>The former turns, each with the files joined to its request.</summary>
    private static List<AssistantTurn> Joined(IReadOnlyList<AssistantTurn> history, AssistantFiles files) =>
        history.Select((turn, index) => files.Turns.TryGetValue(index, out var joined) ? turn with { Attachments = joined } : turn).ToList();

    private CurrentUser User() => currentUser.User ?? throw new DomainException(ErrorCodes.NotAuthenticated, RefusalKind.Forbidden);

    private async Task<StoredAssistantSettings> SettingsAsync(CancellationToken cancellationToken) =>
        await ports.Settings.ReadAsync(cancellationToken) ?? new StoredAssistantSettings(AssistantSettings.Default, null);

    /// <summary>The connector that calls a model; IA_INDISPONIBLE for none or the copy and paste, or when the installation has none of the kind chosen.</summary>
    private IAssistantConnector ConnectorOf(AssistantSettings settings) =>
        ModeOf(settings) == AssistantModes.Model && ports.Of(settings.Connector) is { } connector
            ? connector
            : throw new DomainException(AssistantErrors.Unavailable, RefusalKind.Invalid);

    /// <summary>The settings and the key of the API of Anthropic unprotected for the call; null with the reason when the key is missing or unreadable.</summary>
    private AssistantConnection? ConnectionOf(StoredAssistantSettings stored, out string? refused)
    {
        refused = null;
        var settings = stored.Settings;
        if (settings.Connector != AssistantConnector.Anthropic)
            return new AssistantConnection(settings, null);
        if (stored.ProtectedSecret is not { } kept)
        {
            refused = AssistantFailures.KeyMissing;
            return null;
        }
        var secret = ports.Protector.Unprotect(kept);
        if (string.IsNullOrEmpty(secret))
        {
            refused = AssistantFailures.KeyUnreadable;
            return null;
        }
        return new AssistantConnection(settings, secret);
    }

    private static string ModeOf(AssistantSettings settings) =>
        settings.Connector is AssistantConnector.Anthropic or AssistantConnector.Bedrock ? AssistantModes.Model : AssistantModes.CopyPaste;

    private static bool Allowed(AssistantSettings settings, Classification classification) => settings.AllowedClassifications.Contains(classification);

    private static DomainException ClassificationRefused(Classification classification) =>
        new(AssistantErrors.ClassificationRefused, RefusalKind.Forbidden, new Dictionary<string, object?> { ["classification"] = Codes.Of(classification) });

    private static AssistantDestinationView Destination(AssistantSettings settings, Classification classification, AssistantScope scope, string? mapName) =>
        new(Codes.Of(settings.Connector), settings.Model, settings.Region, Codes.Of(classification), Codes.Of(scope), mapName);

    /// <summary>
    /// IA_CONTENU_TROP_GRAND when the parts of the prompt together exceed its bound, the texts read from the documents
    /// joined with them (a PDF or an image is bounded by its size).
    /// </summary>
    private static void EnsureSize(AssistantPrompt prompt)
    {
        long size = (prompt.Schema?.Length ?? 0) + (prompt.OpenMap?.Length ?? 0) + prompt.Request.Length
            + prompt.History.Sum(turn => (long)turn.Request.Length + turn.Reply.Length) + prompt.Selection.Sum(code => (long)code.Length + 1)
            + AttachmentsOf(prompt).Sum(attachment => (long)(attachment.Text?.Length ?? 0));
        if (size > AssistantBounds.PromptMax)
            throw new DomainException(AssistantErrors.ContentTooLarge, RefusalKind.TooLarge, new Dictionary<string, object?> { ["max"] = AssistantBounds.PromptMax });
    }

    /// <summary>The faults of the `.pmap` of a proposal, as the opening of a file reports them; none when it opens, or without a proposal.</summary>
    private List<FileError> Faults(string? pmap)
    {
        if (pmap is null)
            return [];
        try
        {
            format.Read(pmap, languages, RaciRules.Letters(appSettings));
            return [];
        }
        catch (DomainException refusal) when (refusal.Code == ErrorCodes.InvalidFile)
        {
            return refusal.Details.TryGetValue(FileError.DetailsKey, out var errors) && errors is IEnumerable<FileError> list
                ? list.ToList()
                : [new FileError("", ErrorCodes.InvalidFile, new Dictionary<string, object?>())];
        }
        catch (DomainException refusal)
        {
            return [new FileError("", refusal.Code, refusal.Details)];
        }
    }

    /// <summary>The request that sends a proposal back to the model: the faults found by the format, each at its place in the file.</summary>
    private static string CorrectionRequest(IReadOnlyList<FileError> faults)
    {
        var text = new StringBuilder("The application refused the .pmap of your proposal. Its faults, each at its path in the file:\n");
        foreach (var fault in faults)
        {
            text.Append("- ").Append(fault.Path.Length == 0 ? "(file)" : fault.Path).Append(": ").Append(fault.Code);
            if (fault.Values.Count > 0)
                text.Append(' ').Append(JsonSerializer.Serialize(fault.Values));
            text.Append('\n');
        }
        text.Append("Correct them following the guide of the format and give the whole proposal again, in the same form.");
        return text.ToString();
    }

    /// <summary>
    /// The reply as the panel shows it: its proposal with what it holds and its faults, the objects of the project it names
    /// (cited, in question in a finding, or written in its texts), each once.
    /// </summary>
    private AssistantReplyView View(AssistantReply reply, IReadOnlyList<FileError> faults, AssistantContext? context, AssistantCostView? cost, int duration)
    {
        AssistantProposalView? proposal = null;
        if (reply.Pmap is { } pmap)
        {
            var document = faults.Count == 0 ? format.Read(pmap, languages, RaciRules.Letters(appSettings)) : null;
            var counts = document?.Counts ?? new PmapCounts(0, 0, 0, 0, 0);
            proposal = new AssistantProposalView(pmap, document?.ProjectCode, document?.ProjectName ?? "", counts.Maps, counts.Lanes, counts.Steps, counts.Flows,
                counts.Sheets, document?.Uncertainties ?? [], faults.Select(fault => new AssistantFaultView(fault.Path, fault.Code, "", fault.Values)).ToList());
        }
        var objects = new List<AssistantObjectView>();
        if (context is not null)
        {
            var named = reply.Cited.Concat(reply.Findings.SelectMany(finding => finding.Objects))
                .Concat(Texts(reply).SelectMany(text => AssistantReplyReader.CodePattern().Matches(text).Select(match => match.Value)));
            foreach (var code in named.Distinct(StringComparer.Ordinal))
            {
                if (objects.Count == AssistantBounds.ObjectsMax)
                    break;
                if (context.Objects.TryGetValue(code, out var found))
                    objects.Add(found);
            }
        }
        return new AssistantReplyView(Codes.Of(reply.Form), reply.Message, reply.Questions, reply.Findings, proposal, objects, reply.Raw, cost, duration);
    }

    private static IEnumerable<string> Texts(AssistantReply reply) =>
        new[] { reply.Message }.Concat(reply.Questions.Select(question => question.Question))
            .Concat(reply.Findings.SelectMany(finding => new[] { finding.Title, finding.Detail, finding.Suggestion ?? "" }));

    /// <summary>The cost for the administrator: the tokens always, the dollars at the prices of the API of Anthropic for its connector only.</summary>
    private static AssistantCostView? Cost(PreparedExchange prepared, string? model, AssistantUsage usage)
    {
        if (!prepared.ShowsCost || model is null)
            return null;
        var dollars = prepared.Settings.Settings.Connector == AssistantConnector.Anthropic ? AssistantPrices.Estimate(model, usage) : null;
        return new AssistantCostView(model, usage.InputTokens, usage.CacheReadTokens, usage.CacheWriteTokens, usage.OutputTokens, dollars);
    }

    /// <summary>Writes the event of the exchange (spec 3.0 section 6: who, when, the connector and the model, the scope, the tokens, the outcome, the number and the kinds of the files joined; never a text nor a file).</summary>
    private async Task<AssistantExchangeResult> FinishAsync(PreparedExchange prepared, AssistantExchangeResult result, AssistantForm? form, AssistantUsage usage,
        bool corrected)
    {
        var settings = prepared.Settings.Settings;
        var details = JsonSerializer.Serialize(new
        {
            connector = Codes.Of(settings.Connector), model = settings.Model, scope = Codes.Of(prepared.Prompt.Scope), intent = Codes.Of(prepared.Prompt.Intent),
            form = form is { } shown ? Codes.Of(shown) : null, succeeded = result.Failure is null, failure = result.Failure, corrected,
            duration_ms = result.DurationMilliseconds, input_tokens = usage.InputTokens, cache_read_tokens = usage.CacheReadTokens,
            cache_write_tokens = usage.CacheWriteTokens, output_tokens = usage.OutputTokens,
            files = AttachmentsOf(prepared.Prompt).Count(), file_kinds = AttachmentsOf(prepared.Prompt).Select(file => Codes.Of(file.Kind)).Distinct().Order().ToList(),
        });
        // Written alone, outside any unit of work, like the event of an export: the exchange writes nothing else; written
        // even when the person stopped the exchange, whose request is cancelled.
        await audit.AppendAsync(AuditTrail.OfAssistantExchange(clock.UtcNow, prepared.UserId, prepared.Context?.ProjectId, details), CancellationToken.None);
        return result;
    }

    private static int Elapsed(Stopwatch clock) => (int)Math.Min(int.MaxValue, clock.ElapsedMilliseconds);

    /// <summary>Every file the prompt sends: those of the former turns, then those of its request.</summary>
    private static IEnumerable<AssistantAttachment> AttachmentsOf(AssistantPrompt prompt) =>
        prompt.History.SelectMany(turn => turn.Attachments).Concat(prompt.Attachments);
}
