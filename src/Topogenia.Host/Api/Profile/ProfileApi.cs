// ProfileApi.cs
// The routes of the profile turned into their requests: read, change, last project opened (annex A 1 moi, A 2.1, AR-49).

using Microsoft.Extensions.Options;
using Topogenia.Application.Commands;
using Topogenia.Application.Profile;
using Topogenia.Domain.Projects;
using Topogenia.Host.Composition;
using Topogenia.Host.Json;

namespace Topogenia.Host.Api.Profile;

/// <summary>
/// `PATCH /api/me` (1.x: `langue`, `nom_affiche`, `premiere_visite_faite`): only the fields present change. A language or
/// a display name sent as null is refused like an empty one (LANGUE_INVALIDE, CHAMP_OBLIGATOIRE), as the 1.x did.
/// </summary>
public sealed record ChangeProfileBody
{
    public Optional<string?> Language { get; init; }

    public Optional<string?> DisplayName { get; init; }

    public bool? FirstVisitDone { get; init; }
}

/// <summary>`PUT /api/me/last-project` (AR-49, route named by the host): the project the user has just opened.</summary>
public sealed record LastProjectBody(Guid? ProjectId);

/// <summary>Runs the requests of the profile routes; each reply says whether the host runs in desktop mode (spec 5.2).</summary>
public sealed class ProfileApi
{
    private readonly UserRunner users;
    private readonly bool desktop;

    public ProfileApi(UserRunner users, IOptions<ProductOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.users = users;
        desktop = options.Value.Mode == HostMode.Desktop;
    }

    public async Task<ProfileView> GetAsync(CancellationToken cancellationToken) => Of(await users.RunAsync(new GetProfileQuery(), cancellationToken));

    public async Task<ProfileView> ChangeAsync(ChangeProfileBody body, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var change = new ProfileChange(Present(body.Language), Present(body.DisplayName), body.FirstVisitDone);
        return Of(await users.RunAsync(ChangeProfileCommand.WithVersion(change, ApiInput.Version(ifMatch)), cancellationToken));
    }

    /// <summary>Remembers the project (Lecteur at least), then reads the profile that names it.</summary>
    public async Task<ProfileView> RememberProjectAsync(LastProjectBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        await users.RunAsync(new RememberOpenedProjectCommand(ApiInput.Required(body.ProjectId, "project_id")), cancellationToken);
        return await GetAsync(cancellationToken);
    }

    private ProfileView Of(ProfileView profile) => profile with { Desktop = desktop };

    /// <summary>A field present with null becomes an empty text, which the domain refuses; an absent field changes nothing.</summary>
    private static string? Present(Optional<string?> field) => field.IsPresent ? field.Value ?? "" : null;
}
