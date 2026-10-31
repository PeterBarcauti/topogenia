// MemberCommands.cs
// The members of a project: their replacement, which cannot be undone, and the participants (annex A 1, A 2.1, annex B 2.6-44).

using Topogenia.Application.Audit;
using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Members;

/// <summary>A member as the replies show him (1.x `MembreVue`).</summary>
public sealed record MemberView(Guid UserId, string Login, string DisplayName, MemberRole Role, bool IsSponsor, bool IsKeyContributor, int DisplayOrder);

/// <summary>The members after a replacement, in display order, the users invited and created with them, and the people who left.</summary>
public sealed record MembersReply(IReadOnlyList<MemberView> Members, IReadOnlyList<Guid> InvitedUserIds, IReadOnlyList<Guid> RemovedUserIds);

public static class MemberViews
{
    /// <summary>The members of a project in display order (A-2.1-23: sponsors, then `DisplayOrder`, then name).</summary>
    public static IReadOnlyList<MemberView> Of(IEnumerable<ProjectMember> members, IEnumerable<AppUser> users)
    {
        var known = users.ToDictionary(user => user.Id);
        return MemberRules.Ordered(members, known.Values)
            .Select(member => View(member, known[member.UserId]))
            .ToList();
    }

    /// <summary>The members of the project of a workspace loaded with its members and their users.</summary>
    public static IReadOnlyList<MemberView> Of(ProjectWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return Of(workspace.All<ProjectMember>().Where(member => member.ProjectId == workspace.ProjectId), workspace.All<AppUser>());
    }

    private static MemberView View(ProjectMember member, AppUser user) =>
        new(member.UserId, user.Login, user.DisplayName, member.Role, member.IsSponsor, member.IsKeyContributor, member.DisplayOrder);
}

/// <summary>
/// `PUT /api/projects/{id}/members` (1.x `PUT /api/projets/{id}/membres`, Animateur): replaces the whole list
/// (`MemberRules.Replace`, A-2.1-14 to 22). The invited users are created in the same unit of work (spec 5.6-5,
/// A-2.1-22); the known users of the entered logins are read under the lock on each attempt of the unit, so that a unit
/// run again after a creation lost to a concurrent one takes the user created since (ADR 033). Not undoable (annex B
/// 2.6-44, AR-21): nothing is written in the operation log. If-Match on the project (spec 5.3, ADR 045 revised): the
/// list has no version of its own, so a replacement that changes it moves the version of the project on, and a client
/// that read the members before is refused CONFLIT_VERSION instead of losing the change of another facilitator.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class ReplaceMembersCommand(Guid projectId, IReadOnlyList<MemberEntry> members, PeopleSource people)
    : ProjectCommand<MembersReply>(AccessTarget.Project(projectId))
{
    private PeopleLookup? lookup;

    public override WorkspaceContent Content => WorkspaceContent.Members;

    public override string? AuditKind => AuditKinds.MembersChanged;

    public override string? AuditDetails(ChangeSet changes) => AuditTrail.MembersDetails(changes);

    public override Guid? MapOf(ProjectWorkspace workspace) => null;

    public override async Task PrepareAsync(IOperationLog log, Guid userId, CancellationToken cancellationToken) =>
        lookup = await people.LookupAsync(members, cancellationToken);

    public override MembersReply Execute(ProjectWorkspace workspace, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var known = lookup ?? throw new InvalidOperationException("The people are read before the domain runs.");
        var replacement = MemberRules.Replace(workspace, context, members, known);
        foreach (var user in replacement.NewUsers)
            workspace.Put(user);
        // The workspace holds the users of the former members and the new ones; a known user who joins is in the lookup.
        var users = workspace.All<AppUser>().Concat(known.KnownUsers).DistinctBy(user => user.Id);
        return new MembersReply(MemberViews.Of(replacement.Members, users), replacement.NewUsers.Select(user => user.Id).ToList(),
            replacement.RemovedUserIds);
    }

    /// <summary>The If-Match of the request on the project, whose version stands for the list of its members.</summary>
    public static ReplaceMembersCommand WithVersion(Guid projectId, IReadOnlyList<MemberEntry> members, PeopleSource people, int? version) =>
        new(projectId, members, people) { Versions = version is { } read ? [VersionCheck.Of<Project>(Project.KeyOf(projectId), read)] : [] };
}

/// <summary>`GET /api/projects/{id}/participants` (1.x `GET /api/projets/{id}/participants`, Lecteur): sponsors first.</summary>
[RequiresRole(MemberRole.Reader)]
public sealed class GetParticipantsQuery(Guid projectId) : ProjectQuery<IReadOnlyList<MemberView>>(AccessTarget.Project(projectId))
{
    public override WorkspaceContent Content => WorkspaceContent.Members;

    public override IReadOnlyList<MemberView> Read(ProjectWorkspace workspace, QueryContext context) => MemberViews.Of(workspace);
}
