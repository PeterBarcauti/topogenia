// SystemClock.cs
// The time of the server, in UTC: the clock of the host (D08).

using Topogenia.Application.Ports;

namespace Topogenia.Host.Composition;

/// <summary>The time of the server, always UTC (D08).</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
