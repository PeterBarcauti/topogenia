// ReplySchema.cs
// The JSON Schema of the reply of the model (spec 3.0 section 7): one object of one of the forms an exchange allows, with the fields of those forms alone, the report of a map among them (plan 3.1, L4), which the structured output of the providers enforces.

using System.Collections.Concurrent;
using System.Text.Json;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Domain.Values;

namespace Topogenia.Assistant;

/// <summary>
/// The schema of the reply, in the subset the structured outputs accept (no recursion, `additionalProperties` false,
/// every property required): the proposal carries the text of a `.pmap` file, whose maps nest, as a string that the
/// format of the application reads with its own rules. The application reads the reply again whatever the provider did
/// (`AssistantReplyReader`), and a field left out reads as an empty one. An exchange is given the schema of the forms
/// it allows, with their fields alone: a provider compiles the schema into a grammar and refuses one too large, by an
/// internal limit it does not quantify (the API of Anthropic refused the schema of every form together). Each text is
/// built once and kept: the same exchange always sends the same text, which a test pins.
/// </summary>
public static class ReplySchema
{
    /// <summary>The kinds of a finding, as the application reads them.</summary>
    public static readonly IReadOnlyList<string> FindingKinds = ["wait", "re_entry", "repeated_task", "no_owner", "no_sheet", "loop", "break", "other"];

    /// <summary>The forms of a reply.</summary>
    public static readonly IReadOnlyList<string> Forms = ["answer", "questions", "findings", "proposal", "report", "summaries"];

    /// <summary>The sources of a problem and the waves of an action of a report.</summary>
    public static readonly IReadOnlyList<string> Sources = ["noted", "spotted"];

    public static readonly IReadOnlyList<string> Waves = ["now", "month", "later"];

    /// <summary>The texts built so far, by the forms allowed and the comments asked.</summary>
    private static readonly ConcurrentDictionary<string, string> Texts = new(StringComparer.Ordinal);

    /// <summary>
    /// The schema of the reply of an exchange: the one of the forms its prompt allows, with the comments when it asks
    /// some, of steps or of flows (the same field carries both: the schema sent to the providers is the same one).
    /// </summary>
    public static string For(AssistantPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return For(prompt.Forms, prompt.Comments.Count + prompt.FlowComments.Count > 0);
    }

    /// <summary>
    /// The schema of the forms allowed, as a JSON text: `form` among them, `message` and `cited`, then the fields of those
    /// forms alone, every one required; the translations of comments of presentation beside a proposal when some are
    /// asked. The order the forms are given in changes nothing: the text of the same forms is the same one.
    /// </summary>
    public static string For(IReadOnlyList<AssistantForm> forms, bool comments)
    {
        ArgumentNullException.ThrowIfNull(forms);
        var allowed = Forms.Where(code => forms.Any(form => Codes.Of(form) == code)).ToList();
        if (allowed.Count == 0)
            throw new ArgumentException("An exchange allows one form at least.", nameof(forms));
        var commented = comments && forms.Contains(AssistantForm.Proposal);
        var key = string.Join(',', allowed) + (commented ? "+comments" : "");
        return Texts.TryGetValue(key, out var text) ? text : Texts.GetOrAdd(key, Build(allowed, commented));
    }

    /// <summary>The schema of an exchange as the SDK of Anthropic takes it: its members at the top.</summary>
    public static Dictionary<string, JsonElement> Properties(AssistantPrompt prompt)
    {
        using var document = JsonDocument.Parse(For(prompt));
        return document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone());
    }

    /// <summary>
    /// The text of the schema of the forms allowed, its properties always in the same order. The report is the object
    /// itself when it is the only form allowed, and admits null beside another; the summaries are a list, empty for
    /// another form.
    /// </summary>
    private static string Build(IReadOnlyList<string> allowed, bool comments)
    {
        bool Allows(AssistantForm form) => allowed.Contains(Codes.Of(form));
        var properties = new Dictionary<string, object>
        {
            ["form"] = new { type = "string", @enum = allowed },
            ["message"] = new { type = "string" },
            ["cited"] = Strings(),
        };
        if (Allows(AssistantForm.Questions))
            properties["questions"] = ListOf(new() { ["question"] = new { type = "string" }, ["choices"] = Strings() });
        if (Allows(AssistantForm.Findings))
        {
            properties["findings"] = ListOf(new()
            {
                ["kind"] = new { type = "string", @enum = FindingKinds }, ["title"] = new { type = "string" }, ["detail"] = new { type = "string" },
                ["objects"] = Strings(), ["suggestion"] = new { type = "string" },
            });
        }
        if (Allows(AssistantForm.Proposal))
            properties["pmap"] = OrNull(new { type = "string" });
        if (Allows(AssistantForm.Report))
            properties["report"] = allowed.Count == 1 ? Report() : OrNull(Report());
        if (Allows(AssistantForm.Summaries))
            properties["summaries"] = ListOf(new() { ["map"] = new { type = "string" }, ["text"] = new { type = "string" } });
        // The translations of the comments of presentation asked with a translation (ADR 099 revised), beside the `.pmap`.
        if (comments)
            properties["comments"] = OrNull(ListOf(new() { ["step"] = new { type = "string" }, ["text"] = new { type = "string" } }));
        return JsonSerializer.Serialize(Strict(properties));
    }

    /// <summary>An object whose properties are all required and none other admitted, as the structured outputs ask.</summary>
    private static Dictionary<string, object> Strict(Dictionary<string, object> properties) => new()
    {
        ["type"] = "object", ["properties"] = properties, ["required"] = properties.Keys.ToArray(), ["additionalProperties"] = false,
    };

    private static object Strings() => new { type = "array", items = new { type = "string" } };

    private static object ListOf(Dictionary<string, object> properties) => new { type = "array", items = Strict(properties) };

    /// <summary>A value, or null when the reply has none.</summary>
    private static object OrNull(object value) => new { anyOf = new[] { value, new { type = "null" } } };

    /// <summary>The report of a map (plan 3.1, L4): the texts the model writes; the figures and the diagrams are the application's.</summary>
    private static Dictionary<string, object> Report() => Strict(new()
    {
        ["summary"] = new { type = "string" },
        ["actors"] = new { type = "string" },
        ["chapters"] = ListOf(new() { ["title"] = new { type = "string" }, ["short"] = new { type = "string" }, ["steps"] = Strings(), ["story"] = new { type = "string" } }),
        ["journey"] = ListOf(new() { ["step"] = new { type = "string" }, ["short"] = new { type = "string" }, ["score"] = new { type = "integer" }, ["why"] = new { type = "string" } }),
        ["problems"] = ListOf(new()
        {
            ["source"] = new { type = "string", @enum = Sources }, ["pain_point"] = OrNull(new { type = "string" }),
            ["steps"] = Strings(), ["title"] = new { type = "string" }, ["short"] = new { type = "string" }, ["detail"] = new { type = "string" },
            ["kind"] = new { type = "string", @enum = FindingKinds },
        }),
        ["actions"] = ListOf(new()
        {
            ["title"] = new { type = "string" }, ["short"] = new { type = "string" }, ["detail"] = new { type = "string" },
            ["problems"] = new { type = "array", items = new { type = "integer" } }, ["owner"] = new { type = "string" },
            ["effort"] = new { type = "integer" }, ["impact"] = new { type = "integer" }, ["wave"] = new { type = "string", @enum = Waves },
        }),
        ["conclusion"] = new { type = "string" },
    });
}
