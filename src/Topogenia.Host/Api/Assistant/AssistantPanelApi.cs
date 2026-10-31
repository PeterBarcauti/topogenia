// AssistantPanelApi.cs
// The routes of the panel of the assistant turned into their requests: its state, the exchange written as a stream of JSON lines, the prompt copied, the reply pasted (spec 3.0 sections 3.4, 5, 7).

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;
using Topogenia.Host.Errors;

namespace Topogenia.Host.Api.Assistant;

/// <summary>A file joined to a request (lot 4): its name and its content in base64, kept by the page for the conversation.</summary>
public sealed record AssistantFileBody(string? Name, string? Data);

/// <summary>A former turn of the panel, kept by the page: the request of the person, the JSON the model replied, the files joined to the request.</summary>
public sealed record AssistantTurnBody(string? Request, string? Reply, List<AssistantFileBody>? Files = null);

/// <summary>
/// `POST /api/maps/{id}/assistant/exchanges` and its kin: the practice of the tile (`free` without one), the request, the
/// codes of the steps selected, the former turns of the panel, the files joined to the request, and the language of a
/// translation (practice `translate`, ADR 083).
/// </summary>
public sealed record AssistantAskBody(string? Intent, string? Request, List<string>? Selection, List<AssistantTurnBody>? History,
    List<AssistantFileBody>? Files = null, string? Language = null)
{
    /// <summary>The ask of the application, the files decoded with the turn they belong to; VALEUR_INVALIDE for a content that is no base64.</summary>
    public AssistantAsk ToAsk()
    {
        var files = (History ?? []).SelectMany((turn, index) => (turn?.Files ?? []).Select(file => Decoded(file, index)))
            .Concat((Files ?? []).Select(file => Decoded(file, null))).ToList();
        return new(Intent, Request, Selection, History?.Select(turn => new AssistantTurn(turn?.Request ?? "", turn?.Reply ?? "")).ToList())
        {
            Files = files.Count == 0 ? null : files, Language = Language,
        };
    }

    private static AssistantFileInput Decoded(AssistantFileBody? file, int? turn)
    {
        if (file?.Data is not { } data)
            return new AssistantFileInput(file?.Name, null, turn);
        try
        {
            return new AssistantFileInput(file.Name, Convert.FromBase64String(data), turn);
        }
        catch (FormatException)
        {
            throw DomainException.Invalid(ErrorCodes.InvalidValue, "files.data");
        }
    }
}

/// <summary>
/// `POST /api/maps/{id}/assistant/replies`: the exchange as for a model, and the reply pasted from the AI of the person;
/// its files are not read again (the person joined them in their AI).
/// </summary>
public sealed record AssistantPasteBody(string? Intent, string? Request, List<string>? Selection, List<AssistantTurnBody>? History, string? Reply)
{
    public AssistantAsk ToAsk() =>
        new AssistantAskBody(Intent, Request, Selection, History?.Select(turn => turn with { Files = null }).ToList()).ToAsk();
}

/// <summary>The whole prompt of an exchange, to copy into the AI of the person.</summary>
public sealed record AssistantPromptView(string Prompt);

/// <summary>The reply pasted as the panel shows it, or the reason it cannot be read.</summary>
public sealed record AssistantPastedView(AssistantReplyView? Reply, string? Failure, string? Detail);

/// <summary>
/// A line of the stream of an exchange (`application/x-ndjson`): `started` with where the data go, `progress` with the
/// phase, the last thought of the model, the characters written and the time, then one `reply` or one `failure`.
/// </summary>
public sealed record AssistantEvent(
    string Type,
    AssistantDestinationView? Destination = null,
    string? Phase = null,
    string? Thought = null,
    int? Characters = null,
    int? ElapsedMilliseconds = null,
    AssistantReplyView? Reply = null,
    string? Failure = null,
    string? Detail = null,
    AssistantCostView? Cost = null);

