// AssistantProviders.cs
// The providers reached by the Chat Completions API of OpenAI (ADR 098): the only place of the shipped code where the address of a provider is written; for each, its address or the form of it, the header of its key, the form of its structured output, the parameter of the ceiling of a reply.

using Topogenia.Application.Assistant;

namespace Topogenia.Assistant;

/// <summary>How a provider constrains the reply to the schema: by the schema itself, or by a JSON object and the schema told in the instructions.</summary>
public enum ReplyFormat { JsonSchema, JsonObject }

/// <summary>A provider of the Chat Completions API: its fixed address (none for Azure and the machine, built from the settings), its key, its output, its ceiling.</summary>
public sealed record AssistantProvider(
    AssistantConnector Kind,
    string? Address,
    bool BearerKey,
    ReplyFormat Format,
    string CeilingParameter);

/// <summary>
/// The table of the providers (ADR 098). An address is written here only: the rule of the shipped code admits no other
/// (`SourceRulesTests`), and the list is the one of `docs/securite.md`. Azure is reached at the resource of the
/// settings, Ollama and LM Studio on this machine at the port of the settings: no address is ever typed (S-5.4-13).
/// This machine is called by the literal address of its loopback, never by a name: a name is resolved, and may lead
/// to the listener of another account of the machine (the IPv6 loopback first on Windows). The name of the ceiling
/// of a reply is the one each provider takes: OpenAI and Azure OpenAI refuse `max_tokens` for their recent models
/// and take `max_completion_tokens`, every other provider takes `max_tokens`. DeepSeek writes a JSON object, not a
/// schema; Alibaba Qwen a JSON object for nearly all its models and the schema for a part of them only: both get
/// the schema told in the instructions.
/// </summary>
public static class AssistantProviders
{
    public static IReadOnlyList<AssistantProvider> All { get; } =
    [
        new(AssistantConnector.OpenAi, "https://api.openai.com/v1", true, ReplyFormat.JsonSchema, "max_completion_tokens"),
        new(AssistantConnector.Gemini, "https://generativelanguage.googleapis.com/v1beta/openai", true, ReplyFormat.JsonSchema, "max_tokens"),
        new(AssistantConnector.Grok, "https://api.x.ai/v1", true, ReplyFormat.JsonSchema, "max_tokens"),
        new(AssistantConnector.Mistral, "https://api.mistral.ai/v1", true, ReplyFormat.JsonSchema, "max_tokens"),
        new(AssistantConnector.DeepSeek, "https://api.deepseek.com/v1", true, ReplyFormat.JsonObject, "max_tokens"),
        new(AssistantConnector.Kimi, "https://api.moonshot.ai/v1", true, ReplyFormat.JsonSchema, "max_tokens"),
        new(AssistantConnector.Qwen, "https://dashscope-intl.aliyuncs.com/compatible-mode/v1", true, ReplyFormat.JsonObject, "max_tokens"),
        new(AssistantConnector.AzureOpenAi, null, false, ReplyFormat.JsonSchema, "max_completion_tokens"),
        new(AssistantConnector.Ollama, null, true, ReplyFormat.JsonSchema, "max_tokens"),
        new(AssistantConnector.LmStudio, null, true, ReplyFormat.JsonSchema, "max_tokens"),
    ];

    /// <summary>The provider of a connector of the Chat Completions API.</summary>
    public static AssistantProvider Of(AssistantConnector kind) =>
        All.FirstOrDefault(provider => provider.Kind == kind) ?? throw new ArgumentOutOfRangeException(nameof(kind), kind, "No provider of the Chat Completions API");

    /// <summary>The address of the completions of a provider, for the settings given: the resource of Azure, the port of the machine.</summary>
    public static Uri ChatAddress(AssistantConnector kind, AssistantSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var provider = Of(kind);
        var root = kind switch
        {
            AssistantConnector.AzureOpenAi => $"https://{settings.Resource}.openai.azure.com/openai/v1",
            AssistantConnector.Ollama or AssistantConnector.LmStudio => $"http://127.0.0.1:{settings.Port ?? AssistantConnectors.DefaultPort(kind)}/v1",
            _ => provider.Address!,
        };
        return new Uri($"{root}/chat/completions");
    }
}
