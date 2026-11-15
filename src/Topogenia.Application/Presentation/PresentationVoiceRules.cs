// PresentationVoiceRules.cs
// The rules of the voice of a project (ADR 103, lot J10): its name, what a facilitator choosing a voice for a language changes, the version read of its line, what a package brings.

using Topogenia.Application.Commands;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Presentation;

/// <summary>The voice asked for a language: its name as the browser gives it, and whether it is a voice of the computer (otherwise a voice read online).</summary>
public readonly record struct VoiceChoice(string Name, bool Local);

/// <summary>
/// The rules of the voice a project keeps for a language (ADR 103). Its language follows the rule of the language of a
/// comment of presentation (<see cref="PresentationCommentRules.Language"/>): one of the languages of the configuration.
/// A voice has neither original nor translation, so that none of the rules of the lines of a comment
/// (<see cref="CommentLineRules"/>) applies to it but the check of the version read, written once for both
/// (<see cref="RecordVersion.EnsureLine"/>).
/// </summary>
public static class PresentationVoiceRules
{
    /// <summary>The longest name of a voice: the names the browsers give hold in a few dozen characters.</summary>
    public const int NameMax = 200;

    /// <summary>The type of object that the refusal of a version read names for the voice of a language: its table.</summary>
    public const string ObjectType = "presentation_voice";

    /// <summary>The field of the name of a voice, named by a refusal.</summary>
    public const string NameField = "name";

    /// <summary>The field that says whether a voice is one of the computer, named by a refusal.</summary>
    public const string LocalField = "local";

    /// <summary>The key of the voices of a project in a package, on its project: the field a refusal of its lines names.</summary>
    public const string PackageField = "presentationVoices";

    /// <summary>
    /// The name of a voice: an entered text (trimmed, without markup nor control character, TEXTE_TROP_LONG beyond
    /// <see cref="NameMax"/>) on one line (a line end or a tabulation is VALEUR_INVALIDE); null when it is empty.
    /// </summary>
    public static string? Name(string? name)
    {
        var text = TextRules.Optional(name, NameField, NameMax);
        return text is not null && text.Any(character => character is '\n' or '\r' or '\t') ? throw DomainException.InvalidValue(NameField) : text;
    }

    /// <summary>
    /// The voice a request asks for: none when its name is empty or absent (the voice kept is then removed, and where it
    /// is read is not asked); otherwise its name with where it is read, which the request must say (CHAMP_OBLIGATOIRE on
    /// <see cref="LocalField"/>: an absent field is never read as a voice of the computer nor as a voice read online).
    /// </summary>
    public static VoiceChoice? Asked(string? name, bool? local) =>
        Name(name) is { } text ? new VoiceChoice(text, local ?? throw DomainException.Invalid(ErrorCodes.RequiredField, LocalField)) : null;

    /// <summary>
    /// The If-Match of a request on the voice of the language it writes: nothing to check without a version read;
    /// CONFLIT_VERSION, object <see cref="ObjectType"/>, when the voice kept (none: version 0) is no longer the one read.
    /// </summary>
    public static void EnsureRead(PresentationVoice? kept, int? versionRead) =>
        RecordVersion.EnsureLine(versionRead, kept?.Version ?? 0, ObjectType, kept?.UpdatedBy, kept?.UpdatedAt);

    /// <summary>
    /// What a facilitator choosing a voice for a language changes, over the voice the project keeps for that language
    /// (<paramref name="kept"/>, none: null): the voice asked is written over it, its version moved on by one, by the
    /// actor of the context at its time; no voice asked removes the one kept. Nothing (null) when the very voice is
    /// already kept, or when there is none to remove.
    /// </summary>
    public static VoiceWrite? Chosen(Guid projectId, string language, VoiceChoice? asked, PresentationVoice? kept, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (asked is not { } voice)
            return kept is null ? null : new VoiceWrite(projectId, language, null, kept.Version);
        if (kept is not null && kept.Local == voice.Local && string.Equals(kept.Name, voice.Name, StringComparison.Ordinal))
            return null;
        var version = kept?.Version ?? 0;
        return new VoiceWrite(projectId, language, new PresentationVoice(projectId, language, voice.Name, voice.Local, version + 1, context.Now, context.ActorId), version);
    }

    /// <summary>The voices a project keeps once a write is done.</summary>
    public static IReadOnlyList<PresentationVoice> After(IReadOnlyList<PresentationVoice> kept, VoiceWrite write)
    {
        ArgumentNullException.ThrowIfNull(kept);
        ArgumentNullException.ThrowIfNull(write);
        var others = kept.Where(voice => voice.Language != write.Language);
        return [.. write.Line is { } line ? others.Append(line) : others];
    }

    /// <summary>
    /// The voices a package brings for its project (format 2.4): those of a language this installation does not configure
    /// are left out, as the comments of presentation and the translations of the core (AR-119); a name that its rule
    /// refuses or changes, or two voices for one language, refuse the package, PAQUET_INVALIDE, reason <c>values</c>, field
    /// <c>presentationVoices</c> (the schema already refuses such a name: this is the last guard).
    /// </summary>
    public static IReadOnlyList<PresentationVoice> Imported(IReadOnlyList<PresentationVoice> voices, LanguageSettings languages)
    {
        ArgumentNullException.ThrowIfNull(voices);
        ArgumentNullException.ThrowIfNull(languages);
        if (voices.Select(voice => voice.Language).Distinct(StringComparer.Ordinal).Count() != voices.Count)
            throw ImportedRecords.Refusal([PackageField]);
        var kept = new List<PresentationVoice>();
        foreach (var voice in voices.Where(voice => languages.IsSupported(voice.Language)))
        {
            string? name;
            try
            {
                name = Name(voice.Name);
            }
            catch (DomainException)
            {
                throw ImportedRecords.Refusal([PackageField]);
            }
            if (name != voice.Name)
                throw ImportedRecords.Refusal([PackageField]);
            kept.Add(voice);
        }
        return kept;
    }
}
