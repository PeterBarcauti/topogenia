// GatedBody.cs
// A request body that gives nothing until it is let go and counts what is read of it: a refusal made before the body is read leaves it untouched (audit 3.2.0).

namespace Topogenia.Host.Tests.Exchange;

/// <summary>
/// The body of a request sent by the client of the server in memory, which copies it in the background while the
/// request runs: nothing comes out of it until <see cref="Open"/>, and <see cref="Given"/> counts the bytes read. A route
/// that refuses before it reads its body answers while the gate is shut, and nothing was read. A route that reads first
/// would wait for the gate: it opens by itself after <see cref="Patience"/>, so that such a route answers late with the
/// bytes counted and the test fails instead of hanging.
/// </summary>
public sealed class GatedBody(byte[] content) : Stream
{
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private readonly TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int position;

    /// <summary>The bytes read so far.</summary>
    public int Given => Volatile.Read(ref position);

    /// <summary>Lets the body go: the copy of the client ends.</summary>
    public void Open() => gate.TrySetResult();

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        gate.Task.Wait(Patience);
        return Take(buffer.AsSpan(offset, count));
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await Task.WhenAny(gate.Task, Task.Delay(Patience, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        return Take(buffer.Span);
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private int Take(Span<byte> buffer)
    {
        var count = Math.Min(buffer.Length, content.Length - position);
        content.AsSpan(position, count).CopyTo(buffer);
        Interlocked.Add(ref position, count);
        return count;
    }
}
