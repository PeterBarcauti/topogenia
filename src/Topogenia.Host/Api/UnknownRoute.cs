// UnknownRoute.cs
// A route under /api that the API does not know: INTROUVABLE 404, in the JSON format of the errors (annex A 1, `/api/{**reste}`).

using Topogenia.Domain.Errors;

namespace Topogenia.Host.Api;

/// <summary>The answer of the catch-all route of the API (1.x `/api/{**reste}`), never the page of the application.</summary>
public static class UnknownRoute
{
    public static Task Refuse(HttpContext context) => throw DomainException.NotFound();
}
