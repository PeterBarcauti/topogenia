// ErrorMiddleware.cs
// Turns every failure of a request into the JSON error {code, message, details} in the language of the user (annex A 1, A-1.1-04 to 11).

using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;
using Topogenia.Host.Security;

namespace Topogenia.Host.Errors;

/// <summary>
/// The single place where a failure becomes a reply (A-1.1-04): a refusal of the domain or of the application gets its
/// status and its message in the effective language of the user (A-1.1-05: profile, else `Accept-Language`, else the
/// default language, AR-51), with the display name of the author of a conflicting change (A-2.16-06); a body that cannot be
/// read is REQUETE_INVALIDE (A-2.18-05, A-2.18-06); any other failure is ERREUR_INTERNE (A-1.1-11), logged by its type
/// only, never by its message nor its <c>ToString()</c>, which may name the account, the base or the file (ADR 036).
/// </summary>
public sealed partial class ErrorMiddleware
{
    private readonly RequestDelegate next;
    private readonly ErrorResponses responses;
    private readonly LanguageSettings languages;
    private readonly IClock clock;
    private readonly JsonSerializerOptions json;
    private readonly ILogger<ErrorMiddleware> logger;
    private readonly RefusalWatch refusals;

    public ErrorMiddleware(RequestDelegate next, ErrorResponses responses, LanguageSettings languages, IClock clock,
        IOptions<JsonOptions> json, ILogger<ErrorMiddleware> logger, RefusalWatch refusals)
    {
        this.refusals = refusals;
        ArgumentNullException.ThrowIfNull(json);
        this.next = next;
        this.responses = responses;
        this.languages = languages;
        this.clock = clock;
        this.json = json.Value.SerializerOptions;
        this.logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUser user, IUserReader users)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(users);
        try
        {
            await next(context);
        }
        catch (Exception failure) when (!context.Response.HasStarted && !context.RequestAborted.IsCancellationRequested)
        {
            var language = user.User?.Language ?? languages.EffectiveLanguage(null, context.Request.Headers.AcceptLanguage.ToString());
            var (status, body) = failure switch
            {
                DomainException refusal => (ErrorResponses.StatusOf(refusal), await BodyOfAsync(refusal, language, users, context.RequestAborted)),
                BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } =>
                    (StatusCodes.Status413PayloadTooLarge, responses.BodyOf(BodySizeLimit.Refusal(context), language, clock.UtcNow, _ => null)),
                BadHttpRequestException { InnerException: JsonException wrong } when PatchFieldOf(context, wrong) is { } field =>
                    (StatusCodes.Status400BadRequest, responses.BodyOf(DomainException.Invalid(ErrorCodes.InvalidValue, field), language, clock.UtcNow, _ => null)),
                JsonException wrong when PatchFieldOf(context, wrong) is { } field =>
                    (StatusCodes.Status400BadRequest, responses.BodyOf(DomainException.Invalid(ErrorCodes.InvalidValue, field), language, clock.UtcNow, _ => null)),
                BadHttpRequestException or JsonException =>
                    (StatusCodes.Status400BadRequest, responses.BodyOf(new DomainException(ErrorCodes.InvalidRequest, RefusalKind.Invalid), language, clock.UtcNow, _ => null)),
                _ => (StatusCodes.Status500InternalServerError, Internal(failure, context, language)),
            };
            // The reply is written again; HSTS, set before this middleware in the server mode, stays on it (F-05).
            var hsts = context.Response.Headers.StrictTransportSecurity;
            context.Response.Clear();
            if (!StringValues.IsNullOrEmpty(hsts))
                context.Response.Headers.StrictTransportSecurity = hsts;
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(body, json, context.RequestAborted);
            if (failure is DomainException { Code: ErrorCodes.AccessDenied } && user.User is { } refused)
                await AuditRefusalAsync(context, refused.Id);
        }
    }

    /// <summary>
    /// The field of a PATCH body whose value has the wrong type (`{"first_visit_done":"yes"}`): VALEUR_INVALIDE with the
    /// field, as the 1.x read each field of a PATCH apart (A-1.2-05, `CorpsPatch.Lire`). The 1.x bound every other body as a
    /// whole and refused it as REQUETE_INVALIDE, a field of the wrong type included: so do POST and PUT here.
    /// </summary>
    private static string? PatchFieldOf(HttpContext context, JsonException failure) =>
        HttpMethods.IsPatch(context.Request.Method) ? FieldOf(failure) : null;

    /// <summary>
    /// The last property named by the path of a failure of the serializer, the field whose value has the wrong type. A body
    /// that is not JSON at all has none: the serializer wraps the failure of its reader in the exception it throws, which
    /// then names the property it was reading too.
    /// </summary>
    public static string? FieldOf(JsonException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        if (failure.InnerException is JsonException || failure.Path is not { } path)
            return null;
        var match = System.Text.RegularExpressions.Regex.Match(path, @"\.([A-Za-z0-9_]+)(\[\d+\])*$");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>A refusal of access counted for the audit (spec 5.5); a failure of the trail never changes the reply.</summary>
    private async Task AuditRefusalAsync(HttpContext context, Guid userId)
    {
        try
        {
            await refusals.RecordAsync(userId, context.RequestServices.GetRequiredService<IAuditStore>(), context.RequestAborted);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            LogAuditFailed(logger, failure.GetType().FullName ?? failure.GetType().Name);
        }
    }

    private async Task<ErrorBody> BodyOfAsync(DomainException refusal, string language, IUserReader users, CancellationToken cancellationToken)
    {
        var names = new Dictionary<Guid, string?>();
        if (refusal.Details.TryGetValue("updated_by", out var by) && by is Guid author)
            names[author] = (await users.FindAsync(author, cancellationToken))?.DisplayName;
        return responses.BodyOf(refusal, language, clock.UtcNow, id => names.GetValueOrDefault(id));
    }

    private ErrorBody Internal(Exception failure, HttpContext context, string language)
    {
        LogInternal(logger, failure.GetType().FullName ?? failure.GetType().Name, context.TraceIdentifier);
        return responses.InternalError(language);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Audit of repeated refusals failed: {Type}.")]
    private static partial void LogAuditFailed(ILogger logger, string type);

    [LoggerMessage(Level = LogLevel.Error, Message = "Internal error {Type} on the request {TraceId}.")]
    private static partial void LogInternal(ILogger logger, string type, string traceId);
}
