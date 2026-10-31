// ReplySchema.cs
// The JSON Schema of the reply of the model (spec 3.0 section 7): one object of one of the four forms, which the structured output of the providers enforces.

using System.Text.Json;

namespace Topogenia.Assistant;

/// <summary>
/// The schema of the reply, in the subset the structured outputs accept (no recursion, `additionalProperties` false,
/// every property required): the proposal carries the text of a `.pmap` file, whose maps nest, as a string that the
/// format of the application reads with its own rules. The application reads the reply again whatever the provider did
/// (`AssistantReplyReader`).
/// </summary>
public static class ReplySchema
{
    /// <summary>The kinds of a finding, as the application reads them.</summary>
    public static readonly IReadOnlyList<string> FindingKinds = ["wait", "re_entry", "repeated_task", "no_owner", "no_sheet", "loop", "break", "other"];

    /// <summary>The forms of a reply.</summary>
    public static readonly IReadOnlyList<string> Forms = ["answer", "questions", "findings", "proposal"];

    public static string Text { get; } = JsonSerializer.Serialize(new Dictionary<string, object>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>
        {
            ["form"] = new { type = "string", @enum = Forms },
            ["message"] = new { type = "string" },
            ["cited"] = new { type = "array", items = new { type = "string" } },
            ["questions"] = new
            {
                type = "array",
                items = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["question"] = new { type = "string" }, ["choices"] = new { type = "array", items = new { type = "string" } },
                    },
                    ["required"] = new[] { "question", "choices" },
                    ["additionalProperties"] = false,
                },
            },
            ["findings"] = new
            {
                type = "array",
                items = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["kind"] = new { type = "string", @enum = FindingKinds },
                        ["title"] = new { type = "string" },
                        ["detail"] = new { type = "string" },
                        ["objects"] = new { type = "array", items = new { type = "string" } },
                        ["suggestion"] = new { type = "string" },
                    },
                    ["required"] = new[] { "kind", "title", "detail", "objects", "suggestion" },
                    ["additionalProperties"] = false,
                },
            },
            ["pmap"] = new { anyOf = new object[] { new { type = "string" }, new { type = "null" } } },
        },
        ["required"] = new[] { "form", "message", "cited", "questions", "findings", "pmap" },
        ["additionalProperties"] = false,
    });

    /// <summary>The schema as the SDK of Anthropic takes it: its properties at the top.</summary>
    public static Dictionary<string, JsonElement> Properties()
    {
        using var document = JsonDocument.Parse(Text);
        return document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone());
    }
}
