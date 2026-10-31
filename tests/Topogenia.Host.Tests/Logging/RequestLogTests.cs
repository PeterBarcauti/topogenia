// RequestLogTests.cs
// The level of the line of each request: Error from 500, Information for a write of the API, Debug otherwise; the route, never the query (A-4-26).

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Topogenia.Host.Logging;

namespace Topogenia.Host.Tests.Logging;

public sealed class RequestLogTests
{
    private sealed class LevelLogger : ILogger<RequestLog>
    {
        public List<(LogLevel Level, string Line)> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add((logLevel, formatter(state, exception)));
    }

    private static async Task<(LogLevel Level, string Line)> LogOfAsync(string method, string path, int status, string? route = null)
    {
        var logger = new LevelLogger();
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString("?token=secret");
        if (route is not null)
            context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse(route), 0, EndpointMetadataCollection.Empty, null));
        await new RequestLog(http =>
        {
            http.Response.StatusCode = status;
            return Task.CompletedTask;
        }, logger).InvokeAsync(context);
        return Assert.Single(logger.Lines);
    }

    [Theory]
    [InlineData("GET", "/api/maps/1", 500, LogLevel.Error)]
    [InlineData("POST", "/api/maps/1/lanes", 503, LogLevel.Error)]
    [InlineData("POST", "/api/maps/1/lanes", 201, LogLevel.Information)]
    [InlineData("DELETE", "/api/flows/1", 404, LogLevel.Information)]
    [InlineData("GET", "/api/maps/1", 200, LogLevel.Debug)]
    [InlineData("OPTIONS", "/api/maps/1", 204, LogLevel.Debug)]
    [InlineData("POST", "/launch", 401, LogLevel.Debug)]
    public async Task The_level_follows_the_status_then_whether_the_request_writes_the_api(string method, string path, int status, LogLevel level) =>
        Assert.Equal(level, (await LogOfAsync(method, path, status)).Level);

    [Fact]
    public async Task The_line_gives_the_pattern_of_the_route_and_never_the_query_string()
    {
        var (_, line) = await LogOfAsync("PATCH", "/api/lanes/0193", 200, "/api/lanes/{id:guid}");
        Assert.StartsWith("PATCH /api/lanes/{id:guid} 200 in ", line, StringComparison.Ordinal);
        Assert.DoesNotContain("0193", line, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", line, StringComparison.Ordinal);
        Assert.Contains("(user )", (await LogOfAsync("GET", "/index.html", 401)).Line, StringComparison.Ordinal);
    }
}
