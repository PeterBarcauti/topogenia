// FixedClock.cs
// A clock that always gives the same UTC time, for the records of the history of the schema written by the tests.

using Topogenia.Application.Ports;

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>The time of the tests, with ticks below the millisecond: the full precision must survive a round trip.</summary>
public sealed class FixedClock : IClock
{
    public static readonly DateTime Time = new DateTime(2026, 10, 23, 8, 30, 15, DateTimeKind.Utc).AddTicks(1234567);

    public DateTime UtcNow => Time;
}
