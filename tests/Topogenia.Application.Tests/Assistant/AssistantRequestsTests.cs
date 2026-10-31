// AssistantRequestsTests.cs
// The requests of the settings of the assistant: the administrator alone, the key protected and never in the view nor in the audit, the version, the test of the connection and its failures (spec 3.0 sections 4, 6).

using Topogenia.Application.Assistant;
using Topogenia.Application.Audit;
using Topogenia.Application.Ports;
using Topogenia.Application.Tests.Support;
using Topogenia.Application.Tests.Users;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;

namespace Topogenia.Application.Tests.Assistant;

public sealed class AssistantRequestsTests
{
    private const string Key = "sk-ant-0123456789-wxyz";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static AssistantSettingsChange Anthropic(string? secret = Key, string? model = null) => new("anthropic", model, null, null, null, null, secret);

    private static UsersFixture Admin()
    {
        var fixture = new UsersFixture();
        fixture.ActingAs(fixture.Project.Admin);
        return fixture;
    }

    private static List<AuditEvent> Events(UsersFixture fixture, string kind) => [.. fixture.Database.All<AuditEvent>().Where(audit => audit.Kind == kind)];

    [Fact]
    public async Task A_member_who_is_not_an_administrator_is_refused_each_request_and_nothing_is_written()
    {
        var fixture = new UsersFixture();
        fixture.ActingAs(fixture.Project.Facilitator);
        foreach (var run in new Func<Task>[]
        {
            () => fixture.Runner.RunAsync(new ReadAssistantSettingsQuery(), Token),
            () => fixture.Runner.RunAsync(new ChangeAssistantSettingsCommand(Anthropic()), Token),
            () => fixture.Runner.RunAsync(new TestAssistantConnectionCommand(Anthropic()), Token),
        })
            Assert.Equal(ErrorCodes.AccessDenied, (await Assert.ThrowsAsync<DomainException>(run)).Code);
        Assert.Equal((0, 0), (fixture.Assistant.Store.Writes, fixture.Assistant.Anthropic.Connections.Count));
        Assert.Empty(fixture.Database.All<AuditEvent>());
    }

    [Fact]
    public async Task The_key_is_kept_protected_shown_by_its_hint_only_and_the_change_is_audited_by_the_names_of_its_fields()
    {
        var fixture = Admin();
        var view = await fixture.Runner.RunAsync(ChangeAssistantSettingsCommand.WithVersion(Anthropic(), 0), Token);
        Assert.Equal(("anthropic", "claude-opus-5-5", true, "wxyz", 1), (view.Connector, view.Model, view.HasSecret, view.SecretHint, view.Version));
        var stored = fixture.Assistant.Store.Stored!;
        Assert.Equal((FakeSecretProtector.Prefix + new string([.. Key.Reverse()]), fixture.Project.Admin.Id, TestProject.Now),
            (stored.ProtectedSecret, stored.Settings.UpdatedBy, stored.Settings.UpdatedAt));
        var audit = Assert.Single(Events(fixture, AuditKinds.AssistantSettingsChanged));
        Assert.Equal("""{"connector":"anthropic","fields":["connector","model","secret"]}""", audit.Details);
        Assert.DoesNotContain("wxyz", audit.Details!, StringComparison.Ordinal);
        Assert.Equivalent(view, await fixture.Runner.RunAsync(new ReadAssistantSettingsQuery(), Token), strict: true);
    }

    [Fact]
    public async Task Settings_unchanged_write_nothing_and_a_version_read_before_a_change_is_a_conflict()
    {
        var fixture = Admin();
        await fixture.Runner.RunAsync(new ChangeAssistantSettingsCommand(Anthropic()), Token);
        var view = await fixture.Runner.RunAsync(new ChangeAssistantSettingsCommand(Anthropic(secret: null)), Token);
        Assert.Equal((1, 1), (view.Version, fixture.Assistant.Store.Writes));
        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Runner.RunAsync(ChangeAssistantSettingsCommand.WithVersion(new AssistantSettingsChange("none", null, null, null, null, null, null), 0), Token));
        Assert.Equal((ErrorCodes.VersionConflict, (object?)"assistant_settings"), (refusal.Code, refusal.Details["object"]));
        Assert.Equal(1, fixture.Assistant.Store.Writes);
    }

    [Fact]
    public async Task The_test_gives_the_connector_the_key_kept_unprotected_or_the_key_typed_and_writes_only_its_audit()
    {
        var fixture = Admin();
        await fixture.Runner.RunAsync(new ChangeAssistantSettingsCommand(Anthropic()), Token);
        var outcome = await fixture.Runner.RunAsync(new TestAssistantConnectionCommand(Anthropic(secret: null)), Token);
        Assert.True(outcome.Succeeded);
        await fixture.Runner.RunAsync(new TestAssistantConnectionCommand(Anthropic(secret: "sk-typed-0000")), Token);
        Assert.Equal([Key, "sk-typed-0000"], fixture.Assistant.Anthropic.Connections.Select(connection => connection.Secret));
        Assert.Equal(1, fixture.Assistant.Store.Writes);
        var audits = Events(fixture, AuditKinds.AssistantConnectionTested);
        Assert.Equal(2, audits.Count);
        Assert.All(audits, audit => Assert.DoesNotContain("sk-", audit.Details!, StringComparison.Ordinal));
        Assert.Contains("\"succeeded\":true", audits[0].Details!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_key_or_with_a_key_the_host_cannot_read_or_without_a_connector_the_test_fails_before_any_call()
    {
        var fixture = Admin();
        Assert.Equal(AssistantFailures.KeyMissing, (await fixture.Runner.RunAsync(new TestAssistantConnectionCommand(Anthropic(secret: null)), Token)).Failure);
        await fixture.Runner.RunAsync(new ChangeAssistantSettingsCommand(Anthropic()), Token);
        fixture.Assistant.Protector.KeysLost = true;
        Assert.Equal(AssistantFailures.KeyUnreadable, (await fixture.Runner.RunAsync(new TestAssistantConnectionCommand(Anthropic(secret: null)), Token)).Failure);
        var none = new AssistantSettingsChange("copy_paste", null, null, null, null, null, null);
        Assert.Equal(AssistantFailures.NoConnector, (await fixture.Runner.RunAsync(new TestAssistantConnectionCommand(none), Token)).Failure);
        Assert.Empty(fixture.Assistant.Anthropic.Connections);
        Assert.Equal(3, Events(fixture, AuditKinds.AssistantConnectionTested).Count);
    }

    [Fact]
    public async Task Bedrock_is_tested_with_its_profile_and_region_and_without_a_key_and_a_refusal_of_the_provider_is_an_outcome()
    {
        var fixture = Admin();
        fixture.Assistant.Bedrock.Outcome = AssistantTestOutcome.Failed(AssistantFailures.SessionExpired, 7, "SSO session expired");
        var change = new AssistantSettingsChange("bedrock", "global.anthropic.claude-sonnet-5-5", "eu-west-3", "profil-fictif", null, null, "typed-key");
        var outcome = await fixture.Runner.RunAsync(new TestAssistantConnectionCommand(change), Token);
        Assert.Equal((false, AssistantFailures.SessionExpired, "SSO session expired"), (outcome.Succeeded, outcome.Failure, outcome.Detail));
        var connection = Assert.Single(fixture.Assistant.Bedrock.Connections);
        Assert.Equal(((string?)null, "eu-west-3", "profil-fictif"), (connection.Secret, connection.Settings.Region, connection.Settings.AwsProfile));
    }
}
