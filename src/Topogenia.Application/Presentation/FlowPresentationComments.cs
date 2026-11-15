// FlowPresentationComments.cs
// The comment of presentation of a flow (ADR 099, lot J10): what a presentation says while it goes along the flow from a step to the next, instead of its own sentence of the handoff; one line per language, the line of the language of origin of the source step of the flow being the original and the others its translations; kept beside the flows, outside the closed core; written by a facilitator, read with the map by anyone who may read it.

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Presentation;

/// <summary>
/// A line of the comment of presentation of a flow as it is kept: its flow (it follows the flow to the trash and back,
/// and goes before it at its purge) and its language, which are its key; its text, its source, its version and its last
/// author. A flow has no language of its own: the line in the language of origin of its source step is the original; any
/// other line is a translation.
/// </summary>
public sealed record FlowPresentationComment(Guid FlowId, string Text, string Language, CommentSource Source, int Version, DateTime UpdatedAt, Guid UpdatedBy)
{
    /// <summary>The line as the rules shared with the comment of a step see it (<see cref="CommentLineRules"/>): its flow is its subject.</summary>
    public CommentLine AsLine() => new(FlowId, Text, Language, Source, Version, UpdatedAt, UpdatedBy);

    /// <summary>The line of the comment of a flow that the shared rules give.</summary>
    public static FlowPresentationComment Of(CommentLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return new(line.SubjectId, line.Text, line.Language, line.Source, line.Version, line.UpdatedAt, line.UpdatedBy);
    }
}

/// <summary>A line of the comment of a flow as the map shows it: its flow, text, language and source, its version for the If-Match of its change.</summary>
public sealed record FlowPresentationCommentView(Guid FlowId, string Text, string Language, string Source, int Version)
{
    public static FlowPresentationCommentView Of(FlowPresentationComment comment)
    {
        ArgumentNullException.ThrowIfNull(comment);
        return new(comment.FlowId, comment.Text, comment.Language, Codes.Of(comment.Source), comment.Version);
    }

    /// <summary>The lines in a stable order: by flow, then by language.</summary>
    public static IReadOnlyList<FlowPresentationCommentView> Of(IEnumerable<FlowPresentationComment> comments) =>
        [.. comments.OrderBy(comment => comment.FlowId).ThenBy(comment => comment.Language, StringComparer.Ordinal).Select(Of)];
}

/// <summary>
/// One line of the comment of a flow to write: created or replaced by <see cref="Line"/>, or removed when it is null;
/// <see cref="VersionRead"/> is the version of the line the writer read (0 for none), checked by the store.
/// </summary>
public sealed record FlowCommentWrite(Guid FlowId, string Language, FlowPresentationComment? Line, int VersionRead)
{
    /// <summary>The write as the rules shared with the comment of a step see it.</summary>
    public CommentLineWrite AsLineWrite() => new(FlowId, Language, Line?.AsLine(), VersionRead);

    /// <summary>The write on the comment of a flow that the shared rules decide.</summary>
    public static FlowCommentWrite Of(CommentLineWrite write)
    {
        ArgumentNullException.ThrowIfNull(write);
        return new(write.SubjectId, write.Language, write.Line is { } line ? FlowPresentationComment.Of(line) : null, write.VersionRead);
    }
}

/// <summary>
/// The comments of presentation beside the flows, as <see cref="IPresentationCommentStore"/> keeps those of the steps:
/// read for some flows, written line by line in the unit of a request, or all those of an import.
/// </summary>
public interface IFlowPresentationCommentStore
{
    /// <summary>Every line of the comments of these flows (those without one are left out), in one consistent read or in the unit of the request.</summary>
    Task<IReadOnlyList<FlowPresentationComment>> OfFlowsAsync(IReadOnlyCollection<Guid> flowIds, CancellationToken cancellationToken);

    /// <summary>
    /// In the unit of work of the request: each line written (created or replaced) or removed, all or none; the version
    /// read of each checked (0 for none): a line changed meanwhile is CONFLIT_VERSION.
    /// </summary>
    Task WriteAsync(IReadOnlyList<FlowCommentWrite> writes, CancellationToken cancellationToken);

