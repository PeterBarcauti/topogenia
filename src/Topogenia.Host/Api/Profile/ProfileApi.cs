// ProfileApi.cs
// The routes of the profile turned into their requests: read, change, last project opened (annex A 1 moi, A 2.1, AR-49); the mode, the Store and the links to the site that the host adds (ADR 111).

using Microsoft.Extensions.Options;
using Topogenia.Application.Commands;
using Topogenia.Application.Profile;
using Topogenia.Domain.Projects;
using Topogenia.Host.Composition;
using Topogenia.Host.Desktop;
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

/// <summary>
/// What the profile says of the links to the site (ADR 111 points 3 and 4), decided by the host alone: the Store when the
/// desktop mode runs from its package (the family name of the package, never given: it carries the pseudonym of the
/// publisher); the two addresses of the identity of the product unless the operation turned the switch `SiteLinks` off.
/// </summary>
public sealed record ProfileLinks(bool Store, ProductLinks? Links)
{
    public static ProfileLinks Of(ProductOptions options, string? familyName)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new(options.Mode == HostMode.Desktop && familyName is not null, options.SiteLinks ? ProductLinks.OfProduct : null);
    }
}

/// <summary>
/// Runs the requests of the profile routes; each reply says whether the host runs in desktop mode (spec 5.2), from the
/// package of the Store, and the links to the site (ADR 111).
/// </summary>
public sealed class ProfileApi
{
    private readonly UserRunner users;
    private readonly bool desktop;
    private readonly ProfileLinks links;

    public ProfileApi(UserRunner users, IOptions<ProductOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.users = users;
        desktop = options.Value.Mode == HostMode.Desktop;
        links = ProfileLinks.Of(options.Value, DesktopPackage.FamilyName);
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

    private ProfileView Of(ProfileView profile) => profile with { Desktop = desktop, Store = links.Store, Links = links.Links };

    /// <summary>A field present with null becomes an empty text, which the domain refuses; an absent field changes nothing.</summary>
    private static string? Present(Optional<string?> field) => field.IsPresent ? field.Value ?? "" : null;
}