/// <summary>
/// The panel of the assistant (<see cref="AssistantExchanges"/>). An exchange is checked before anything is written: a
/// refusal (right, connector, classification, size) is an ordinary error. Then the reply is a stream of JSON lines: the
/// line `started`, the progress of the model, a line at least every <see cref="Heartbeat"/> so that nothing on the way (a
/// proxy, IIS) cuts a long exchange, and the reply or the failure. The person who stops the exchange closes the request:
/// the call to the model is cancelled.
/// </summary>
public sealed class AssistantPanelApi(AssistantExchanges exchanges, ErrorMessages messages, ICurrentUser currentUser, LanguageSettings languages,
    IOptions<JsonOptions> json, ILogger<AssistantPanelApi> logger)
{
    /// <summary>The media type of the stream of an exchange.</summary>
    public const string StreamType = "application/x-ndjson";

    /// <summary>The longest body of an exchange: the former turns may hold proposals, the files of the conversation go in base64 (20 MB at most, a third larger).</summary>
    public const long BodyLimit = 64L * 1024 * 1024;

    /// <summary>The longest silence of the stream.</summary>
    public static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(2);

    public Task<AssistantStatusView> StatusAsync(Guid mapId, CancellationToken cancellationToken) => exchanges.StatusAsync(mapId, cancellationToken);

    public async Task<AssistantPromptView> PromptAsync(Guid? mapId, AssistantAskBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        return new AssistantPromptView(await exchanges.CopyPromptAsync(mapId, body.ToAsk(), cancellationToken));
    }

    public async Task<AssistantPastedView> PastedAsync(Guid? mapId, AssistantPasteBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var result = await exchanges.ReadPastedAsync(mapId, body.ToAsk(), body.Reply, cancellationToken);
        return new AssistantPastedView(Messages(result.Reply), result.Failure, result.Detail);
    }

    /// <summary>An exchange on a map, or on a project yet to create when <paramref name="mapId"/> is null, written to the response as a stream.</summary>
    public async Task ExchangeAsync(Guid? mapId, AssistantAskBody body, HttpContext http, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(http);
        var prepared = mapId is { } map
            ? await exchanges.PrepareAsync(map, body.ToAsk(), cancellationToken)
            : await exchanges.PrepareCreationAsync(body.ToAsk(), cancellationToken);
        var response = http.Response;
        response.ContentType = StreamType + "; charset=utf-8";
        response.Headers.CacheControl = "no-store";
        // The lines leave as they are written: no buffer of the server, nor of a proxy that reads this header.
        response.Headers["X-Accel-Buffering"] = "no";
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        var clock = Stopwatch.StartNew();
        await WriteAsync(response, new AssistantEvent("started", Destination: prepared.Destination), cancellationToken);
        var lines = Channel.CreateUnbounded<AssistantEvent>(new UnboundedChannelOptions { SingleReader = true });
        var progress = new LineProgress(lines.Writer);
        var run = Task.Run(async () =>
        {
            try
            {
                var result = await exchanges.RunAsync(prepared, progress, cancellationToken);
                lines.Writer.TryWrite(result.Reply is { } reply
                    ? new AssistantEvent("reply", Reply: Messages(reply), Cost: result.Cost, ElapsedMilliseconds: result.DurationMilliseconds)
                    : new AssistantEvent("failure", Failure: result.Failure, Detail: result.Detail, Cost: result.Cost,
                        ElapsedMilliseconds: result.DurationMilliseconds));
                lines.Writer.TryComplete();
            }
            catch (Exception failure)
            {
                lines.Writer.TryComplete(failure);
            }
        }, CancellationToken.None);
        var last = new AssistantEvent("progress", Phase: Codes.Of(AssistantPhase.Waiting), Characters: 0);
        Task<bool>? waiting = null;
        try
        {
            while (true)
            {
                waiting ??= lines.Reader.WaitToReadAsync(cancellationToken).AsTask();
                if (await Task.WhenAny(waiting, Task.Delay(Heartbeat, cancellationToken)) != waiting)
                {
                    await WriteAsync(response, last with { ElapsedMilliseconds = Elapsed(clock) }, cancellationToken);
                    continue;
                }
                var more = await waiting;
                waiting = null;
                if (!more)
                    break;
                while (lines.Reader.TryRead(out var line))
                {
                    if (line.Type == "progress")
                        last = line;
                    await WriteAsync(response, line.ElapsedMilliseconds is null ? line with { ElapsedMilliseconds = Elapsed(clock) } : line, cancellationToken);
                }
            }
        }
        catch (Exception failure) when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // A failure of the code once the stream has started cannot become an error of HTTP: the panel reads a failure.
            logger.LogError(failure, "The exchange of the assistant failed after its start.");
            await WriteAsync(response, new AssistantEvent("failure", Failure: AssistantFailures.Other, ElapsedMilliseconds: Elapsed(clock)), CancellationToken.None);
        }
        await run;
    }

    /// <summary>The messages of the faults of a proposal, in the language of the person.</summary>
    private AssistantReplyView? Messages(AssistantReplyView? reply)
    {
        if (reply?.Proposal is not { Faults.Count: > 0 } proposal)
            return reply;
        var language = currentUser.User?.Language ?? languages.DefaultLanguage;
        var faults = proposal.Faults.Select(fault => fault with { Message = messages.Format(fault.Code, language, fault.Values) }).ToList();
        return reply with { Proposal = proposal with { Faults = faults } };
    }

    private async Task WriteAsync(HttpResponse response, AssistantEvent line, CancellationToken cancellationToken)
    {
        var text = JsonSerializer.Serialize(line, json.Value.SerializerOptions) + "\n";
        await response.Body.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    private static int Elapsed(Stopwatch clock) => (int)Math.Min(int.MaxValue, clock.ElapsedMilliseconds);

    /// <summary>The progress of the model turned into lines of the stream.</summary>
    private sealed class LineProgress(ChannelWriter<AssistantEvent> writer) : IProgress<AssistantProgress>
    {
        public void Report(AssistantProgress value) =>
            writer.TryWrite(new AssistantEvent("progress", Phase: Codes.Of(value.Phase), Thought: value.Thought, Characters: value.Characters));
    }
}
