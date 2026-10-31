// HealthUnavailableTests.cs
// `GET /api/health` answers 200 while the database answers and 503 `degraded` when it does not, on both modes (A-1.5-19, spec 5.3).

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Topogenia.Application.Ports;
using Topogenia.Host.Api.Health;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.SqlServer;
using Topogenia.Persistence.Sqlite;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Api;

/// <summary>
/// The database that does not answer is a base that cannot be reached, given to the health of a variant of the host
/// (the host itself must reach its base to start): a SQL Server base never created on the instance of the tests, a SQLite
/// file in a folder that does not exist, opened without creation. Nothing is created by the test.
/// </summary>
public sealed class HealthUnavailableTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private static DatabaseEngine Unreachable(EngineKind kind)
    {
        if (kind == EngineKind.SqlServer)
            return new SqlServerEngine(TestServer.ConnectionString($"{TestDatabase.Prefix}{Environment.ProcessId}_absent"));
        var file = Path.Combine(Path.GetTempPath(), $"{TestDatabase.FolderPrefix}{Environment.ProcessId}-absent-{Guid.NewGuid():N}", "data.db");
        return new SqliteEngine(new SqliteConnectionStringBuilder { DataSource = file, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ConnectionString);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_health_is_503_degraded_when_the_database_does_not_answer_and_200_ok_when_it_does(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        using (var reader = host.As(Logins.Reader))
        {
            var healthy = (await reader.GetAsync("/api/health")).Ok();
            Assert.Equal(("ok", "ok"), (healthy.GetProperty("status").GetString(), healthy.GetProperty("database").GetString()));
        }

        var unreachable = Unreachable(kind);
        await host.WithVariantAsync(services => services.AddScoped(provider => new HealthApi(unreachable, provider.GetRequiredService<IClock>())), async clientOf =>
        {
            using var reader = clientOf(Logins.Reader);
            var reply = await reader.GetAsync("/api/health");
            Assert.Equal(503, reply.Status);
            var health = reply.Json;
            Assert.Equal(("degraded", "unavailable"), (health.GetProperty("status").GetString(), health.GetProperty("database").GetString()));
            Assert.False(string.IsNullOrEmpty(health.GetProperty("version").GetString()));
            if (kind == EngineKind.SqlServer)
            {
                using var anonymous = clientOf(null);
                var minimal = await anonymous.GetAsync("/api/health");
                Assert.Equal((503, "{\"status\":\"degraded\"}"), (minimal.Status, minimal.Body));
            }
        });
        if (unreachable is SqliteEngine sqlite)
            Assert.False(Directory.Exists(Path.GetDirectoryName(sqlite.DataSource)));
    }
}