    /// <summary>In the unit of work of an import: the lines of the comments of the flows it made, added after them.</summary>
    Task AddAsync(IReadOnlyList<FlowPresentationComment> comments, CancellationToken cancellationToken);
}

/// <summary>
/// `GET /api/maps/{id}/flow-presentation-comments` (3.2, ADR 099, lot J10, reader): every line of the comments of the live
/// flows of a live map, which a presentation reads with the comments of its steps, to say the line of the language shown
/// while it goes along a flow; INTROUVABLE for a map that is not live.
/// </summary>
[RequiresRole(MemberRole.Reader)]
public sealed class ReadFlowPresentationCommentsQuery(Guid mapId) : UserRequest<IReadOnlyList<FlowPresentationCommentView>>
{
    public override AccessTarget? Target => AccessTarget.Map(mapId);

    public override async Task<IReadOnlyList<FlowPresentationCommentView>> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var workspace = await scope.Ports.Store.LoadAsync(scope.Project, WorkspaceContent.None, cancellationToken) ?? throw DomainException.NotFound();
        workspace.LiveMap(mapId);
        var flows = workspace.FlowsOf(mapId).Select(flow => flow.Id).ToList();
        if (flows.Count == 0)
            return [];
        return FlowPresentationCommentView.Of(await scope.Ports.FlowComments.OfFlowsAsync(flows, cancellationToken));
    }
}

/// <summary>
/// `PUT /api/flows/{id}/presentation-comment` (3.2, ADR 099, lot J10, facilitator): the line of the comment of a live flow
/// in the language given, written or removed by an empty text (<see cref="FlowPresentationCommentRules.Typed"/>: in the
/// language of origin of the source step of the flow it is the original, in another one a translation typed); the
/// version of that line read in If-Match (0 when the flow had none in this language). The project is locked meanwhile,
/// so that no purge of the flow nor any other writer of its comment interleaves. Nothing goes in the history of the map:
/// a comment is not undone by Ctrl+Z, it is written again. A flow in the trash keeps its comment for its restoration and
/// takes none: INTROUVABLE. The reply is every line of the flow after the change.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class SetFlowPresentationCommentRequest(Guid flowId, string? text, string? language) : UserRequest<IReadOnlyList<FlowPresentationCommentView>>
{
    /// <summary>The If-Match of the request, when the client sent one.</summary>
    public int? Version { get; init; }

    public override AccessTarget? Target => AccessTarget.Flow(flowId);

    public override bool InUnitOfWork => true;

    public override bool LocksTarget => true;

    public override async Task<IReadOnlyList<FlowPresentationCommentView>> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var workspace = await scope.Ports.Store.LoadAsync(scope.Project, WorkspaceContent.None, cancellationToken) ?? throw DomainException.NotFound();
        var subject = FlowPresentationCommentRules.Subject(workspace, workspace.LiveFlow(flowId));
        var said = PresentationCommentRules.Text(text);
        var spoken = PresentationCommentRules.Language(language, scope.Languages);
        var kept = await scope.Ports.FlowComments.OfFlowsAsync([flowId], cancellationToken);
        CommentLineRules.EnsureRead(kept.FirstOrDefault(comment => comment.Language == spoken)?.AsLine(), Version, FlowPresentationCommentRules.ObjectType);
        var writes = FlowPresentationCommentRules.Typed(subject, kept, said, spoken, scope.Operation);
        if (writes.Count > 0)
            await scope.Ports.FlowComments.WriteAsync(writes, cancellationToken);
        return FlowPresentationCommentView.Of(FlowPresentationCommentRules.After(kept, writes));
    }

    /// <summary>The If-Match of the request on the line of the comment, when the client sent one.</summary>
    public static SetFlowPresentationCommentRequest WithVersion(Guid flowId, string? text, string? language, int? version) =>
        new(flowId, text, language) { Version = version };
}
