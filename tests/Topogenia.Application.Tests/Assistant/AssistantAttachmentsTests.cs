// AssistantAttachmentsTests.cs
// The rules of the files joined to the requests of the assistant: the kinds told by the extension and confirmed by the content, the former formats, the bounds of each connector, the PDF protected, the turns of the conversation, the text of the documents (spec 3.0 section 3.4, lot 4).

using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;

namespace Topogenia.Application.Tests.Assistant;

public sealed class AssistantAttachmentsTests
{
    private const long Megabyte = 1024 * 1024;

    private static readonly byte[] Pdf = "%PDF-1.7\n1 0 obj << /Type /Catalog >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF"u8.ToArray();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16];
    private static readonly byte[] Gif = "GIF89a....."u8.ToArray();
    private static readonly byte[] Webp = "RIFF\x10\0\0\0WEBPVP8 "u8.ToArray();
    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04, 20, 0];

    /// <summary>A reader of documents that gives the kind it was asked, or nothing for a content that says « illisible ».</summary>
    private sealed class Reader : IDocumentTextReader
    {
        public List<AttachmentKind> Read { get; } = [];

        string? IDocumentTextReader.Read(AttachmentKind kind, byte[] content)
        {
            Read.Add(kind);
            return content.Length > 6 ? null : $"texte {kind}";
        }
    }

    private static AssistantFiles Files(params AssistantFileInput[] files) =>
        AssistantAttachmentRules.Read(files, 3, AssistantFileBounds.Of(AssistantConnector.Anthropic), new Reader());

    private static string Refusal(AssistantConnector connector, params AssistantFileInput[] files) =>
        Assert.Throws<DomainException>(() => AssistantAttachmentRules.Read(files, 3, AssistantFileBounds.Of(connector), new Reader())).Code;

    [Fact]
    public void Each_kind_is_told_by_its_extension_and_sent_as_it_is_or_as_the_text_the_host_read()
    {
        var reader = new Reader();
        var files = AssistantAttachmentRules.Read(
        [
            new("plan.PDF", Pdf), new("photo.png", Png), new("tableau.jpeg", Jpeg), new("anime.gif", Gif), new("image.webp", Webp),
            new("procédure.docx", Zip), new("suivi.xlsx", Zip), new("schéma.vsdx", Zip),
        ], 0, new AssistantFileBounds(10, Megabyte, Megabyte, 10 * Megabyte), reader);

        Assert.Equal(["application/pdf", "image/png", "image/jpeg", "image/gif", "image/webp"], files.Current.Where(file => file.Binary).Select(file => file.MediaType));
        Assert.Equal([AttachmentKind.Word, AttachmentKind.Excel, AttachmentKind.Visio], reader.Read);
        Assert.Equal(["texte Word", "texte Excel", "texte Visio"], files.Current.Where(file => !file.Binary).Select(file => file.Text));
        Assert.All(files.Current.Where(file => !file.Binary), file => Assert.Null(file.Content));
        Assert.Equal(Pdf.Length, files.Current[0].Size);
    }

    [Fact]
    public void The_name_keeps_the_file_only_without_its_folder_nor_its_control_characters()
    {
        var files = Files(new AssistantFileInput("C:\\Users\\x\\Documents\\Achat\u0007.pdf", Pdf), new AssistantFileInput("/tmp/a/b.png", Png));

        Assert.Equal(["Achat.pdf", "b.png"], files.Current.Select(file => file.Name));
        Assert.Equal(ErrorCodes.RequiredField, Refusal(AssistantConnector.Anthropic, new AssistantFileInput("  ", Pdf)));
        Assert.Equal(ErrorCodes.TextTooLong, Refusal(AssistantConnector.Anthropic, new AssistantFileInput(new string('a', AssistantAttachmentRules.NameMax) + ".pdf", Pdf)));
    }

    [Theory]
    [InlineData("ancien.doc", "docx")]
    [InlineData("ancien.XLS", "xlsx")]
    [InlineData("ancien.vsd", "vsdx")]
    public void A_former_format_of_office_is_refused_with_the_format_to_save_it_as(string name, string format)
    {
        var refusal = Assert.Throws<DomainException>(() => Files(new AssistantFileInput(name, Zip)));

        Assert.Equal(AssistantErrors.OutdatedFile, refusal.Code);
        Assert.Equal(format, refusal.Details["format"]);
        Assert.Equal(name, refusal.Details["name"]);
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("présentation.pptx")]
    [InlineData("sans-extension")]
    [InlineData("image.bmp")]
    public void Another_kind_is_refused(string name) =>
        Assert.Equal(AssistantErrors.FileFormat, Refusal(AssistantConnector.Anthropic, new AssistantFileInput(name, Zip)));

    [Fact]
    public void A_file_empty_or_whose_content_is_not_what_its_name_says_is_unreadable()
    {
        Assert.Equal(AssistantErrors.UnreadableFile, Refusal(AssistantConnector.Anthropic, new AssistantFileInput("vide.pdf", [])));
        Assert.Equal(AssistantErrors.UnreadableFile, Refusal(AssistantConnector.Anthropic, new AssistantFileInput("rien.pdf", null)));
        Assert.Equal(AssistantErrors.UnreadableFile, Refusal(AssistantConnector.Anthropic, new AssistantFileInput("photo.png", Jpeg)));
        Assert.Equal(AssistantErrors.UnreadableFile, Refusal(AssistantConnector.Anthropic, new AssistantFileInput("faux.docx", Pdf)));
        Assert.Equal(AssistantErrors.UnreadableFile, Refusal(AssistantConnector.Anthropic, new AssistantFileInput("faux.webp", "RIFF\x10\0\0\0WAVEfmt "u8.ToArray())));
        // The reader of the host gives nothing for a document it cannot read.
        Assert.Equal(AssistantErrors.UnreadableFile, Refusal(AssistantConnector.Anthropic, new AssistantFileInput("abîmé.docx", [.. Zip, 1, 2])));
    }

    [Fact]
    public void A_pdf_may_open_with_some_bytes_before_its_header_and_one_protected_by_a_password_is_refused()
    {
        Assert.Single(Files(new AssistantFileInput("décalé.pdf", [.. "\r\n\r\n"u8, .. Pdf])).Current);
        var protectedPdf = "%PDF-1.7\ntrailer << /Root 1 0 R /Encrypt 5 0 R >>"u8.ToArray();
        Assert.Equal(AssistantErrors.ProtectedFile, Refusal(AssistantConnector.Anthropic, new AssistantFileInput("secret.pdf", protectedPdf)));
    }

    [Fact]
    public void The_bounds_of_each_connector_are_those_of_its_provider()
    {
        Assert.Equal(new AssistantFileBounds(5, 10 * Megabyte, 7 * Megabyte, 20 * Megabyte), AssistantFileBounds.Of(AssistantConnector.Anthropic));
        Assert.Equal(new AssistantFileBounds(5, 4 * Megabyte, 7 * Megabyte / 2, 10 * Megabyte), AssistantFileBounds.Of(AssistantConnector.Bedrock));
        Assert.Equal(AssistantFileBounds.Of(AssistantConnector.Anthropic), AssistantFileBounds.Of(AssistantConnector.CopyPaste));
        var view = AssistantAttachmentRules.ViewOf(AssistantFileBounds.Of(AssistantConnector.Bedrock));
        Assert.Equal((5, 4 * Megabyte, 7 * Megabyte / 2, 10 * Megabyte), (view.Files, view.FileBytes, view.ImageBytes, view.TotalBytes));
        Assert.Equal([".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".docx", ".xlsx", ".vsdx"], view.Extensions);
        Assert.Equal("xlsx", view.Outdated[".xls"]);
    }

    [Fact]
    public void A_file_past_the_bound_of_its_kind_or_files_past_their_number_or_their_whole_are_refused()
    {
        byte[] Sized(byte[] head, long size) => [.. head, .. new byte[size - head.Length]];
        var bedrock = AssistantFileBounds.Of(AssistantConnector.Bedrock);
        Assert.Single(Files(new AssistantFileInput("grande.png", Sized(Png, bedrock.ImageBytes + 1))).Current);

        var image = Assert.Throws<DomainException>(() => AssistantAttachmentRules.Read([new("grande.png", Sized(Png, bedrock.ImageBytes + 1))], 0, bedrock, new Reader()));
        Assert.Equal((AssistantErrors.FileTooLarge, "3.5", "grande.png"), (image.Code, image.Details["max"], image.Details["name"]));
        Assert.Equal(AssistantErrors.FileTooLarge, Refusal(AssistantConnector.Bedrock, new AssistantFileInput("gros.pdf", Sized(Pdf, bedrock.FileBytes + 1))));
        Assert.Single(AssistantAttachmentRules.Read([new("juste.pdf", Sized(Pdf, bedrock.FileBytes))], 0, bedrock, new Reader()).Current);

        var three = Enumerable.Range(0, 3).Select(index => new AssistantFileInput($"part{index}.pdf", Sized(Pdf, 4 * Megabyte))).ToArray();
        var whole = Assert.Throws<DomainException>(() => AssistantAttachmentRules.Read(three, 0, bedrock, new Reader()));
        Assert.Equal((AssistantErrors.FilesTooLarge, "10"), (whole.Code, whole.Details["max"]));

        var six = Enumerable.Range(0, 6).Select(index => new AssistantFileInput($"f{index}.png", Png)).ToArray();
        var many = Assert.Throws<DomainException>(() => Files(six));
        Assert.Equal((AssistantErrors.TooManyFiles, (object?)5), (many.Code, many.Details["max"]));
        Assert.Equal(5, Files(six[..5]).Current.Count);
    }

    [Fact]
    public void The_files_of_the_former_turns_are_kept_with_their_turn_and_count_with_those_of_the_request()
    {
        var files = Files(new AssistantFileInput("a.pdf", Pdf, 0), new AssistantFileInput("b.png", Png, 2), new AssistantFileInput("c.png", Png, 0),
            new AssistantFileInput("d.png", Png));

        Assert.Equal(["a.pdf", "c.png"], files.Turns[0].Select(file => file.Name));
        Assert.Equal(["b.png"], files.Turns[2].Select(file => file.Name));
        Assert.Equal(["d.png"], files.Current.Select(file => file.Name));
        Assert.Equal(["a.pdf", "c.png", "b.png", "d.png"], files.All.Select(file => file.Name));
        Assert.Equal(ErrorCodes.InvalidValue, Refusal(AssistantConnector.Anthropic, new AssistantFileInput("x.png", Png, 3)));
        Assert.Equal(ErrorCodes.InvalidValue, Refusal(AssistantConnector.Anthropic, new AssistantFileInput("x.png", Png, -1)));
        Assert.Same(AssistantFiles.None, Files());
    }
}
