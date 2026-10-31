// ProfileCommands.cs
// The profile of the user: read with his roles, changed (versioned, AR-07), and the last project he opened (annex A 1 `/api/moi`, AR-49).

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Profile;

/// <summary>
/// The profile (1.x `ProfilVue`): the language of the profile and the effective one (AR-51), the administrator flag, the
/// first visit, the last project opened (AR-49), the version (AR-07), the roles in the live projects, and the languages of
/// the configuration in their order, from which the interface builds its language switch (AR-29, NF12, ADR 057).
/// </summary>
public sealed record ProfileView(
    Guid Id,
    string Login,
    string DisplayName,
    string? Email,
    string? Language,
    string EffectiveLanguage,
    bool IsAdmin,
    bool FirstVisitDone,
    Guid? LastProjectId,
    int Version,
    IReadOnlyList<ProjectRole> Roles,
    IReadOnlyList<string> Languages)
{
    /// <summary>
    /// The host runs in desktop mode (spec 5.2, 6.1): one user, administrator of his data file; the interface offers the
    /// command « Quit » and, while he has no project, the sample project. Set by the host, which alone knows its mode.
    /// </summary>
    public bool Desktop { get; init; }
}

public static class ProfileViews
{
    public static ProfileView Of(UserProfile profile, string effectiveLanguage, LanguageSettings languages)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(languages);
        var user = profile.User;
        return new ProfileView(user.Id, user.Login, user.DisplayName, user.Email, user.Language, effectiveLanguage, user.IsAdmin, user.FirstVisitDone,
            user.LastProjectId, user.Version, profile.Roles, languages.Languages);
    }

    /// <summary>The profile of the user of the request, read in one consistent read (or in the unit that changed it).</summary>
    public static async Task<ProfileView> ReadAsync(UserScope scope, string effectiveLanguage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var profile = await scope.Ports.Users.ProfileAsync(scope.User.Id, cancellationToken) ?? throw DomainException.NotFound();
        return Of(profile, effectiveLanguage, scope.Languages);
    }
}

/// <summary>`GET /api/me` (1.x `GET /api/moi`, authenticated): the profile and the roles in the live projects (A-1.2-01).</summary>
[RequiresAuthenticated]
public sealed class GetProfileQuery : UserRequest<ProfileView>
{
    public override Task<ProfileView> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return ProfileViews.ReadAsync(scope, scope.User.Language, cancellationToken);
    }
}

/// <summary>
/// `PATCH /api/me` (1.x `PATCH /api/moi`, authenticated): `UserRules.ChangeProfile` (language, display name, first visit;
/// A-1.2-02 to 05). The profile is versioned (AR-07): a version read by the client is checked (CONFLIT_VERSION), none is
/// required here, as in the 1.x; the host demands it in server mode (VERSION_REQUISE, P4-T04). An unchanged profile
/// writes nothing (spec 5.6-6).
/// </summary>
[RequiresAuthenticated]
public sealed class ChangeProfileCommand(ProfileChange change) : UserRequest<ProfileView>
{
    public int? Version { get; init; }

    public override bool InUnitOfWork => true;

    public override async Task<ProfileView> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var user = await scope.Ports.Users.FindAsync(scope.User.Id, cancellationToken) ?? throw DomainException.NotFound();
        RecordVersion.Ensure(user, Version);
        var update = UserRules.ChangeProfile(scope.Operation, user, change, scope.Languages);
        if (update.Changed)
            await scope.Ports.Store.SaveAsync(update.Changes, cancellationToken);
        return await ProfileViews.ReadAsync(scope, change.Language is null ? scope.User.Language : update.Record.Language!, cancellationToken);
    }

    /// <summary>The If-Match of the request on the profile, when the client sent one.</summary>
    public static ChangeProfileCommand WithVersion(ProfileChange change, int? version) => new(change) { Version = version };
}

/// <summary>
/// The last project opened by the user (AR-49), on which the home page focuses: written on his profile when he opens a
/// project he may read (Lecteur). The spec names no route: the host calls it when a map of the project is opened (P4-T04).
/// </summary>
[RequiresRole(MemberRole.Reader)]
public sealed class RememberOpenedProjectCommand(Guid projectId) : UserRequest<Guid>
{
    public override AccessTarget? Target => AccessTarget.Project(projectId);

    public override bool InUnitOfWork => true;

    public override async Task<Guid> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var user = await scope.Ports.Users.FindAsync(scope.User.Id, cancellationToken) ?? throw DomainException.NotFound();
        var update = UserRules.OpenProject(scope.Operation, user, scope.Project);
        if (update.Changed)
            await scope.Ports.Store.SaveAsync(update.Changes, cancellationToken);
        return scope.Project;
    }
}
