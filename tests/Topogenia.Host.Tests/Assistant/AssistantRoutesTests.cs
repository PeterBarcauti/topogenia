// AssistantRoutesTests.cs
// The routes of the settings of the assistant through the whole host on both databases: the defaults, a change with its version, a write overtaken by another one, the key never given back and kept protected, the test of the connection and the audit (spec 3.0 sections 4, 6, 8).

using Microsoft.Extensions.DependencyInjection;
using Topogenia.Application.Assistant;
using Topogenia.Application.Audit;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Assistant;

public sealed class AssistantRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    /// <summary>A key of the tests: never sent to a real service, the test hosts answer in memory.</summary>
    private const string Key = "sk-ant-test-0123456789-abcd";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<int> VersionAsync(ApiClient admin) => (await admin.GetAsync("/api/admin/assistant")).Ok().GetProperty("version").GetInt32();

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_settings_list_the_connectors_and_are_read_and_changed_by_an_administrator_only(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        using var admin = host.As(Logins.Admin);
        using var facilitator = host.As(Logins.Facilitator);
        var read = (await admin.GetAsync("/api/admin/assistant")).Ok();
        Assert.Equal(["none", "copy_paste", "anthropic", "bedrock"], read.GetProperty("connectors").EnumerateArray().Select(code => code.GetString()));
        (await facilitator.GetAsync("/api/admin/assistant")).Refused(403, "ACCES_REFUSE");
        (await facilitator.PutAsync("/api/admin/assistant", new { connector = "copy_paste" }, await VersionAsync(admin))).Refused(403, "ACCES_REFUSE");
        (await facilitator.PostAsync("/api/admin/assistant/test", new { connector = "anthropic", secret = Key })).Refused(403, "ACCES_REFUSE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_key_is_written_never_given_back_and_kept_protected_and_a_stale_version_is_a_conflict(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        using var admin = host.As(Logins.Admin);
        var version = await VersionAsync(admin);
        var changed = await admin.PutAsync("/api/admin/assistant",
            new { connector = "anthropic", secret = Key, allowed_classifications = new[] { "internal", "confidential" } }, version);
        var view = changed.Ok();
        Assert.DoesNotContain(Key, changed.Body, StringComparison.Ordinal);
        Assert.Equal(("anthropic", "claude-opus-5-5", true, "abcd", version + 1),
            (view.GetProperty("connector").GetString(), view.GetProperty("model").GetString(), view.GetProperty("has_secret").GetBoolean(),
                view.GetProperty("secret_hint").GetString(), view.GetProperty("version").GetInt32()));
        Assert.DoesNotContain(Key, (await admin.GetAsync("/api/admin/assistant")).Body, StringComparison.Ordinal);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<IAssistantSettingsStore>().ReadAsync(Token);
            Assert.DoesNotContain(Key, stored!.ProtectedSecret!, StringComparison.Ordinal);
            Assert.Equal(Key, scope.ServiceProvider.GetRequiredService<ISecretProtector>().Unprotect(stored.ProtectedSecret!));
        }
        (await admin.PutAsync("/api/admin/assistant", new { connector = "none" }, version)).Refused(409, "CONFLIT_VERSION");
        (await admin.PutAsync("/api/admin/assistant", new { connector = "bedrock", model = "global.anthropic.claude-sonnet-5-5" }, version + 1))
            .Refused(400, "CHAMP_OBLIGATOIRE");
        var details = (await admin.GetAsync("/api/admin/audit?kind=assistant.settings_changed")).Ok().EnumerateArray().First().GetProperty("details").GetString();
        Assert.Contains("\"secret\"", details, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, details!, StringComparison.Ordinal);

        // The bounds of the ceiling of a reply, accepted by the CHECK of each base (the table holds one row only).
        foreach (var ceiling in new[] { AssistantRules.MinOutputTokens, AssistantRules.MaxOutputTokens })
        {
            var bound = (await admin.PutAsync("/api/admin/assistant", new { connector = "anthropic", max_output_tokens = ceiling }, await VersionAsync(admin))).Ok();
            Assert.Equal(ceiling, bound.GetProperty("max_output_tokens").GetInt32());
        }
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_test_reaches_the_connector_with_the_key_kept_says_a_refusal_and_is_audited_without_it(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        using var admin = host.As(Logins.Admin);
        (await admin.PutAsync("/api/admin/assistant", new { connector = "anthropic", secret = Key }, await VersionAsync(admin))).Ok();
        var passed = (await admin.PostAsync("/api/admin/assistant/test", new { connector = "anthropic" })).Ok();
        Assert.Equal((true, "claude-opus-5-5"), (passed.GetProperty("succeeded").GetBoolean(), passed.GetProperty("model").GetString()));
        var connector = host.Services.GetServices<IAssistantConnector>().OfType<TestConnector>().Single(item => item.Kind == AssistantConnector.Anthropic);
        Assert.Equal(Key, connector.Connections[^1].Secret);
        var refused = (await admin.PostAsync("/api/admin/assistant/test", new { connector = "anthropic", model = TestConnector.RefusedModel })).Ok();
        Assert.Equal((false, "refused"), (refused.GetProperty("succeeded").GetBoolean(), refused.GetProperty("failure").GetString()));
        var audit = (await admin.GetAsync("/api/admin/audit?kind=assistant.connection_tested")).Ok().EnumerateArray().ToList();
        Assert.True(audit.Count >= 2);
        Assert.All(audit, entry => Assert.DoesNotContain(Key, entry.GetProperty("details").GetString()!, StringComparison.Ordinal));
        Assert.Contains("\"failure\":\"refused\"", audit[0].GetProperty("details").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_write_overtaken_by_another_administrator_is_a_version_conflict_and_writes_nothing(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        using var admin = host.As(Logins.Admin);
        // A change that is always one: another ceiling than the one kept.
        var ceiling = (await admin.GetAsync("/api/admin/assistant")).Ok().GetProperty("max_output_tokens").GetInt32() == 4096 ? 4097 : 4096;
        var saved = (await admin.PutAsync("/api/admin/assistant", new { connector = "copy_paste", max_output_tokens = ceiling }, await VersionAsync(admin))).Ok();
        await using var scope = host.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IAssistantSettingsStore>();
        var read = (await store.ReadAsync(Token))!;
        // Written as by a request that read the version before: the change saved above came in between.
        var late = new StoredAssistantSettings(read.Settings with { MaxOutputTokens = 512 }, read.ProtectedSecret);
        var audit = new AuditEvent(Guid.CreateVersion7(), DateTime.UtcNow, null, AuditKinds.AssistantSettingsChanged, null, null);
        var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var refusal = await Assert.ThrowsAsync<DomainException>(() => unit.RunAsync(async token =>
        {
            await store.WriteAsync(late, audit, token);
            return true;
        }, Token));
        Assert.Equal((ErrorCodes.VersionConflict, (object?)"assistant_settings"), (refusal.Code, refusal.Details["object"]));
        var kept = (await admin.GetAsync("/api/admin/assistant")).Ok();
        Assert.Equal((saved.GetProperty("version").GetInt32(), saved.GetProperty("max_output_tokens").GetInt32()),
            (kept.GetProperty("version").GetInt32(), kept.GetProperty("max_output_tokens").GetInt32()));
    }
}
