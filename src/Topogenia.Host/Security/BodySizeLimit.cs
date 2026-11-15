// BodySizeLimit.cs
// The limit of the body of each entry point, applied before anything reads it: 1 MB by default, more where a route says so (spec 5.4, A-4-23).

using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Topogenia.Domain.Errors;

namespace Topogenia.Host.Security;

/// <summary>
/// Applies the limit of the body of the route (its <see cref="IRequestSizeLimitMetadata"/>, else <see cref="Default"/>)
/// before anything reads the request (spec 5.4, weakness 1.x 5.6-7): a request that announces a longer body is refused
/// at once, 413 REQUETE_TROP_GROSSE with the limit in MB, without reading it; a body without announced length (chunked)
/// is read through a stream that refuses the same way as soon as it passes the limit, so that the reader of the route
/// never gets more. The limit of the server is set a little above, as a last guard. The 1.x relied on the limit of
/// Kestrel or IIS (about 30 MB) and checked the size of an import after reading it.
/// </summary>
public sealed class BodySizeLimit(RequestDelegate next)
{
    /// <summary>The limit of an entry point that names none: 1 MB (spec 5.4).</summary>
    public const long Default = 1024 * 1024;

    private const long Megabyte = 1024 * 1024;

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var limit = LimitOf(context);
        if (context.Request.ContentLength > limit)
            throw Refusal(context);
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } feature)
            feature.MaxRequestBodySize = limit + Megabyte;
        if (context.Request.ContentLength is null)
            context.Request.Body = new LimitedBody(context.Request.Body, limit, () => Refusal(context));
        return next(context);
    }

    /// <summary>The limit of the route of the request, in bytes.</summary>
    public static long LimitOf(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.GetEndpoint()?.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize ?? Default;
    }

    /// <summary>REQUETE_TROP_GROSSE with the limit of the route in MB.</summary>
    public static DomainException Refusal(HttpContext context) =>
        new(ErrorCodes.RequestTooLarge, RefusalKind.TooLarge, new Dictionary<string, object?> { ["max"] = LimitOf(context) / Megabyte });

    /// <summary>The body read up to the limit: one byte more and the refusal is thrown instead of the data.</summary>
    private sealed class LimitedBody(Stream inner, long limit, Func<Exception> refusal) : Stream
    {
        private long read;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => read;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Counted(inner.Read(buffer, offset, count));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Counted(await inner.ReadAsync(buffer, cancellationToken));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private int Counted(int count)
        {
            read += count;
            return read > limit ? throw refusal() : count;
        }
    }
}
