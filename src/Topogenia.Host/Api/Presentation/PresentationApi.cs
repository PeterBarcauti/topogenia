// PresentationApi.cs
// The routes of the comments of presentation turned into their requests (ADR 099 revised): the lines of those of the steps and of the flows of a map read, the line of a step or of a flow in a language written with If-Match; and those of the voices a project keeps (ADR 103): read, the voice of a language kept with If-Match.

using Topogenia.Application.Commands;
using Topogenia.Application.Presentation;

namespace Topogenia.Host.Api.Presentation;

/// <summary>
/// `PUT /api/steps/{id}/presentation-comment` and `PUT /api/flows/{id}/presentation-comment`: the text said in
/// presentation and the language it is written in; in the language of origin of the step (for a flow, of its source
/// step) it is the original (empty: the comment and its translations are removed), in another one a translation (empty:
/// this translation only).
/// </summary>
public sealed record PresentationCommentBody(string? Text, string? Language);

/// <summary>Every line of the comment of the step after the change: its original and its translations; none when it has no comment left.</summary>
public sealed record PresentationCommentReply(IReadOnlyList<PresentationCommentView> Comments);

/// <summary>Every line of the comment of the flow after the change: its original and its translations; none when it has no comment left.</summary>
public sealed record FlowPresentationCommentReply(IReadOnlyList<FlowPresentationCommentView> Comments);

/// <summary>
/// `PUT /api/projects/{id}/presentation-voices/{language}` (ADR 103): the name of the voice as the browser gives it, and
/// whether it is a voice of the computer (otherwise one read online), required with a name (spec 5.4: an absent field is an
/// error, never a silent default); an empty or absent name removes the voice the project keeps for the language.
/// </summary>
public sealed record PresentationVoiceBody(string? Name, bool? Local);

/// <summary>Every voice the project keeps after the change, by language; none when it keeps no voice.</summary>
public sealed record PresentationVoiceReply(IReadOnlyList<PresentationVoiceView> Voices);

/// <summary>
/// The comments of presentation of the steps (<see cref="ReadPresentationCommentsQuery"/>, <see cref="SetPresentationCommentRequest"/>)
/// and of the flows (<see cref="ReadFlowPresentationCommentsQuery"/>, <see cref="SetFlowPresentationCommentRequest"/>), and the
/// voices a project keeps for its presentation (<see cref="ReadPresentationVoicesQuery"/>, <see cref="SetPresentationVoiceRequest"/>).
/// </summary>
public sealed class PresentationApi(UserRunner users)
{
    public Task<IReadOnlyList<PresentationCommentView>> ReadAsync(Guid mapId, CancellationToken cancellationToken) =>
        users.RunAsync(new ReadPresentationCommentsQuery(mapId), cancellationToken);

    public async Task<PresentationCommentReply> WriteAsync(Guid stepId, PresentationCommentBody body, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var request = SetPresentationCommentRequest.WithVersion(stepId, body.Text, body.Language, ApiInput.Version(ifMatch));
        return new PresentationCommentReply(await users.RunAsync(request, cancellationToken));
    }

    public Task<IReadOnlyList<FlowPresentationCommentView>> ReadFlowsAsync(Guid mapId, CancellationToken cancellationToken) =>
        users.RunAsync(new ReadFlowPresentationCommentsQuery(mapId), cancellationToken);

    public async Task<FlowPresentationCommentReply> WriteFlowAsync(Guid flowId, PresentationCommentBody body, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var request = SetFlowPresentationCommentRequest.WithVersion(flowId, body.Text, body.Language, ApiInput.Version(ifMatch));
        return new FlowPresentationCommentReply(await users.RunAsync(request, cancellationToken));
    }

    public Task<IReadOnlyList<PresentationVoiceView>> ReadVoicesAsync(Guid projectId, CancellationToken cancellationToken) =>
        users.RunAsync(new ReadPresentationVoicesQuery(projectId), cancellationToken);

    public async Task<PresentationVoiceReply> WriteVoiceAsync(Guid projectId, string language, PresentationVoiceBody body, string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var request = SetPresentationVoiceRequest.WithVersion(projectId, language, body.Name, body.Local, ApiInput.Version(ifMatch));
        return new PresentationVoiceReply(await users.RunAsync(request, cancellationToken));
    }
}
