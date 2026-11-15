// RequestLog.cs
// One line per request in the log: method, route, status, duration and user by identifier, at the level of its kind (annex A 4, A-4-26, A-4-28).

using System.Diagnostics;
using Topogenia.Host.Users;

namespace Topogenia.Host.Logging;

/// <summary>
/// Logs each request once it is answered (A-4-26, as the 1.x): Error for a status of 500 or more, Information for a write
/// of the API (any method but GET, HEAD, OPTIONS under `/api`), Debug for the rest. The line holds the method, the
/// pattern of the route (its identifiers stay out of the line: `/api/maps/{id}`), else the path for a request no route
/// takes, the status, the duration and the identifier of the user; never a login, a name, a body nor a query string
/// (spec 5.2, 5.4, A-4-28).
/// </summary>
public sealed partial class RequestLog(RequestDelegate next, ILogger<RequestLog> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var request = context.Request;
            var status = context.Response.StatusCode;
            var level = status >= StatusCodes.Status500InternalServerError ? LogLevel.Error
                : IsApiWrite(request) ? LogLevel.Information
                : LogLevel.Debug;
            if (logger.IsEnabled(level))
            {
                var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? request.Path.Value ?? "";
                var user = context.RequestServices.GetService<RequestUser>()?.User?.Id;
                LogRequest(logger, level, request.Method, route, status, Stopwatch.GetElapsedTime(started).TotalMilliseconds, user);
            }
        }
    }

    private static bool IsApiWrite(HttpRequest request) =>
        !HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method) && !HttpMethods.IsOptions(request.Method)
        && request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);

    [LoggerMessage(Message = "{Method} {Route} {Status} in {Elapsed:0} ms (user {UserId}).")]
    private static partial void LogRequest(ILogger logger, LogLevel level, string method, string route, int status, double elapsed, Guid? userId);
}
