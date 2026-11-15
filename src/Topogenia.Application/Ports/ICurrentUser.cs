// ICurrentUser.cs
// Port of the user of the request, given by the host after authentication (spec 3.4, annex A 2.1).

namespace Topogenia.Application.Ports;

/// <summary>
/// The authenticated user of a request: identifier, login, display name, effective language (AR-51) and the
/// administrator flag read again from the configuration at each request (annex A 2.1).
/// </summary>
public sealed record CurrentUser(Guid Id, string Login, string DisplayName, string Language, bool IsAdmin);

/// <summary>The user of the current request; null when nobody is authenticated (NON_AUTHENTIFIE).</summary>
public interface ICurrentUser
{
    CurrentUser? User { get; }
}
