// PresentationComments.cs
// The comment of presentation of a step (ADR 099): one to three sentences that a presentation says instead of its own sentences of the step; one line per language, the line of the language of origin of the step being the original and the others its translations; kept beside the steps, outside the closed core; written by a facilitator, read with the map by anyone who may read it.

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Presentation;

/// <summary>
/// Where a line of a comment comes from: typed by a person, written by the assistant, or brought by a workbook. A line
/// typed is never replaced by the assistant, nor by a workbook but with its scope `all`.
/// </summary>
public enum CommentSource { Manual, Assistant, Workbook }

/// <summary>
/// A line of a comment of presentation as it is kept: its step (it follows the step from map to map, and goes with it at
/// its purge) and its language, which are its key; its text, its source, its version and its last author. The line in
/// the language of origin of the step is the original; any other line is a translation.
/// </summary>
public sealed record PresentationComment(Guid StepId, string Text, string Language, CommentSource Source, int Version, DateTime UpdatedAt, Guid UpdatedBy)
{
    /// <summary>The line as the rules shared with the comment of a flow see it (<see cref="CommentLineRules"/>): its step is its subject.</summary>
    public CommentLine AsLine() => new(StepId, Text, Language, Source, Version, UpdatedAt, UpdatedBy);

    /// <summary>The line of the comment of a step that the shared rules give.</summary>
    public static PresentationComment Of(CommentLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return new(line.SubjectId, line.Text, line.Language, line.Source, line.Version, line.UpdatedAt, line.UpdatedBy);
    }
}

/// <summary>A line of a comment as the map shows it: its step, text, language and source, its version for the If-Match of its change.</summary>
public sealed record PresentationCommentView(Guid StepId, string Text, string Language, string Source, int Version)
{
    public static PresentationCommentView Of(PresentationComment comment)
    {
        ArgumentNullException.ThrowIfNull(comment);
        return new(comment.StepId, comment.Text, comment.Language, Codes.Of(comment.Source), comment.Version);
    }

    /// <summary>The lines in a stable order: by step, then by language.</summary>
    public static IReadOnlyList<PresentationCommentView> Of(IEnumerable<PresentationComment> comments) =>
        [.. comments.OrderBy(comment => comment.StepId).ThenBy(comment => comment.Language, StringComparer.Ordinal).Select(Of)];
}

/// <summary>
/// One line to write: created or replaced by <see cref="Line"/>, or removed when it is null; <see cref="VersionRead"/> is
/// the version of the line the writer read (0 for none), checked by the store.
/// </summary>
public sealed record CommentWrite(Guid StepId, string Language, PresentationComment? Line, int VersionRead)
{
    /// <summary>The write as the rules shared with the comment of a flow see it.</summary>
    public CommentLineWrite AsLineWrite() => new(StepId, Language, Line?.AsLine(), VersionRead);

    /// <summary>The write on the comment of a step that the shared rules decide.</summary>
    public static CommentWrite Of(CommentLineWrite write)
    {
        ArgumentNullException.ThrowIfNull(write);
        return new(write.SubjectId, write.Language, write.Line is { } line ? PresentationComment.Of(line) : null, write.VersionRead);
    }
}

/// <summary>The comments of presentation beside the steps: read for some steps, written line by line in the unit of a request, or all those of an import.</summary>
public interface IPresentationCommentStore
{
    /// <summary>Every line of the comments of these steps (those without one are left out), in one consistent read or in the unit of the request.</summary>
    Task<IReadOnlyList<PresentationComment>> OfStepsAsync(IReadOnlyCollection<Guid> stepIds, CancellationToken cancellationToken);

    /// <summary>
    /// In the unit of work of the request: each line written (created or replaced) or removed, all or none; the version
    /// read of each checked (0 for none): a line changed meanwhile is CONFLIT_VERSION.
    /// </summary>
    Task WriteAsync(IReadOnlyList<CommentWrite> writes, CancellationToken cancellationToken);

