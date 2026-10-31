// RefusalWatchTests.cs
// The sliding window of the repeated refusals on a clock of the test: ten refusals within ten minutes make one event, refusals spread wider make none (spec 5.5, ADR 046).

using Topogenia.Application.Audit;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Host.Errors;
using Topogenia.Host.Tests.Logging;

namespace Topogenia.Host.Tests.Errors;

/// <summary>
/// <see cref="RefusalWatch"/> reads the time of its injected clock: the tests move a <see cref="SettableClock"/> between
/// the refusals, so that the window is tried at its bound without waiting. The threshold and the window are written here
/// as the decision says them (10 refusals, 10 minutes), not read from the code under test.
/// </summary>
public sealed class RefusalWatchTests
{
    private static readonly DateTime Start = new(2026, 3, 2, 8, 0, 0, DateTimeKind.Utc);

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private const int Threshold = 10;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Counts one refusal of <paramref name="userId"/> at each of <paramref name="times"/>; returns what each count answered.</summary>
    private static List<bool> CountAt(RefusalWatch watch, SettableClock clock, Guid userId, IEnumerable<DateTime> times)
    {
        var answers = new List<bool>();
        foreach (var time in times)
        {
            clock.UtcNow = time;
            answers.Add(watch.Count(userId));
        }
        return answers;
    }

    private static IEnumerable<DateTime> Every(TimeSpan step, int count) => Enumerable.Range(0, count).Select(rank => Start + step * rank);

    [Fact]
    public async Task Ten_refusals_spread_over_just_under_ten_minutes_append_one_event_with_the_count_and_the_window()
    {
        var clock = new SettableClock(Start);
        var watch = new RefusalWatch(clock);
        var audit = new RecordedAudit();
        var user = Guid.CreateVersion7();
        var step = (Window - TimeSpan.FromSeconds(1)) / (Threshold - 1);
        var counts = new List<int>();
        foreach (var time in Every(step, Threshold))
        {
            clock.UtcNow = time;
            await watch.RecordAsync(user, audit, Token);
            counts.Add(audit.Events.Count);
        }
        Assert.Equal([.. Enumerable.Repeat(0, Threshold - 1), 1], counts);
        var recorded = Assert.Single(audit.Events);
        Assert.Equal((AuditKinds.RepeatedRefusals, user, (Guid?)null, clock.UtcNow), (recorded.Kind, recorded.UserId, recorded.ProjectId, recorded.At));
        Assert.Equal("{\"refusals\":10,\"window_minutes\":10}", recorded.Details);
    }

    [Fact]
    public void Refusals_one_apart_more_than_a_tenth_of_the_window_never_make_an_event()
    {
        var clock = new SettableClock(Start);
        var watch = new RefusalWatch(clock);
        var step = Window / (Threshold - 1) + TimeSpan.FromSeconds(1);
        Assert.DoesNotContain(true, CountAt(watch, clock, Guid.CreateVersion7(), Every(step, 5 * Threshold)));
    }

    [Fact]
    public void A_refusal_ten_minutes_old_has_left_the_window_and_one_a_tick_younger_has_not()
    {
        var user = Guid.CreateVersion7();
        var clock = new SettableClock(Start);
        var watch = new RefusalWatch(clock);
        var first = Enumerable.Repeat(Start, Threshold - 1);
        Assert.DoesNotContain(true, CountAt(watch, clock, user, [.. first, Start + Window]));

        var younger = new RefusalWatch(clock);
        Assert.Equal([.. Enumerable.Repeat(false, Threshold - 1), true],
            CountAt(younger, clock, user, [.. first, Start + Window - TimeSpan.FromTicks(1)]));
    }

    [Fact]
    public void The_window_slides_the_oldest_refusals_out_one_by_one_and_the_count_starts_again_after_an_event()
    {
        var user = Guid.CreateVersion7();
        var clock = new SettableClock(Start);
        var watch = new RefusalWatch(clock);
        var minute = TimeSpan.FromMinutes(1);
        Assert.DoesNotContain(true, CountAt(watch, clock, user, Every(minute, Threshold - 1)));
        Assert.Equal([false, false], CountAt(watch, clock, user, [Start + 10 * minute, Start + 11 * minute]));
        Assert.Equal([true], CountAt(watch, clock, user, [Start + 11 * minute + TimeSpan.FromSeconds(30)]));
        Assert.DoesNotContain(true, CountAt(watch, clock, user, Enumerable.Repeat(Start + 12 * minute, Threshold - 1)));
    }

    [Fact]
    public void The_refusals_of_two_users_are_counted_apart()
    {
        var clock = new SettableClock(Start);
        var watch = new RefusalWatch(clock);
        var (first, second) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        for (var refusal = 1; refusal < Threshold; refusal++)
        {
            Assert.False(watch.Count(first));
            Assert.False(watch.Count(second));
        }
        Assert.True(watch.Count(second));
        Assert.True(watch.Count(first));
    }

    /// <summary>An audit store that keeps the events appended.</summary>
    private sealed class RecordedAudit : IAuditStore
    {
        public List<AuditEvent> Events { get; } = [];

        public Task AppendAsync(AuditEvent audit, CancellationToken cancellationToken)
        {
            Events.Add(audit);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditEvent>> ReadAsync(AuditQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AuditEvent>>(Events);

        public Task<int> RemoveBeforeAsync(DateTime limit, CancellationToken cancellationToken) => Task.FromResult(0);
    }
}
