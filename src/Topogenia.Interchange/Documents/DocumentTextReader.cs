// DocumentTextReader.cs
// The text of a Word, Excel or Visio file joined to a request of the assistant, read by the host on the content of the file (never by Office), bounded and cut with a line that says so (spec 3.0 section 3.4, lot 4).

using System.Text;
using System.Xml;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;

namespace Topogenia.Interchange.Documents;

/// <summary>
/// Reads the text of a document of Office from its archive (<see cref="OfficeArchive"/>): Word by its paragraphs and tables,
/// Excel by its sheets and cells, Visio by its pages, shapes and links. A content that is not an archive of the format, an
/// XML refused (a DTD) or a limit passed gives null: the file is unreadable. The tests give smaller limits.
/// </summary>
public sealed class DocumentTextReader(OfficeLimits? limits = null, int textMax = AssistantBounds.FileTextMax) : IDocumentTextReader
{
    public string? Read(AttachmentKind kind, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var text = new BoundedText(textMax);
        try
        {
            using var archive = new OfficeArchive(content, limits ?? OfficeLimits.Default);
            var read = kind switch
            {
                AttachmentKind.Word => WordText.Read(archive, text),
                AttachmentKind.Excel => ExcelText.Read(archive, text),
                AttachmentKind.Visio => VisioText.Read(archive, text),
                _ => false,
            };
            return read ? text.ToString() : null;
        }
        catch (Exception failure) when (failure is InvalidDataException or XmlException or IOException or FormatException or OverflowException)
        {
            return null;
        }
    }
}

/// <summary>
/// The text of a document as it is written, bounded: past its bound nothing more is kept, and its end says that the text
/// was cut, so that the model knows it reads a part of the file.
/// </summary>
internal sealed class BoundedText(int max)
{
    private readonly StringBuilder text = new();

    /// <summary>The bound is reached: the readers stop there.</summary>
    public bool Full { get; private set; }

    public BoundedText Append(string? value)
    {
        if (Full || string.IsNullOrEmpty(value))
            return this;
        if (text.Length + value.Length > max)
        {
            text.Append(value.AsSpan(0, max - text.Length));
            Full = true;
        }
        else
            text.Append(value);
        return this;
    }

    public BoundedText Append(char value) => Append(value.ToString());

    /// <summary>Ends the line written, unless the text is empty or already ends one.</summary>
    public BoundedText EndLine()
    {
        if (text.Length > 0 && text[^1] != '\n')
            Append('\n');
        return this;
    }

    /// <summary>Removes the spaces and tabs that end the text written.</summary>
    public void TrimEnd(params char[] characters)
    {
        while (text.Length > 0 && characters.Contains(text[^1]))
            text.Length--;
    }

    public override string ToString()
    {
        var written = text.ToString().TrimStart('\n').TrimEnd();
        if (written.Length == 0)
            return "(the file holds no text)";
        return Full ? written + $"\n[The text was cut here: the application reads at most {max} characters of a file.]" : written;
    }
}
