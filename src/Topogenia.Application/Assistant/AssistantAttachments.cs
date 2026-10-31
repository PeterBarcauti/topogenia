// AssistantAttachments.cs
// The files joined to the requests of the panel of the assistant: their kinds, the bounds of each connector, the checks of their names, sizes and contents, the text of the documents read by the host (spec 3.0 section 3.4, lot 4).

using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;

namespace Topogenia.Application.Assistant;

/// <summary>The kinds of the files joined: a PDF or an image sent as it is; a Word, Excel or Visio file read by the host and sent as text.</summary>
public enum AttachmentKind { Pdf, Image, Word, Excel, Visio }

/// <summary>A file joined by the person, as it came: its name, its content, the former turn it was joined to (null: the request of this exchange).</summary>
public sealed record AssistantFileInput(string? Name, byte[]? Content, int? Turn = null);

/// <summary>
/// A file joined, checked: its name, its kind, the media type it is sent as, its size; its content for a PDF or an image,
/// the text the host read from it for a Word, Excel or Visio file.
/// </summary>
public sealed record AssistantAttachment(string Name, AttachmentKind Kind, string MediaType, long Size, byte[]? Content, string? Text)
{
    /// <summary>The file is sent as it is (a PDF, an image), not as the text read by the host.</summary>
    public bool Binary => Content is not null;
}

/// <summary>The files of an exchange checked: those of its request and those of each former turn, by the index of the turn.</summary>
public sealed record AssistantFiles(IReadOnlyList<AssistantAttachment> Current, IReadOnlyDictionary<int, IReadOnlyList<AssistantAttachment>> Turns)
{
    public static AssistantFiles None { get; } = new([], new Dictionary<int, IReadOnlyList<AssistantAttachment>>());

    /// <summary>Every file of the conversation, those of the former turns first.</summary>
    public IEnumerable<AssistantAttachment> All => Turns.OrderBy(turn => turn.Key).SelectMany(turn => turn.Value).Concat(Current);
}

/// <summary>
/// The bounds of the files of a conversation for a connector, within the limits of its provider read on 30/10/2026: the
/// API of Anthropic takes requests of 128 MB and images of 10 MB in base64; Bedrock requests of 20 MB, images of 5 MB in
/// base64 and documents of 4.5 MB. The content goes in base64, a third larger; the prompt keeps its room.
/// </summary>
public sealed record AssistantFileBounds(int Files, long FileBytes, long ImageBytes, long TotalBytes)
{
    private const long Megabyte = 1024 * 1024;

    public static AssistantFileBounds Of(AssistantConnector connector) => connector == AssistantConnector.Bedrock
        ? new(5, 4 * Megabyte, 7 * Megabyte / 2, 10 * Megabyte)
        : new(5, 10 * Megabyte, 7 * Megabyte, 20 * Megabyte);
}

/// <summary>
/// The bounds of the files as the panel checks them before sending (the host checks them again): the most files of a
/// conversation, the largest file, image and whole, in bytes; the extensions admitted, and the former formats with the one
/// to save them as.
/// </summary>
public sealed record AssistantFileBoundsView(
    int Files,
    long FileBytes,
    long ImageBytes,
    long TotalBytes,
    IReadOnlyList<string> Extensions,
    IReadOnlyDictionary<string, string> Outdated);

/// <summary>
/// The rules of the files joined (spec 3.0 sections 3.4 and 6: hostile inputs): the kind from the extension, confirmed by
/// the first bytes of the content (a file whose content is not what its name says is unreadable); the former formats of
/// Office refused with the advice to save them in the recent one; a PDF protected by a password refused; the number and
/// the sizes bounded for the connector; the text of a Word, Excel or Visio file read by the host, bounded. Nothing is kept.
/// </summary>
public static class AssistantAttachmentRules
{
    /// <summary>The longest name of a file joined, in characters.</summary>
    public const int NameMax = 200;

