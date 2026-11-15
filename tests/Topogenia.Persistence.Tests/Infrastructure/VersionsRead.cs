// VersionsRead.cs
// The If-Match of the requests of the persistence tests: always carried, so that the templates check the versions sent.

using Topogenia.Application.Ports;

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>A request that carries the versions its mode requires (the desktop mode, or the server mode with If-Match).</summary>
internal sealed class VersionsRead : IVersionRequirement
{
    public bool IsMissing => false;
}
