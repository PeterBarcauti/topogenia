// AssistantRulesTests.cs
// The rules of the settings of the assistant: the defaults, each connector and its fields, the secret kept, removed or replaced, its least length and its hint, a key never given to another provider nor to another resource of Azure, the bounds (spec 3.0 section 4, ADR 098).

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
        var checkedSettings = AssistantRules.Check(AssistantSettings.Default, Change("anthropic", region: "eu-central-1", profile: "profil-fictif", secret: "sk-ant-0123-wxyz"));
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
            Change("bedrock", model: "global.anthropic.claude-sonnet-5-5", region: "eu-central-1", profile: "profil-fictif", secret: "ignored-key"));
        Assert.Equal(("global.anthropic.claude-sonnet-5-5", "eu-central-1", "profil-fictif", (string?)null),
            (checkedSettings.Settings.Model, checkedSettings.Settings.Region, checkedSettings.Settings.AwsProfile, checkedSettings.Settings.SecretHint));
        Assert.Equal(SecretKind.Remove, checkedSettings.Secret.Kind);
        Assert.Equal((ErrorCodes.RequiredField, (object?)"region"), (Refusal(Change("bedrock", model: "m")).Code, Refusal(Change("bedrock", model: "m")).Details["field"]));
        Assert.Equal((object?)"model", Refusal(Change("bedrock", region: "eu-central-1")).Details["field"]);
    }

    [Theory]
    [InlineData("anthropic", null, "eu west", null, "region")]
    [InlineData("bedrock", "model name", "eu-central-1", null, "model")]
    [InlineData("bedrock", "m", "eu-central-1", "two words", "aws_profile")]
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

    [Theory]
    [InlineData("open_ai", AssistantConnector.OpenAi)]
    [InlineData("gemini", AssistantConnector.Gemini)]
    [InlineData("grok", AssistantConnector.Grok)]
    [InlineData("mistral", AssistantConnector.Mistral)]
    [InlineData("deep_seek", AssistantConnector.DeepSeek)]
    [InlineData("kimi", AssistantConnector.Kimi)]
    [InlineData("qwen", AssistantConnector.Qwen)]
    public void A_maker_of_the_chat_completions_api_needs_its_model_takes_its_key_and_nothing_of_another_connector(string code, AssistantConnector connector)
    {
        var checkedSettings = AssistantRules.Check(AssistantSettings.Default,
            Change(code, model: "model-1", region: "eu-central-1", profile: "profil-fictif", secret: "sk-0123-4567-abcd") with { Resource = "contoso", Port = 11500 });
        var settings = checkedSettings.Settings;
        Assert.Equal((connector, "model-1", (string?)null, (string?)null, (string?)null, (int?)null, "abcd"),
            (settings.Connector, settings.Model, settings.Region, settings.AwsProfile, settings.Resource, settings.Port, settings.SecretHint));
        Assert.Equal(SecretKind.Replace, checkedSettings.Secret.Kind);
        var refusal = Refusal(Change(code));
        Assert.Equal((ErrorCodes.RequiredField, (object?)"model"), (refusal.Code, refusal.Details["field"]));
    }

    [Fact]
    public void Azure_needs_the_name_of_its_resource_never_an_address_and_takes_its_key()
    {
        var checkedSettings = AssistantRules.Check(AssistantSettings.Default, Change("azure_open_ai", model: "processus-gpt", secret: "az-0123-4567-wxyz") with { Resource = "contoso-ai" });
        Assert.Equal(("contoso-ai", "processus-gpt", "wxyz"), (checkedSettings.Settings.Resource, checkedSettings.Settings.Model, checkedSettings.Settings.SecretHint));
        var missing = Refusal(Change("azure_open_ai", model: "processus-gpt"));
        Assert.Equal((ErrorCodes.RequiredField, (object?)"resource"), (missing.Code, missing.Details["field"]));
        foreach (var resource in new[] { "https://contoso.example", "contoso.openai.azure.com", "Contoso", "c", "-contoso" })
        {
            var refusal = Refusal(Change("azure_open_ai", model: "processus-gpt") with { Resource = resource });
            Assert.Equal((ErrorCodes.InvalidValue, (object?)"resource"), (refusal.Code, refusal.Details["field"]));
        }
    }

    [Fact]
    public void A_model_of_this_machine_takes_its_port_or_none_and_never_a_key()
    {
        var withKey = AssistantSettings.Default with { Connector = AssistantConnector.Anthropic, SecretHint = "wxyz" };
        var ollama = AssistantRules.Check(withKey, Change("ollama", model: "llama3.1:8b", secret: "ignored-key") with { Port = 11500 });
        Assert.Equal((AssistantConnector.Ollama, (int?)11500, (string?)null, SecretKind.Remove),
            (ollama.Settings.Connector, ollama.Settings.Port, ollama.Settings.SecretHint, ollama.Secret.Kind));
        Assert.Null(AssistantRules.Check(AssistantSettings.Default, Change("lm_studio", model: "qwen3")).Settings.Port);
        foreach (var port in new[] { 0, 65536 })
        {
            var refusal = Refusal(Change("ollama", model: "llama3.1:8b") with { Port = port });
            Assert.Equal((ErrorCodes.InvalidValue, (object?)"port"), (refusal.Code, refusal.Details["field"]));
        }
    }

    [Fact]
    public void The_connectors_say_what_they_call_take_and_read()
    {
        var calling = Enum.GetValues<AssistantConnector>().Where(AssistantConnectors.CallsModel).ToList();
        Assert.DoesNotContain(AssistantConnector.None, calling);
        Assert.DoesNotContain(AssistantConnector.CopyPaste, calling);
        // One choice without a connection (lot J10): the copy and paste is shown as none and offered no more; every other connector is itself.
        Assert.Equal(AssistantConnector.None, AssistantConnectors.Shown(AssistantConnector.CopyPaste));
        Assert.All(Enum.GetValues<AssistantConnector>().Where(connector => connector != AssistantConnector.CopyPaste),
            connector => Assert.Equal(connector, AssistantConnectors.Shown(connector)));
        Assert.Equal(Codes.All<AssistantConnector>().Where(code => code != "copy_paste"), AssistantConnectors.Offered());
        Assert.Equal([AssistantConnector.Anthropic, AssistantConnector.AzureOpenAi, AssistantConnector.DeepSeek, AssistantConnector.Gemini, AssistantConnector.Grok,
            AssistantConnector.Kimi, AssistantConnector.Mistral, AssistantConnector.OpenAi, AssistantConnector.Qwen],
            calling.Where(AssistantConnectors.TakesKey).OrderBy(connector => connector.ToString(), StringComparer.Ordinal));
        Assert.Equal([AssistantConnector.Anthropic, AssistantConnector.Bedrock, AssistantConnector.OpenAi], calling.Where(AssistantConnectors.ReadsPdf).OrderBy(connector => connector.ToString(), StringComparer.Ordinal));
        Assert.Equal((11434, 1234, (int?)null), (AssistantConnectors.DefaultPort(AssistantConnector.Ollama), AssistantConnectors.DefaultPort(AssistantConnector.LmStudio),
            AssistantConnectors.DefaultPort(AssistantConnector.OpenAi)));
    }

    [Fact]
    public void A_key_kept_for_another_provider_is_never_given_to_the_one_chosen_the_key_of_the_same_one_is_kept()
    {
        var anthropic = AssistantSettings.Default with { Connector = AssistantConnector.Anthropic, Model = "claude-opus-5-5", SecretHint = "wxyz" };
        var other = AssistantRules.Check(anthropic, Change("open_ai", model: "gpt-5.6-sol"));
        Assert.Equal((SecretKind.Remove, (string?)null), (other.Secret.Kind, other.Settings.SecretHint));
        var same = AssistantRules.Check(anthropic, Change("anthropic", model: "claude-sonnet-5-5"));
        Assert.Equal((SecretKind.Keep, "wxyz"), (same.Secret.Kind, same.Settings.SecretHint));
        var typed = AssistantRules.Check(anthropic, Change("open_ai", model: "gpt-5.6-sol", secret: "sk-0123-4567-abcd"));
        Assert.Equal((SecretKind.Replace, "abcd"), (typed.Secret.Kind, typed.Settings.SecretHint));
    }

    [Fact]
    public void The_key_of_azure_belongs_to_its_resource_another_resource_without_a_key_typed_removes_the_one_kept()
    {
        var azure = AssistantSettings.Default with { Connector = AssistantConnector.AzureOpenAi, Model = "processus-gpt", SecretHint = "wxyz", Resource = "contoso" };
        // One letter more: the key of `contoso` is never given to `contosoo` (R5-14).
        var other = AssistantRules.Check(azure, Change("azure_open_ai", model: "processus-gpt") with { Resource = "contosoo" });
        Assert.Equal((SecretKind.Remove, (string?)null, "contosoo"), (other.Secret.Kind, other.Settings.SecretHint, other.Settings.Resource));
        Assert.Equal(["resource", "secret"], AssistantRules.ChangedFields(azure, other.Settings, other.Secret));
        var same = AssistantRules.Check(azure, Change("azure_open_ai", model: "autre-deploiement") with { Resource = "contoso" });
        Assert.Equal((SecretKind.Keep, "wxyz"), (same.Secret.Kind, same.Settings.SecretHint));
        var typed = AssistantRules.Check(azure, Change("azure_open_ai", model: "processus-gpt", secret: "az-0123-4567-abcd") with { Resource = "contosoo" });
        Assert.Equal((SecretKind.Replace, "abcd"), (typed.Secret.Kind, typed.Settings.SecretHint));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("abcd")]
    [InlineData("sk-1234")]
    public void A_key_shorter_than_its_least_length_is_refused_whatever_the_connector(string key)
    {
        foreach (var change in new[] { Change("anthropic", secret: key), Change("open_ai", model: "gpt-5.6-sol", secret: key), Change("ollama", model: "llama3.1:8b", secret: key) })
        {
            var refusal = Refusal(change);
            Assert.Equal((ErrorCodes.InvalidValue, (object?)"secret"), (refusal.Code, refusal.Details["field"]));
        }
    }

    /// <summary>The hint never says the key nor most of it (R5-17): a quarter of its characters at most, four at most.</summary>
    [Theory]
    [InlineData("sk-12345", "45")]
    [InlineData("sk-12345678", "78")]
    [InlineData("sk-123456789", "789")]
    [InlineData("sk-123456789abc", "abc")]
    [InlineData("sk-123456789abcd", "abcd")]
    [InlineData("sk-ant-0123456789-0123456789-0123456789-wxyz", "wxyz")]
    public void The_hint_of_a_key_is_a_quarter_of_it_at_most_and_four_characters_at_most(string key, string hint)
    {
        var settings = AssistantRules.Check(AssistantSettings.Default, Change("anthropic", secret: key)).Settings;
        Assert.Equal((hint, true), (settings.SecretHint, settings.HasSecret));
        Assert.True(settings.SecretHint!.Length * 4 <= key.Length && settings.SecretHint.Length <= AssistantRules.HintLength);
    }
}
