// IVersionRequirement.cs
// Port of the If-Match the mode requires: whether the request lacks the version read that its mode demands (spec 5.3, annex A 2.16).

namespace Topogenia.Application.Ports;

/// <summary>
/// The server mode requires the version read (`If-Match`) on every change of a versioned record (spec 5.3, A-2.16-04,
/// weakness 1.x 5.6-3); the desktop mode does not (AR-07). The host says when a request that must carry it does not; the
/// templates then refuse it, 428 VERSION_REQUISE, once its rights are checked and before the domain runs, so that a
/// stranger is still told not found and a weaker role forbidden (<see cref="Commands.VersionCheck.EnsureRead"/>).
/// </summary>
public interface IVersionRequirement
{
    /// <summary>True when the request lacks the version its mode requires.</summary>
    bool IsMissing { get; }
}
