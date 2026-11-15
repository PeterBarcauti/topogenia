// RouteRecorder.cs
// The routes that a host of the tests reached, request by request: the method of the request and the pattern of the entry point that the routing of the host chose for it.

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Topogenia.Host.Tests.Infrastructure;

/// <summary>
/// A test that names the route it plays writes that name itself: nothing ties it to the request it sends, and an
/// address copied from another route would pass for the route named. This recorder is the word of the host: put before
/// every middleware of the host, it keeps, when the reply of a request starts, the method of the request and the
/// pattern of the entry point that the routing chose, written as the table of the routes writes it (the constraints of
/// the parameters left out). The routing of the host runs first, before its own middlewares: a request refused by one
/// of them has its entry point all the same. A request that no entry point of the API takes is kept with the pattern
/// that took it (the pages), or without one.
/// </summary>
public sealed partial class RouteRecorder : IStartupFilter
{
    private readonly List<string> reached = [];

    [GeneratedRegex(@":[a-z]+\}")]
    private static partial Regex Constraint();

    /// <summary>The number of requests whose reply has started.</summary>
    public int Count
    {
        get
        {
            lock (reached)
                return reached.Count;
        }
    }

    /// <summary>The routes reached by the requests answered after the first <paramref name="count"/>, in the order of their replies.</summary>
    public IReadOnlyList<string> Since(int count)
    {
        lock (reached)
            return [.. reached.Skip(count)];
    }

    /// <summary>The name of a route as the table of the routes writes it: `METHOD /api/steps/{id}/sheet`.</summary>
    public static string NameOf(string method, string pattern) => $"{method} {Constraint().Replace(pattern.TrimEnd('/'), "}")}";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return application =>
        {
            application.Use((context, following) =>
            {
                context.Response.OnStarting(() =>
                {
                    Keep(context);
                    return Task.CompletedTask;
                });
                return following(context);
            });
            next(application);
        };
    }

    private void Keep(HttpContext context)
    {
        var pattern = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        var route = pattern is null ? $"{context.Request.Method} (no route)" : NameOf(context.Request.Method, pattern);
        lock (reached)
            reached.Add(route);
    }
}
