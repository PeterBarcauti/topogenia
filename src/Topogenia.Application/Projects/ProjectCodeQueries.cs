// ProjectCodeQueries.cs
// The code proposed from a name and the free code of a project (3.4, lot 3, ADR 111 points 6 and 7): read only, the administrator alone.

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Projects;

/// <summary>The reply of a proposal: the code, or null when the name gives none.</summary>
public sealed record ProposedProjectCode(string? Code);

/// <summary>
/// `POST /api/projects/code-proposal` (3.4, lot 3, admin): the code proposed from the name of a new project
/// (<see cref="ProjectCodeProposal.FromName"/>), then the first free one among the live projects and the trash
/// (<see cref="FreeProjectCodeQuery"/>). Read only: the codes read once, nothing written, neither operation log nor audit.
/// VALEUR_INVALIDE on `name` beyond <see cref="NameMax"/> characters. A name that gives no code of three letters at least
/// gives no proposal (« RH »): the codes are then not read.
/// </summary>
[RequiresAdmin]
public sealed class ProposeProjectCodeQuery(string? name) : UserRequest<ProposedProjectCode>
{
    /// <summary>The longest name read, in characters.</summary>
    public const int NameMax = 200;

    public const string NameField = "name";

    public override async Task<ProposedProjectCode> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (name is { Length: > NameMax })
            throw DomainException.InvalidValue(NameField);
        if (ProjectCodeProposal.FromName(name) is not { } proposed)
            return new ProposedProjectCode(null);
        return new ProposedProjectCode(await FreeProjectCodeQuery.FirstFreeAsync(scope, proposed, cancellationToken));
    }
}

/// <summary>
/// The first free code from a code (3.4, lot 3, admin; the sample project whose code is taken, ADR 111 point 6): the code
/// itself when no project, live or in the trash, holds it; otherwise the first code of the rule of the trash,
/// <see cref="ProjectCodeRules.TrashCode"/> of the closed core, called as it is (CONGES, then CONGEA, CONGEB…). The codes
/// of the same start are read once; nothing is written. PROJET_CODE_INVALIDE for a code out of the format.
/// </summary>
[RequiresAdmin]
public sealed class FreeProjectCodeQuery(string code) : UserRequest<string>
{
    public override Task<string> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return FirstFreeAsync(scope, ProjectCode.Parse(code).Value, cancellationToken);
    }

    /// <summary>The code itself when free, else <see cref="ProjectCodeRules.TrashCode"/> on the codes that share its start.</summary>
    internal static Task<string> FirstFreeAsync(UserScope scope, string code, CancellationToken cancellationToken) =>
        FirstFreeAsync(scope, code, [], cancellationToken);

    /// <summary>
    /// The same, the codes <paramref name="reserved"/> counted as taken besides those of the database: the codes already
    /// proposed higher in the list of a collection (ADR 114 point 6), which no project holds yet.
    /// </summary>
    internal static async Task<string> FirstFreeAsync(UserScope scope, string code, IReadOnlyCollection<string> reserved, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(reserved);
        var used = (await scope.Ports.Projects.CodesStartingWithAsync(ProjectCodeRules.TrashCodePrefix(code), cancellationToken)).Concat(reserved).ToList();
        return used.Contains(code, StringComparer.OrdinalIgnoreCase) ? ProjectCodeRules.TrashCode(code, used) : code;
    }
}
