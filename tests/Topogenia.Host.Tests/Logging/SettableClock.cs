// SettableClock.cs
// A clock that gives the UTC time set by the test, so that a test of the file log chooses its days.

using Topogenia.Application.Ports;

namespace Topogenia.Host.Tests.Logging;

/// <summary>The time of a test of the file log: fixed until the test moves it, read by several threads.</summary>
public sealed class SettableClock : IClock
{
    private long ticks;

    public SettableClock(DateTime start) => UtcNow = start;

    public DateTime UtcNow
    {
        get => new(Interlocked.Read(ref ticks), DateTimeKind.Utc);
        set => Interlocked.Exchange(ref ticks, value.Ticks);
    }
}