    /// <summary>In the unit of work of an import: the lines of the comments of the steps it made, added after them.</summary>
    Task AddAsync(IReadOnlyList<PresentationComment> comments, CancellationToken cancellationToken);
}

/// <summary>
/// The ports of what is kept beside the closed core, given to the templates of the commands and of the queries
/// (<see cref="CommandRunner"/>, <see cref="Queries.QueryRunner"/>): a command reads through them before its domain runs
/// and writes through them in its unit of work, after its change set (ADR 099 revised): the comments of presentation of
/// the steps and those of the flows (lot J10).
/// </summary>
public sealed record BesidePorts(IPresentationCommentStore Comments, IFlowPresentationCommentStore FlowComments);

/// <summary>
/// `GET /api/maps/{id}/presentation-comments` (3.2, ADR 099, reader): every line of the comments of the live steps of a
/// live map, which a presentation reads when it starts and when it dives into a step, to say the line of the language
/// shown; INTROUVABLE for a map that is not live.
/// </summary>
[RequiresRole(MemberRole.Reader)]
public sealed class ReadPresentationCommentsQuery(Guid mapId) : UserRequest<IReadOnlyList<PresentationCommentView>>
{
    public override AccessTarget? Target => AccessTarget.Map(mapId);

    public override async Task<IReadOnlyList<PresentationCommentView>> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var workspace = await scope.Ports.Store.LoadAsync(scope.Project, WorkspaceContent.None, cancellationToken) ?? throw DomainException.NotFound();
        workspace.LiveMap(mapId);
        var steps = workspace.StepsOf(mapId).Select(step => step.Id).ToList();
        if (steps.Count == 0)
            return [];
        return PresentationCommentView.Of(await scope.Ports.Comments.OfStepsAsync(steps, cancellationToken));
    }
}

/// <summary>
/// `PUT /api/steps/{id}/presentation-comment` (3.2, ADR 099, facilitator): the line of the comment of a live step in the
/// language given, written or removed by an empty text (<see cref="PresentationCommentRules.Typed"/>: in the language of
/// origin of the step it is the original, in another one a translation typed); the version of that line read in If-Match
/// (0 when the step had none in this language). The project is locked meanwhile, so that no purge of the step nor any
/// other writer of its comment interleaves. Nothing goes in the history of the map: a comment is not undone by Ctrl+Z, it
/// is written again. The reply is every line of the step after the change.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class SetPresentationCommentRequest(Guid stepId, string? text, string? language) : UserRequest<IReadOnlyList<PresentationCommentView>>
{
    /// <summary>The If-Match of the request, when the client sent one.</summary>
    public int? Version { get; init; }

    public override AccessTarget? Target => AccessTarget.Step(stepId);

    public override bool InUnitOfWork => true;

    public override bool LocksTarget => true;

    public override async Task<IReadOnlyList<PresentationCommentView>> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var workspace = await scope.Ports.Store.LoadAsync(scope.Project, WorkspaceContent.None, cancellationToken) ?? throw DomainException.NotFound();
        var step = workspace.LiveStep(stepId);
        var said = PresentationCommentRules.Text(text);
        var spoken = PresentationCommentRules.Language(language, scope.Languages);
        var kept = await scope.Ports.Comments.OfStepsAsync([stepId], cancellationToken);
        CommentLineRules.EnsureRead(kept.FirstOrDefault(comment => comment.Language == spoken)?.AsLine(), Version, "presentation_comment");
        var writes = PresentationCommentRules.Typed(step, kept, said, spoken, scope.Operation);
        if (writes.Count > 0)
            await scope.Ports.Comments.WriteAsync(writes, cancellationToken);
        return PresentationCommentView.Of(PresentationCommentRules.After(kept, writes));
    }

    /// <summary>The If-Match of the request on the line of the comment, when the client sent one.</summary>
    public static SetPresentationCommentRequest WithVersion(Guid stepId, string? text, string? language, int? version) =>
        new(stepId, text, language) { Version = version };
}
