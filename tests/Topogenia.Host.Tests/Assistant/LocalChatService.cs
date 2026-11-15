// LocalChatService.cs
// A program that listens on the loopback of the machine for the tests, as Ollama or LM Studio do: it answers each request with the next of its replies and keeps what it received; no request leaves the machine.

using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Topogenia.Host.Tests.Assistant;

/// <summary>A reply of the service: its status, its body, and the address it redirects to when it has one.</summary>
internal sealed record LocalReply(int Status, string Body, string? Location = null);

/// <summary>A request the service received: its first line (the method, the target as it was written, the version), its headers, its body.</summary>
internal sealed record LocalRequest(string Line, IReadOnlyDictionary<string, string> Headers, string Body);

/// <summary>
/// A service of HTTP on `127.0.0.1` at a free port, one connection at a time: each request is read whole and kept, then
/// answered with the next reply given (the last one again) and its connection closed. It plays the program of the
/// machine, the place a redirection points to, or a proxy (which receives the whole address in the first line).
/// </summary>
internal sealed class LocalChatService : IAsyncDisposable
{
    private static readonly byte[] HeadEnd = "\r\n\r\n"u8.ToArray();

    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stopping = new();
    private readonly ConcurrentQueue<LocalRequest> requests = new();
    private readonly LocalReply[] replies;
    private readonly Task serving;
    private int served;

    public LocalChatService(params LocalReply[] replies)
    {
        this.replies = replies;
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        serving = ServeAsync();
    }

    /// <summary>The port the service listens on, given to the settings of the connector.</summary>
    public int Port { get; }

    /// <summary>The address of the completions of the service, as a redirection names it.</summary>
    public string Address => $"http://127.0.0.1:{Port}/v1/chat/completions";

    public IReadOnlyList<LocalRequest> Requests => [.. requests];

    private async Task ServeAsync()
    {
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                using var client = await listener.AcceptTcpClientAsync(stopping.Token);
                await AnswerAsync(client.GetStream(), stopping.Token);
            }
            catch (Exception failure) when (stopping.IsCancellationRequested || failure is OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // A client that hangs up before the end of its reply: the next one is served.
            }
        }
    }

    private async Task AnswerAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        using var received = new MemoryStream();
        var buffer = new byte[65536];
        int end;
        while ((end = received.GetBuffer().AsSpan(0, (int)received.Length).IndexOf(HeadEnd)) < 0)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                return;
            received.Write(buffer, 0, read);
        }
        var head = Encoding.ASCII.GetString(received.GetBuffer(), 0, end).Split("\r\n");
        var headers = head.Skip(1).Select(line => line.Split(':', 2)).Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);
        var length = headers.TryGetValue("Content-Length", out var announced) ? int.Parse(announced, CultureInfo.InvariantCulture) : 0;
        var start = end + HeadEnd.Length;
        while (received.Length < start + length)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;
            received.Write(buffer, 0, read);
        }
        requests.Enqueue(new LocalRequest(head[0], headers, Encoding.UTF8.GetString(received.GetBuffer(), start, (int)received.Length - start)));
        var reply = replies[Math.Min(Interlocked.Increment(ref served) - 1, replies.Length - 1)];
        var body = Encoding.UTF8.GetBytes(reply.Body);
        var answer = new StringBuilder($"HTTP/1.1 {reply.Status} Reply\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n");
        if (reply.Location is { } location)
            answer.Append("Location: ").Append(location).Append("\r\n");
        await stream.WriteAsync(Encoding.ASCII.GetBytes(answer.Append("\r\n").ToString()), cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await stopping.CancelAsync();
        listener.Stop();
        await serving;
        listener.Dispose();
        stopping.Dispose();
    }
}
