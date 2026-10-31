// IAdministrators.cs
// Port of the administrators of the configuration, read at the start and at each change of the file (spec 5.3).

namespace Topogenia.Application.Ports;

/// <summary>
/// The logins of the administrators as the configuration gives them now (spec 5.3: read again at the start and at each
/// change of the file, not at each request, A-2.1-26). The flag of a user is synchronised on this list at his sign-in, both
/// ways (`UserRules.WithAdministratorFlag`); the host implements it.
/// </summary>
public interface IAdministrators
{
    IReadOnlyCollection<string> Logins { get; }
}
