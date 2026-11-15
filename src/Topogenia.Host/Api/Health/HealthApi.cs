// HealthApi.cs
// The health of the host: whether the database answers, the version and the time (annex A 1 `/api/sante`, AR-46).

using System.Reflection;
using System.Text.Json.Serialization;
using Topogenia.Application.Ports;
using Topogenia.Persistence.Schema;

namespace Topogenia.Host.Api.Health;

/// <summary>The health (1.x `EtatSante` without `ia`): `ok` or `degraded`, the database `ok` or `unavailable`, the version, the time.</summary>
public sealed record HealthReply(string Status, string Database, string Version, DateTime Timestamp)
{
    /// <summary>True when the database answers; the status code says it, the body does not (1.x `EtatSante`).</summary>
    [JsonIgnore]
    public bool Healthy => Database == HealthApi.Available;
}

/// <summary>The health given to an anonymous caller of the server mode: the state alone (spec 5.3, weakness 1.x 5.6-8).</summary>
public sealed record HealthStatusReply(string Status);

/// <summary>
/// `GET /api/health`: 200 when the database answers, 503 otherwise (A-1.5-19). In desktop mode it needs the session
/// cookie like any route (AR-46); in server mode an anonymous caller gets <see cref="HealthStatusReply"/> only
/// (`Endpoints/SystemEndpoints`). The failure of the database is not described: the state only.
/// </summary>
public sealed class HealthApi(DatabaseEngine engine, IClock clock)
{
    public const string Available = "ok";

    public const string Unavailable = "unavailable";

    public async Task<HealthReply> ReadAsync(CancellationToken cancellationToken)
    {
        bool answers;
        try
        {
            await using var context = engine.CreateContext();
            answers = await context.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            answers = false;
        }
        var version = typeof(HealthApi).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "";
        return new HealthReply(answers ? "ok" : "degraded", answers ? Available : Unavailable, version, clock.UtcNow);
    }
}
