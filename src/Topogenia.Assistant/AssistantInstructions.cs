// AssistantInstructions.cs
// The texts sent to the model: the common instruction and the documentation of the formats (cached), the schema of the project (cached), the former turns, the message of the exchange; the whole prompt as one text for the copy and paste (spec 3.0 sections 3.1, 5, 7).

using System.Text;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Domain.Values;

namespace Topogenia.Assistant;

/// <summary>
/// The prompt of an exchange, in the order of the cache of the providers (tools, system, messages): first the common
/// instruction followed by the documentation of the formats, the same for every project and every call; then the schema of
/// the project, the same while the project does not change; then the former turns of the panel; then the message of the
/// exchange (the practice, the map open, the selection, the request), which changes at each call. The documentation is
/// the files of the repository embedded in this assembly: one source, it cannot drift from what the application reads.
/// </summary>
public sealed class AssistantInstructions : IAssistantInstructions
{
    /// <summary>The name of the package of exchange in its files, the root identifier of the solution (ADR 051).</summary>
    private static readonly string Package = nameof(Topogenia).ToLowerInvariant();

    /// <summary>The files of the documentation of the formats, in the order they are given (spec 3.0 section 5).</summary>
    public static readonly IReadOnlyList<string> FormatFiles =
    [
        "docs/pmap/PMAP_GUIDE_IA.md", "docs/format-pmap.md", "schemas/pmap-1.2.schema.json", $"docs/format-{Package}.md",
        $"schemas/{Package}-package-2.1.schema.json",
    ];

    /// <summary>The common instruction followed by the documentation of the formats: the first block of the system, cached.</summary>
    public static string System { get; } = BuildSystem();

    /// <summary>The instruction of a practice (a tile of the panel); none when the person writes directly.</summary>
    public static string? PracticeOf(AssistantIntent intent) => intent switch
    {
        AssistantIntent.Create => Resource("Instructions.practice-create.md"),
        AssistantIntent.Modify => Resource("Instructions.practice-modify.md"),
        AssistantIntent.Analyze => Resource("Instructions.practice-analyze.md"),
        AssistantIntent.Ask => Resource("Instructions.practice-ask.md"),
        AssistantIntent.Translate => Resource("Instructions.practice-translate.md"),
        _ => null,
    };

    /// <summary>The schema of the project with the line that says its format: the second block of the system, cached; null for a project yet to create.</summary>
    public static string? SchemaOf(AssistantPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        if (prompt.Schema is not { } schema)
            return null;
        var heading = prompt.SchemaFormat == AssistantSchemaFormat.Package
            ? $"# The schema of the project\n\nThe whole project as the entries of its exchange package (format .{Package} 2.0), without the people and the translations; each entry under a line `=== name ===`.\n\n"
            : "# The schema of the project\n\nThe whole project as one .pmap 1.1 document of all its maps, the sheets as the person may see them.\n\n";
        return heading + schema;
    }

    /// <summary>A former request of the person, as it was sent.</summary>
    public static string TurnRequest(AssistantTurn turn)
    {
        ArgumentNullException.ThrowIfNull(turn);
        return $"<request>\n{turn.Request}\n</request>";
    }

    /// <summary>
    /// The message of the exchange: the map open as a `.pmap` (none for a project yet to create), the codes selected, the
    /// languages (the one of origin of the project and those configured, ADR 081), the practice with its scope and the forms
    /// allowed, then the request.
    /// </summary>
    public static string MessageOf(AssistantPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var text = new StringBuilder();
        if (prompt.OpenMap is { } map)
            text.Append("<map name=\"").Append(Attribute(prompt.MapName ?? "")).Append("\" format=\"pmap 1.1\">\n").Append(map).Append("\n</map>\n\n");
        else
            text.Append("<map>none: the project does not exist yet</map>\n\n");
        text.Append("<selection>").Append(prompt.Selection.Count == 0 ? "none" : string.Join(", ", prompt.Selection)).Append("</selection>\n\n");
        if (prompt.Languages is { } languages)
        {
            text.Append("<languages");
            if (languages.Origin is { } origin)
                text.Append(" origin=\"").Append(Attribute(origin)).Append('"');
            text.Append(" configured=\"").Append(Attribute(string.Join(", ", languages.Configured))).Append('"');
            if (languages.Target is { } target)
                text.Append(" target=\"").Append(Attribute(target)).Append('"');
            text.Append(" />\n\n");
        }
        text.Append("<exchange practice=\"").Append(Codes.Of(prompt.Intent)).Append("\" scope=\"").Append(ScopeOf(prompt.Scope))
            .Append("\" allowed_forms=\"").Append(string.Join(", ", prompt.Forms.Select(Codes.Of))).Append("\">\n");
        if (PracticeOf(prompt.Intent) is { } practice)
            text.Append(practice).Append('\n');
        else
            text.Append("No tile was chosen: choose the form of your reply among the allowed forms.\n");
        if (!prompt.Forms.Contains(AssistantForm.Proposal))
            text.Append("A proposal is not allowed in this exchange (the person may read the map, not change it): answer, ask or analyse.\n");
        text.Append("</exchange>\n\n<request>\n").Append(prompt.Request).Append("\n</request>");
        return text.ToString();
    }

