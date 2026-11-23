// PmapReader.Extras.cs
// What a whole copy carries in a .pmap 1.5 (ADR 105): the comments of presentation of the steps and the flows, the glossary of the project, the code and the status of a pain point; read under their rules, left out of a proposal.

using System.Text.Json;
using Topogenia.Application.Presentation;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;

namespace Topogenia.Interchange.Pmap;

internal sealed partial class PmapReading
{
    private const string GlossaryField = "glossary";
    private const string CommentField = "comment";
    private const string PainPointCodeField = "pain_point_code";
    private const string PainPointStatusField = "pain_point_status";

    private static readonly string[] CommentProperties = ["text", "translations"];
    private static readonly IReadOnlyList<string> PainPointStatuses = Codes.All<PainPointStatus>();

    /// <summary>
    /// The file is of the version 1.5 (ADR 105): it may carry what a whole copy adds. A proposal of the assistant knows the
    /// properties and keeps none of their values: it writes no comment, glossary nor status before the lot that opens it.
    /// </summary>
    private bool extras;

    /// <summary>True when the additions of the 1.5 are read and kept: a file 1.5, not a proposal.</summary>
    private bool KeepsExtras => extras && !settings.Proposal;

    /// <summary>The glossary of the project (1.5): an entered text of several lines, its bound the one of the setting.</summary>
    private string? Glossary(JsonElement project) =>
        KeepsExtras ? Entered(project, GlossaryField, "project", PmapFormat.GlossaryMax, required: false) : null;

    /// <summary>
    /// The comment of presentation of a step or a flow (1.5): its line in the language of the file, `text`, and its other
    /// lines by language, each under the rule of every comment (`PresentationCommentRules.Text`: 500 characters, no markup,
    /// its line ends made `\n`); null when nothing is left. A translation in the language of the file gives way to `text`.
    /// </summary>
    private PmapComment? Comment(JsonElement element, string path)
    {
        if (!KeepsExtras || Object(element, CommentField, path, required: false) is not { } comment)
            return null;
        var place = Join(path, CommentField);
        Properties(comment, place, CommentProperties);
        var text = CommentLine(Text(comment, "text", place, required: false), Join(place, "text"));
        var translations = CommentTranslations(comment, place);
        return text is null && translations.Count == 0 ? null : new PmapComment(text, translations);
    }

    private Dictionary<string, string> CommentTranslations(JsonElement comment, string place)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Object(comment, "translations", place, required: false) is not { } translations)
            return result;
        foreach (var property in translations.EnumerateObject())
        {
            var at = $"{Join(place, "translations")}.{property.Name}";
            if (!Configured(property.Name, at))
                continue;
            if (property.Value.ValueKind != JsonValueKind.String)
                Fault(at, FileErrorCodes.TranslationNotText);
            else if (CommentLine(property.Value.GetString(), at) is { } text)
                result[property.Name] = text;
        }
        return result;
    }

    /// <summary>A line of a comment by the rule of every comment; a refusal is a fault at its place.</summary>
    private string? CommentLine(string? value, string path)
    {
        try
        {
            return PresentationCommentRules.Text(value);
        }
        catch (DomainException refusal)
        {
            Fault(path, refusal);
            return null;
        }
    }

    /// <summary>The code of a pain point (1.5), informative: only with the pain point it names, 32 characters at most.</summary>
    private string? PainPointCode(JsonElement use, string place, bool painPoint)
    {
        if (!KeepsExtras || Text(use, PainPointCodeField, place, required: false) is not { } code)
            return null;
        if (painPoint)
            return Checked(code.Trim(), Join(place, PainPointCodeField), PainPointCodeField, PmapFormat.PainPointCodeMax);
        Fault(Join(place, PainPointCodeField), DomainException.InvalidValue(PainPointCodeField));
        return null;
    }

    /// <summary>The status of a pain point (1.5): a code of its list, only with the pain point it describes.</summary>
    private string? PainPointStatusOf(JsonElement use, string place, bool painPoint)
    {
        if (!KeepsExtras || Enumeration(use, PainPointStatusField, place, PainPointStatuses) is not { } status)
            return null;
        if (painPoint)
            return status;
        Fault(Join(place, PainPointStatusField), DomainException.InvalidValue(PainPointStatusField));
        return null;
    }
}
