// IUserReader.cs
// Port of the reads of the users: by identifier, by login, by lists of logins or identifiers, the profile with its roles (spec 3.4, annex A 1).

using Topogenia.Domain.Model;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Ports;

/// <summary>The role of a user in one live project, as the profile shows it (`GET /api/me`, 1.x `RoleProjet`).</summary>
public sealed record ProjectRole(Guid ProjectId, MemberRole Role, bool IsSponsor);

/// <summary>A user and his roles in the live projects, read together.</summary>
public sealed record UserProfile(AppUser User, IReadOnlyList<ProjectRole> Roles);

/// <summary>
/// Reads the users (table `app_user`), which belong to no project. Inside a unit of work, in its transaction, so that a
/// unit run again after a creation lost to a concurrent one finds the user created since (ADR 033); outside, each call in
/// one consistent read (ADR 031). A login is looked up by its normalised form (spec 4.1, `Login.Normalize`).
/// </summary>
public interface IUserReader
{
    Task<AppUser?> FindAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The user of a login, case ignored; null when nobody has it.</summary>
    Task<AppUser?> FindByLoginAsync(string login, CancellationToken cancellationToken);

    /// <summary>The users of these logins, case ignored; the logins nobody has are left out.</summary>
    Task<IReadOnlyList<AppUser>> FindByLoginsAsync(IReadOnlyCollection<string> logins, CancellationToken cancellationToken);

    /// <summary>The users of these identifiers; the identifiers nobody has are left out (the authors named by an export).</summary>
    Task<IReadOnlyList<AppUser>> FindManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    /// <summary>The user and his memberships of the live projects, in one consistent read; null when he does not exist.</summary>
    Task<UserProfile?> ProfileAsync(Guid userId, CancellationToken cancellationToken);
}
