// AssistantRulesTests.cs
// The rules of the settings of the assistant: the defaults, each connector and its fields, the secret kept, removed or replaced and its hint, the bounds (spec 3.0 section 4).

using Topogenia.Application.Assistant;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Assistant;

public sealed class AssistantRulesTests
{
    private static AssistantSettingsChange Change(string connector, string? model = null, string? region = null, string? profile = null, int? ceiling = null,
        string[]? allowed = null, string? secret = null) => new(connector, model, region, profile, ceiling, allowed, secret);

    private static DomainException Refusal(AssistantSettingsChange change, AssistantSettings? current = null) =>
        Assert.Throws<DomainException>(() => AssistantRules.Check(current ?? AssistantSettings.Default, change));

    [Fact]
    public void A_new_installation_calls_no_model_and_would_send_only_an_internal_project()
    {
        var settings = AssistantSettings.Default;
        Assert.Equal((AssistantConnector.None, false, 64000, 0), (settings.Connector, settings.HasSecret, settings.MaxOutputTokens, settings.Version));
        Assert.Equal([Classification.Internal], settings.AllowedClassifications);
    }

    [Fact]
    public void The_api_of_anthropic_takes_its_default_model_and_its_key_whose_last_characters_are_the_hint()
    {
        var checkedSettings = AssistantRules.Check(AssistantSettings.Default, Change("anthropic", region: "eu-west-3", profile: "profil-fictif", secret: "sk-ant-0123-wxyz"));
        var settings = checkedSettings.Settings;
        Assert.Equal((AssistantConnector.Anthropic, "claude-opus-5-5", (string?)null, (string?)null, "wxyz"),
            (settings.Connector, settings.Model, settings.Region, settings.AwsProfile, settings.SecretHint));
        Assert.Equal((SecretKind.Replace, "sk-ant-0123-wxyz"), (checkedSettings.Secret.Kind, checkedSettings.Secret.Value));
    }

    [Fact]
    public void Bedrock_needs_its_region_and_its_model_keeps_its_profile_and_takes_no_key()
    {
        var withKey = AssistantSettings.Default with { Connector = AssistantConnector.Anthropic, SecretHint = "wxyz" };
        var checkedSettings = AssistantRules.Check(withKey,
            Change("bedrock", model: "global.anthropic.claude-sonnet-5-5", region: "eu-west-3", profile: "profil-fictif", secret: "ignored-key"));
        Assert.Equal(("global.anthropic.claude-sonnet-5-5", "eu-west-3", "profil-fictif", (string?)null),
            (checkedSettings.Settings.Model, checkedSettings.Settings.Region, checkedSettings.Settings.AwsProfile, checkedSettings.Settings.SecretHint));
        Assert.Equal(SecretKind.Remove, checkedSettings.Secret.Kind);
        Assert.Equal((ErrorCodes.RequiredField, (object?)"region"), (Refusal(Change("bedrock", model: "m")).Code, Refusal(Change("bedrock", model: "m")).Details["field"]));
        Assert.Equal((object?)"model", Refusal(Change("bedrock", region: "eu-west-3")).Details["field"]);
    }

    [Theory]
    [InlineData("anthropic", null, "eu west", null, "region")]
    [InlineData("bedrock", "model name", "eu-west-3", null, "model")]
    [InlineData("bedrock", "m", "eu-west-3", "two words", "aws_profile")]
    [InlineData("openai", null, null, null, "connector")]
    public void A_field_outside_its_form_is_an_invalid_value_on_its_name(string connector, string? model, string? region, string? profile, string field)
    {
        var refusal = Refusal(Change(connector, model, region, profile));
        Assert.Equal((ErrorCodes.InvalidValue, (object?)field), (refusal.Code, refusal.Details["field"]));
    }

    [Fact]
    public void A_key_with_a_blank_or_a_character_outside_ascii_is_refused_too_long_a_key_too_and_an_empty_key_removes_the_one_kept()
    {
        foreach (var key in new[] { "sk ant", "sk-ant\u0007", "sk-ant-cl\u00e9", "sk-ant\u007f" })
            Assert.Equal((object?)"secret", Refusal(Change("anthropic", secret: key)).Details["field"]);
        Assert.Equal("sk-ant-~!", AssistantRules.Check(AssistantSettings.Default, Change("anthropic", secret: "sk-ant-~!")).Secret.Value);
        Assert.Equal(ErrorCodes.TextTooLong, Refusal(Change("anthropic", secret: new string('k', 501))).Code);
        var kept = AssistantSettings.Default with { Connector = AssistantConnector.Anthropic, SecretHint = "wxyz" };
        var removed = AssistantRules.Check(kept, Change("anthropic", secret: ""));
        Assert.Equal((SecretKind.Remove, (string?)null), (removed.Secret.Kind, removed.Settings.SecretHint));
        var keptAgain = AssistantRules.Check(kept, Change("anthropic"));
        Assert.Equal((SecretKind.Keep, "wxyz"), (keptAgain.Secret.Kind, keptAgain.Settings.SecretHint));
    }

    [Theory]
    [InlineData(255, false)]
    [InlineData(256, true)]
    [InlineData(128000, true)]
    [InlineData(128001, false)]
    public void The_ceiling_of_a_reply_stays_within_its_bounds(int ceiling, bool accepted)
    {
        if (accepted)
            Assert.Equal(ceiling, AssistantRules.Check(AssistantSettings.Default, Change("none", ceiling: ceiling)).Settings.MaxOutputTokens);
        else
            Assert.Equal((object?)"max_output_tokens", Refusal(Change("none", ceiling: ceiling)).Details["field"]);
    }

    [Fact]
    public void The_classifications_are_codes_of_the_list_once_each_in_their_order()
    {
        var settings = AssistantRules.Check(AssistantSettings.Default, Change("none", allowed: ["strictly_confidential", "internal", "internal"])).Settings;
        Assert.Equal([Classification.Internal, Classification.StrictlyConfidential], settings.AllowedClassifications);
        Assert.Equal((object?)"allowed_classifications", Refusal(Change("none", allowed: ["secret"])).Details["field"]);
    }

    [Fact]
    public void The_fields_changed_are_named_the_secret_by_its_name_never_by_its_value_its_removal_too()
    {
        var before = AssistantSettings.Default;
        var checkedSettings = AssistantRules.Check(before, Change("anthropic", allowed: ["internal", "confidential"], secret: "sk-ant-0123-wxyz"));
        Assert.Equal(["connector", "model", "allowed_classifications", "secret"], AssistantRules.ChangedFields(before, checkedSettings.Settings, checkedSettings.Secret));
        Assert.Empty(AssistantRules.ChangedFields(before, before, SecretChange.Keep));
        var kept = checkedSettings.Settings;
        var removed = AssistantRules.Check(kept, Change("anthropic", allowed: ["internal", "confidential"], secret: ""));
        Assert.Equal(["secret"], AssistantRules.ChangedFields(kept, removed.Settings, removed.Secret));
    }
}
