// BodySizeLimitTests.cs
// The limit of 1 MB of a body at its bound, with its length announced and sent in chunks without it: the last byte allowed is read, one more is refused 413 (spec 5.4, A-4-23).

using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Topogenia.Host.Errors;
using Topogenia.Host.Security;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Security;

/// <summary>
/// The body is a change of the profile (`PATCH /api/me`, a route that names no limit of its own) whose JSON is padded with
/// spaces to an exact size: a body read whole changes the display name, a refused one changes nothing. The size of the
/// limit is written here as the specification says it (1 MB = 1 048 576 bytes), not read from the code under test. The
/// client sends the body as given (<see cref="ApiHost.Unbuffered"/>), so that a body of unknown length reaches the host
/// without length and is counted while it is read.
/// </summary>
public sealed class BodySizeLimitTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private const int OneMegabyte = 1024 * 1024;

    public static TheoryData<EngineKind, bool> Cases => new()
    {
        { EngineKind.Sqlite, true },
        { EngineKind.Sqlite, false },
        { EngineKind.SqlServer, true },
        { EngineKind.SqlServer, false },
    };

    /// <summary>`{"display_name":"name"}` padded with spaces before its closing brace to exactly <paramref name="size"/> bytes.</summary>
    private static byte[] ProfileOf(string name, int size)
    {
        var head = $"{{\"display_name\":\"{name}\"";
        return Encoding.ASCII.GetBytes(head + new string(' ', size - head.Length - 1) + "}");
    }

    /// <summary>The body with its length announced, or as a stream of unknown length that the client sends in chunks.</summary>
    private static HttpContent Content(byte[] body, bool announced)
    {
        HttpContent content = announced ? new ByteArrayContent(body) : new StreamContent(new UnknownLength(body));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        Assert.Equal(announced ? (long?)body.Length : null, content.Headers.ContentLength);
        return content;
    }

    private static async Task<ApiResponse> ChangeProfileAsync(ApiClient client, byte[] body, bool announced)
    {
        var version = (await client.GetAsync("/api/me")).Ok().GetProperty("version").GetInt32();
        using var content = Content(body, announced);
        return await client.SendContentAsync(HttpMethod.Patch, "/api/me", content, version);
    }

    private static async Task<string?> DisplayNameAsync(ApiClient client) => (await client.GetAsync("/api/me")).Ok().GetProperty("display_name").GetString();

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task A_body_of_exactly_one_megabyte_is_read_whole_and_one_byte_more_is_refused_413_with_or_without_its_length(EngineKind kind, bool announced)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        using var reader = project.Host.Unbuffered(Logins.Reader);
        var name = $"Borne {project.Code}";

        var atTheLimit = await ChangeProfileAsync(reader, ProfileOf(name, OneMegabyte), announced);
        Assert.Equal(name, atTheLimit.Ok().GetProperty("display_name").GetString());

        var over = await ChangeProfileAsync(reader, ProfileOf("Trop long", OneMegabyte + 1), announced);
        var details = over.Refused(413, "REQUETE_TROP_GROSSE");
        Assert.Equal(1, details.GetProperty("max").GetInt32());
        Assert.Equal("MB", details.GetProperty("unit").GetString());
        Assert.Equal(name, await DisplayNameAsync(reader));
    }

    private static readonly ErrorResponses Responses = new(ErrorMessages.Embedded());

    /// <summary>The refusal 413 of a route whose limit is <paramref name="limit"/> bytes, as the API writes it in a language.</summary>
    private static ErrorBody RefusalOf(long limit, string language)
    {
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new RequestSizeLimitMetadata(limit)), "route"));
        return Responses.BodyOf(BodySizeLimit.Refusal(context), language, DateTime.UtcNow, _ => null);
    }

    [Theory]
    [InlineData(4 * 1024, 4, "KB", "La requête dépasse 4 Ko.", "The request exceeds 4 KB.")]
    [InlineData(1024, 1, "KB", "La requête dépasse 1 Ko.", "The request exceeds 1 KB.")]
    [InlineData(1536 * 1024, 1536, "KB", "La requête dépasse 1536 Ko.", "The request exceeds 1536 KB.")]
    [InlineData(1024 * 1024, 1, "MB", "La requête dépasse 1 Mo.", "The request exceeds 1 MB.")]
    [InlineData(10 * 1024 * 1024, 10, "MB", "La requête dépasse 10 Mo.", "The request exceeds 10 MB.")]
    [InlineData(100L * 1024 * 1024, 100, "MB", "La requête dépasse 100 Mo.", "The request exceeds 100 MB.")]
    public void The_limit_is_told_in_the_unit_that_fits_never_as_zero_megabytes(long limit, long max, string unit, string french, string english)
    {
        var body = RefusalOf(limit, "fr");
        Assert.Equal(("REQUETE_TROP_GROSSE", max, unit, french), (body.Code, body.Details["max"], body.Details["unit"], body.Message));
        Assert.Equal(english, RefusalOf(limit, "en").Message);
    }

    private sealed record RequestSizeLimitMetadata(long? MaxRequestBodySize) : IRequestSizeLimitMetadata
    {
        public RequestSizeLimitMetadata(long limit) : this((long?)limit)
        {
        }
    }

    /// <summary>A stream read once, forward only, whose length is unknown: the client cannot announce it.</summary>
    private sealed class UnknownLength(byte[] bytes) : Stream
    {
        private readonly MemoryStream inner = new(bytes, writable: false);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
