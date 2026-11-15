// UnlockedWriteException.cs
// A record without version written by a unit that does not hold the lock of its project: a fault of the caller, never a conflict (ADR 030).

namespace Topogenia.Persistence.Errors;

/// <summary>
/// A record without version (a counter, a deletion batch, a row of a procedure, a step of a frame...) has no column that
/// the statement which writes it could compare: its writing is safe only under the lock of its project (spec 4.7), which
/// serialises every unit that writes the project (ADR 030). A change set that modifies or removes such a record in a unit
/// that has not taken the lock of the project of that record is refused with this error before anything is written: it
/// is a fault of the handler (an internal error), not a conflict with another user.
/// </summary>
public sealed class UnlockedWriteException : InvalidOperationException
{
    public UnlockedWriteException()
        : base("A record without version is written only under the lock of its project.")
    {
    }

    public UnlockedWriteException(string message)
        : base(message)
    {
    }

    public UnlockedWriteException(string message, Exception inner)
        : base(message, inner)
    {
    }

    /// <summary>The error for a record of <paramref name="recordType"/> (the type of its key).</summary>
    public static UnlockedWriteException Of(string recordType) =>
        new($"A record without version ({recordType}) is modified or removed by a unit that does not hold the lock of its project (ADR 030).");
}
