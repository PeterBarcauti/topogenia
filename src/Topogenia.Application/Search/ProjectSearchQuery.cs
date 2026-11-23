// ProjectSearchQuery.cs
// The search of a word in the whole project (ADR 110): the request, its bounds and refusals, and the views of the reply.

using Topogenia.Application.Authorization;
using Topogenia.Application.Documents;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Search;

/// <summary>What a result is: a step (name or code), a lane, a document, a tool of a sheet, a pain point (code or description), an action.</summary>
public enum SearchKind { Step, Lane, Document, Tool, PainPoint, Action }

/// <summary>
/// A result: its kind, its step or its lane, the text the words were found in, the language of that text (null for a code),
/// the marks of the words in it (UTF-16 units; retouch R9b: by place, never overlapping nor touching, one at least) and
/// the first of them as `start` and `length`, the place a result carried before. A document, a
/// tool, a pain point and an action carry the code and the name of their step; a pain point and an action, the status of
/// the pain point.
/// </summary>
public sealed record SearchHitView(SearchKind Kind, Guid? StepId, Guid? LaneId, string Text, string? Language, int Start, int Length,
    IReadOnlyList<TextMatch> Marks)
{
    /// <summary>A result marked: its place is its first mark.</summary>
    public static SearchHitView Of(SearchKind kind, Guid? stepId, Guid? laneId, string text, string? language, IReadOnlyList<TextMatch> marks)
    {
        ArgumentNullException.ThrowIfNull(marks);
        return new(kind, stepId, laneId, text, language, marks[0].Start, marks[0].Length, marks);
    }

    public string? StepCode { get; init; }

    public NamedText? Step { get; init; }

    public PainPointStatus? Status { get; init; }
}

/// <summary>The results of one map, with its level (1 for the root map) and its name.</summary>
public sealed record SearchMapView(Guid MapId, int Depth, NamedText Name, IReadOnlyList<SearchHitView> Hits);

/// <summary>The reply of a search: the number of results, and the first <see cref="ProjectSearchQuery.HitsMax"/> of them by map.</summary>
public sealed record ProjectSearchView(int Total, IReadOnlyList<SearchMapView> Maps);

/// <summary>
/// `POST /api/projects/{id}/search` (3.4, lot 2, Lecteur): a word in the whole project, as the member may read it. The
/// right, then the text and the language checked before anything is loaded, then the project read once without lock nor
/// writing, neither operation log nor event of audit. VALEUR_INVALIDE on `text` for a text empty after trimming or longer
/// than <see cref="TextMax"/>, on `language` for a language out of `[a-z-]{1,16}`; the map asked is put first, ignored
/// when it is unknown or out of the tree.
/// </summary>
[RequiresRole(MemberRole.Reader)]
public sealed class ProjectSearchQuery(Guid projectId, string? text, string? language, Guid? mapId)
    : ProjectQuery<ProjectSearchView>(AccessTarget.Project(projectId))
{
    /// <summary>The longest text searched, in characters.</summary>
    public const int TextMax = 100;

    /// <summary>The longest code of a language.</summary>
    public const int LanguageMax = 16;

    /// <summary>The most results returned; the total counts them all.</summary>
    public const int HitsMax = 200;

    public const string TextField = "text";

    public const string LanguageField = "language";

    public override WorkspaceContent Content => WorkspaceContent.Sheets | WorkspaceContent.Documents | WorkspaceContent.Translations;

    /// <summary>The text and the language, checked after the right and before the loading: a refused search reads nothing.</summary>
    public override void CheckRequest()
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > TextMax)
            throw DomainException.InvalidValue(TextField);
        if (language is not null && !IsLanguage(language))
            throw DomainException.InvalidValue(LanguageField);
    }

    public override ProjectSearchView Read(ProjectWorkspace workspace, QueryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        CheckRequest();
        return SearchTerms.Of(text) is { } terms
            ? ProjectSearchWalk.Run(workspace, new SearchRequest(terms, language, mapId), step => SheetRights.ViewerOf(workspace, step, context.Role, context.UserId))
            : new ProjectSearchView(0, []);
    }

    private static bool IsLanguage(string code) =>
        code.Length is >= 1 and <= LanguageMax && code.All(letter => letter is (>= 'a' and <= 'z') or '-');
}