    /// <summary>
    /// The line that names a file joined (lot 4): before a PDF or an image, which follow as blocks of the provider, it
    /// closes itself; a document read by the host follows it as its text, then the end of the file (<see cref="FileText"/>).
    /// </summary>
    public static string FileHeading(AssistantAttachment file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return $"<file name=\"{Attribute(file.Name)}\" kind=\"{Codes.Of(file.Kind)}\"{(file.Binary ? " />" : ">")}";
    }

    /// <summary>A document read by the host as the model receives it: its heading, its text, its end.</summary>
    public static string FileText(AssistantAttachment file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return FileHeading(file) + "\n" + file.Text + "\n</file>";
    }

    /// <summary>
    /// The files of a request in the prompt copied for the AI of the person: the text of each document; a PDF or an image
    /// named only, for the person joins it in their AI.
    /// </summary>
    private static void CopiedFiles(StringBuilder text, IReadOnlyList<AssistantAttachment> files)
    {
        foreach (var file in files)
        {
            if (file.Binary)
                text.Append("<file name=\"").Append(Attribute(file.Name)).Append("\" kind=\"").Append(Codes.Of(file.Kind))
                    .Append("\">joined by the person to this message</file>\n\n");
            else
                text.Append(FileText(file)).Append("\n\n");
        }
    }

    /// <summary>
    /// The effort of the model (spec 3.0 section 5, set explicitly): `medium` for a question, `high` for what creates,
    /// changes or analyses.
    /// </summary>
    public static string EffortOf(AssistantIntent intent) => intent == AssistantIntent.Ask ? "medium" : "high";

    /// <summary>
    /// The whole prompt as one text, for the copy and paste: the same parts in the same order, the former turns written as
    /// such, the documents joined as their text (a PDF or an image named, for the person joins it in their AI), and the
    /// schema of the reply, which no provider then enforces.
    /// </summary>
    public string Render(AssistantPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var text = new StringBuilder(System).Append("\n\n");
        if (SchemaOf(prompt) is { } schema)
            text.Append(schema).Append("\n\n");
        if (prompt.History.Count > 0)
        {
            text.Append("# The former exchanges of the panel\n\n");
            foreach (var turn in prompt.History)
            {
                CopiedFiles(text, turn.Attachments);
                text.Append(TurnRequest(turn)).Append("\n<your_reply>\n").Append(turn.Reply).Append("\n</your_reply>\n\n");
            }
        }
        text.Append("# This exchange\n\n");
        CopiedFiles(text, prompt.Attachments);
        text.Append(MessageOf(prompt)).Append("\n\n");
        text.Append("# The schema of your reply\n\nReply with one JSON object that follows this JSON Schema, and nothing before or after it:\n\n")
            .Append(ReplySchema.Text);
        return text.ToString();
    }

    private static string ScopeOf(AssistantScope scope) => scope switch
    {
        AssistantScope.Project => "the whole project (the root map is open: every map of the project)",
        AssistantScope.NewProject => "a project yet to create",
        _ => "the map open",
    };

    private static string Attribute(string text) => text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal);

    private static string BuildSystem()
    {
        var text = new StringBuilder(Resource("Instructions.common.md")).Append("\n\n# The documentation of the formats\n");
        foreach (var file in FormatFiles)
            text.Append("\n## ").Append(file).Append("\n\n").Append(Resource("Formats." + Path.GetFileName(file))).Append('\n');
        return text.ToString();
    }

    /// <summary>A text embedded in this assembly, without the comment lines that open a Markdown file of the repository.</summary>
    public static string Resource(string name)
    {
        using var stream = typeof(AssistantInstructions).Assembly.GetManifestResourceStream(typeof(AssistantInstructions).Namespace + "." + name)
            ?? throw new InvalidOperationException($"The text {name} is not embedded.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var lines = reader.ReadToEnd().ReplaceLineEndings("\n").Split('\n');
        var start = 0;
        while (start < lines.Length && lines[start].StartsWith("<!--", StringComparison.Ordinal))
            start++;
        return string.Join('\n', lines[start..]).Trim();
    }

    /// <summary>The names of the texts embedded, for the tests.</summary>
    public static IReadOnlyList<string> Embedded() =>
        typeof(AssistantInstructions).Assembly.GetManifestResourceNames().Where(name => name.EndsWith(".md", StringComparison.Ordinal) || name.EndsWith(".json", StringComparison.Ordinal))
            .ToList();
}