    private static readonly Dictionary<string, (AttachmentKind Kind, string MediaType)> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = (AttachmentKind.Pdf, "application/pdf"),
        [".png"] = (AttachmentKind.Image, "image/png"),
        [".jpg"] = (AttachmentKind.Image, "image/jpeg"),
        [".jpeg"] = (AttachmentKind.Image, "image/jpeg"),
        [".gif"] = (AttachmentKind.Image, "image/gif"),
        [".webp"] = (AttachmentKind.Image, "image/webp"),
        [".docx"] = (AttachmentKind.Word, "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
        [".xlsx"] = (AttachmentKind.Excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
        [".vsdx"] = (AttachmentKind.Visio, "application/vnd.ms-visio.drawing"),
    };

    /// <summary>The former formats of Office, and the recent one to save them as (its extension without its dot, as a message says it).</summary>
    public static readonly IReadOnlyDictionary<string, string> Outdated = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".doc"] = "docx",
        [".xls"] = "xlsx",
        [".vsd"] = "vsdx",
    };

    /// <summary>The bounds of a connector as the panel reads them.</summary>
    public static AssistantFileBoundsView ViewOf(AssistantFileBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        return new AssistantFileBoundsView(bounds.Files, bounds.FileBytes, bounds.ImageBytes, bounds.TotalBytes, [.. Kinds.Keys], Outdated);
    }

    /// <summary>
    /// The files of a conversation checked, those of each former turn by its index (VALEUR_INVALIDE for a turn that does
    /// not exist); the refusals of <see cref="AssistantErrors"/> name the file in question.
    /// </summary>
    public static AssistantFiles Read(IReadOnlyList<AssistantFileInput>? files, int turns, AssistantFileBounds bounds, IDocumentTextReader reader)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(reader);
        if (files is null || files.Count == 0)
            return AssistantFiles.None;
        if (files.Count > bounds.Files)
            throw Refusal(AssistantErrors.TooManyFiles, ("max", bounds.Files));
        var current = new List<AssistantAttachment>();
        var byTurn = new Dictionary<int, List<AssistantAttachment>>();
        long total = 0;
        foreach (var file in files)
        {
            if (file?.Turn is { } turn && (turn < 0 || turn >= turns))
                throw DomainException.Invalid(ErrorCodes.InvalidValue, "files.turn", turns);
            var attachment = Check(file!, bounds, reader, ref total);
            if (file!.Turn is { } index)
            {
                if (!byTurn.TryGetValue(index, out var list))
                    byTurn[index] = list = [];
                list.Add(attachment);
            }
            else
                current.Add(attachment);
        }
        return new AssistantFiles(current, byTurn.ToDictionary(turn => turn.Key, turn => (IReadOnlyList<AssistantAttachment>)turn.Value));
    }

    private static AssistantAttachment Check(AssistantFileInput file, AssistantFileBounds bounds, IDocumentTextReader reader, ref long total)
    {
        var name = NameOf(file.Name);
        var dot = name.LastIndexOf('.');
        var extension = dot < 0 ? "" : name[dot..];
        if (Outdated.TryGetValue(extension, out var recent))
            throw Refusal(AssistantErrors.OutdatedFile, ("name", name), ("format", recent));
        if (!Kinds.TryGetValue(extension, out var known))
            throw Refusal(AssistantErrors.FileFormat, ("name", name));
        var content = file.Content;
        if (content is not { Length: > 0 })
            throw Refusal(AssistantErrors.UnreadableFile, ("name", name));
        var max = known.Kind == AttachmentKind.Image ? bounds.ImageBytes : bounds.FileBytes;
        if (content.LongLength > max)
            throw Refusal(AssistantErrors.FileTooLarge, ("name", name), ("max", Megabytes(max)));
        total += content.LongLength;
        if (total > bounds.TotalBytes)
            throw Refusal(AssistantErrors.FilesTooLarge, ("max", Megabytes(bounds.TotalBytes)));
        if (!Signed(content, known.MediaType))
            throw Refusal(AssistantErrors.UnreadableFile, ("name", name));
        if (known.Kind == AttachmentKind.Pdf && Protected(content))
            throw Refusal(AssistantErrors.ProtectedFile, ("name", name));
        if (known.Kind is AttachmentKind.Pdf or AttachmentKind.Image)
            return new AssistantAttachment(name, known.Kind, known.MediaType, content.LongLength, content, null);
        var text = reader.Read(known.Kind, content) ?? throw Refusal(AssistantErrors.UnreadableFile, ("name", name));
        return new AssistantAttachment(name, known.Kind, known.MediaType, content.LongLength, null, text);
    }

    /// <summary>The name of the file without any folder, its control characters removed; CHAMP_OBLIGATOIRE when empty, TEXTE_TROP_LONG past its bound.</summary>
    private static string NameOf(string? given)
    {
        var name = given?.Replace('\\', '/');
        name = name?[(name.LastIndexOf('/') + 1)..];
        return AssistantAskRules.Text(name?.ReplaceLineEndings(" "), "files.name", NameMax) ?? throw DomainException.Invalid(ErrorCodes.RequiredField, "files.name");
    }

    /// <summary>The content opens as its kind says: the signature of a PDF, of each format of image, of an archive of Office.</summary>
    private static bool Signed(byte[] content, string mediaType) => mediaType switch
    {
        "application/pdf" => Within(content, "%PDF-"u8, 1024),
        "image/png" => Starts(content, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        "image/jpeg" => Starts(content, [0xFF, 0xD8, 0xFF]),
        "image/gif" => Starts(content, "GIF87a"u8) || Starts(content, "GIF89a"u8),
        "image/webp" => content.Length > 12 && Starts(content, "RIFF"u8) && content.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        _ => Starts(content, [0x50, 0x4B, 0x03, 0x04]),
    };

    private static bool Starts(byte[] content, ReadOnlySpan<byte> signature) => content.AsSpan().StartsWith(signature);

    /// <summary>A PDF may open with some bytes before its header, which its readers admit within the first kilobyte.</summary>
    private static bool Within(byte[] content, ReadOnlySpan<byte> signature, int length) =>
        content.AsSpan(0, Math.Min(length, content.Length)).IndexOf(signature) >= 0;

    /// <summary>A PDF protected by a password names its encryption in its trailer (`/Encrypt`), which the providers refuse.</summary>
    private static bool Protected(byte[] content) => content.AsSpan().IndexOf("/Encrypt"u8) >= 0;

    /// <summary>A size in megabytes, as the messages say it (3.5, 10).</summary>
    private static string Megabytes(long bytes) =>
        (bytes / (1024m * 1024m)).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

    private static DomainException Refusal(string code, params (string Key, object? Value)[] values) =>
        new(code, RefusalKind.Invalid, values.ToDictionary(value => value.Key, value => value.Value));
}
