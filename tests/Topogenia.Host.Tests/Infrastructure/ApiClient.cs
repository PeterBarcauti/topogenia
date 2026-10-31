// ApiClient.cs
// A client of the API for the tests: bodies written with the names of the contract, replies read as JSON, refusals checked in their format.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Topogenia.Host.Json;

namespace Topogenia.Host.Tests.Infrastructure;

/// <summary>
/// Sends the requests of one login. A body is written exactly as the test names its fields (`new { lane_id = ... }`), so
/// that a test proves the snake_case names of the contract and not those of a C# type; a text body is sent as it is (a
/// broken JSON), bytes as a file (a package). Every write carries the anti-CSRF header of the API (AR-13).
/// </summary>
public sealed class ApiClient(HttpClient client) : IDisposable
{
    private static readonly JsonSerializerOptions Written = new();

    public Task<ApiResponse> GetAsync(string url) => SendAsync(HttpMethod.Get, url, null, null);

    public Task<ApiResponse> PostAsync(string url, object? body = null, int? ifMatch = null) => SendAsync(HttpMethod.Post, url, body, ifMatch);

    public Task<ApiResponse> PutAsync(string url, object? body, int? ifMatch = null) => SendAsync(HttpMethod.Put, url, body, ifMatch);

    public Task<ApiResponse> PatchAsync(string url, object? body, int? ifMatch = null) => SendAsync(HttpMethod.Patch, url, body, ifMatch);

    public Task<ApiResponse> DeleteAsync(string url, int? ifMatch = null) => SendAsync(HttpMethod.Delete, url, null, ifMatch);

    /// <summary>Sends a request with its headers as given (raw `If-Match`, `Accept-Language`...).</summary>
    public async Task<ApiResponse> SendAsync(HttpMethod method, string url, object? body, int? ifMatch, params (string Name, string Value)[] headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        using var request = new HttpRequestMessage(method, url);
        if (method != HttpMethod.Get)
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        if (ifMatch is { } version)
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        foreach (var (name, value) in headers)
            request.Headers.TryAddWithoutValidation(name, value);
        if (body is byte[] bytes)
        {
            request.Content = new ByteArrayContent(bytes);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        }
        else if (body is not null)
        {
            var text = body as string ?? JsonSerializer.Serialize(body, Written);
            request.Content = new StringContent(text, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
        }
        return await ReplyAsync(request);
    }

    /// <summary>
    /// Sends <paramref name="content"/> as it is, with the anti-CSRF header and the `If-Match` given: a body of an exact
    /// size, with its length announced or sent in chunks without it.
    /// </summary>
    public async Task<ApiResponse> SendContentAsync(HttpMethod method, string url, HttpContent content, int? ifMatch)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        if (ifMatch is { } version)
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return await ReplyAsync(request);
    }

    private async Task<ApiResponse> ReplyAsync(HttpRequestMessage request)
    {
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        var replied = response.Headers.Concat(response.Content.Headers)
            .ToDictionary(header => header.Key, header => string.Join(", ", header.Value), StringComparer.OrdinalIgnoreCase);
        return new ApiResponse((int)response.StatusCode, Encoding.UTF8.GetString(bytes), response.Headers.Location) { Headers = replied, Bytes = bytes };
    }

    public void Dispose() => client.Dispose();
}

/// <summary>A reply: its status, its body, the `Location` of a creation and its headers.</summary>
public sealed record ApiResponse(int Status, string Body, Uri? Location)
{
    /// <summary>The headers of the reply and of its content, by name (case ignored).</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    /// <summary>The body as it came, for a file (a package): <see cref="Body"/> is its UTF-8 reading.</summary>
    public byte[] Bytes { get; init; } = [];

    private static readonly JsonSerializerOptions Read = ReadOptions();

    /// <summary>The options of the API, set as the host sets them (`ApiJson.Configure`).</summary>
    private static JsonSerializerOptions ReadOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiJson.Configure(options);
        return options;
    }

    public JsonElement Json => JsonDocument.Parse(Body).RootElement;

    /// <summary>The body of a success (2xx), else the test fails with the body received.</summary>
    public JsonElement Ok(int status = 200)
    {
        Assert.True(Status == status, $"Expected {status}, got {Status}: {Body}");
        return Body.Length == 0 ? default : Json;
    }

    /// <summary>The body read as a type of the application, with the JSON settings of the API.</summary>
    public T As<T>(int status = 200)
    {
        Ok(status);
        return JsonSerializer.Deserialize<T>(Body, Read)!;
    }

    /// <summary>
    /// A refusal in the format of the API (A-1.1-04): the status, the code, a message that is not empty, an object of
    /// details, and nothing else; returns the details.
    /// </summary>
    public JsonElement Refused(int status, string code)
    {
        Assert.True(Status == status, $"Expected {status} {code}, got {Status}: {Body}");
        var json = Json;
        Assert.Equal(["code", "details", "message"], json.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(code, json.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("message").GetString()));
        Assert.Equal(JsonValueKind.Object, json.GetProperty("details").ValueKind);
        return json.GetProperty("details");
    }
}
