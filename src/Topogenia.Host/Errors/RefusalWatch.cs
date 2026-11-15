// RefusalWatch.cs
// The refusals of access of each user counted over a sliding window until they make an audit event (spec 5.5).

using System.Collections.Concurrent;
using Topogenia.Application.Audit;
using Topogenia.Application.Ports;

namespace Topogenia.Host.Errors;

/// <summary>
/// The refusals of access (ACCES_REFUSE) of each user over a sliding window (spec 5.5: repeated refusals): the
/// <see cref="AuditTrail.RefusalThreshold"/>-th refusal within <see cref="AuditTrail.RefusalWindow"/> appends one event,
/// with the count and the window, and the count starts again. A non-member's INTROUVABLE is not counted: it is the same
/// refusal as an object that does not exist, which ordinary navigation meets. The counts live in the process only.
/// </summary>
public sealed class RefusalWatch(IClock clock)
{
    private readonly ConcurrentDictionary<Guid, Queue<DateTime>> refusals = new();

    /// <summary>Counts a refusal of <paramref name="userId"/>; true when it completes a series to audit.</summary>
    public bool Count(Guid userId)
    {
        var now = clock.UtcNow;
        var times = refusals.GetOrAdd(userId, _ => new Queue<DateTime>());
        lock (times)
        {
            while (times.Count > 0 && now - times.Peek() >= AuditTrail.RefusalWindow)
                times.Dequeue();
            times.Enqueue(now);
            if (times.Count < AuditTrail.RefusalThreshold)
                return false;
            times.Clear();
            return true;
        }
    }

    /// <summary>Counts a refusal and appends the event when it completes a series.</summary>
    public async Task RecordAsync(Guid userId, IAuditStore audit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audit);
        if (!Count(userId))
            return;
        await audit.AppendAsync(AuditTrail.OfRefusals(clock.UtcNow, userId), cancellationToken);
    }
}
