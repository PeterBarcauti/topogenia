// OfficeArchive.cs
// A file of Office (Word, Excel, Visio) opened from its content for its text: the ZIP archive of the Open Packaging Conventions, its entries and their uncompressed sizes bounded while they are read, its XML read without DTD nor resolver (spec 3.0 section 3.4, lot 4).

using System.IO.Compression;
using System.Xml;

namespace Topogenia.Interchange.Documents;

/// <summary>The limits of an archive of Office read for its text: its entries, each entry and the whole read, uncompressed.</summary>
public sealed record OfficeLimits(int MaxEntries, long Entry, long Total)
{
    public static OfficeLimits Default { get; } = new(10_000, 64L * 1024 * 1024, 256L * 1024 * 1024);
}

/// <summary>
/// The parts of an archive of Office, read for their text only: a part is an entry of the ZIP archive, read in a stream
/// that stops at the first byte beyond the limit of an entry or of the whole (a bomb of compression stops there), its XML
/// read without DTD nor external resolver; the relations of a part (`_rels/*.rels`) resolved to the names of the parts
/// they target, never outside the archive. Every fault is an <see cref="InvalidDataException"/> or an
/// <see cref="XmlException"/>, which the reader of the documents turns into a file unreadable.
/// </summary>
internal sealed class OfficeArchive : IDisposable
{
    /// <summary>The relation of the root to the main part of a Word or Excel file.</summary>
    public const string OfficeDocument = "/officeDocument";

    /// <summary>The relation of the root to the main part of a Visio file.</summary>
    public const string VisioDocument = "/document";

    private readonly ZipArchive archive;
    private readonly Dictionary<string, ZipArchiveEntry> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly OfficeLimits limits;
    private long read;

    public OfficeArchive(byte[] content, OfficeLimits limits)
    {
        this.limits = limits;
        archive = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
        var all = archive.Entries;
        if (all.Count > limits.MaxEntries)
            throw new InvalidDataException("Too many entries.");
        foreach (var entry in all)
            entries.TryAdd(entry.FullName.Replace('\\', '/').TrimStart('/'), entry);
    }

    public bool Has(string part) => entries.ContainsKey(part);

    /// <summary>
    /// The node is of a vocabulary of Office (`wordprocessingml`, `spreadsheetml`, `visio`), told by the path of its
    /// namespace, the same in the transitional form of the format (`.../wordprocessingml/2006/main`) and its strict form
    /// (`.../wordprocessingml/main`).
    /// </summary>
    public static bool In(XmlReader xml, string vocabulary)
    {
        var space = xml.NamespaceURI;
        return space.EndsWith("/main", StringComparison.Ordinal) && space.Contains("/" + vocabulary + "/", StringComparison.Ordinal);
    }

    /// <summary>The identifier of the relation an element names (`r:id`, in the namespace of the relations of either form); null without one.</summary>
    public static string? RelationOf(XmlReader xml)
    {
        string? id = null;
        if (xml.MoveToFirstAttribute())
        {
            do
            {
                if (xml.LocalName == "id" && xml.NamespaceURI.EndsWith("/relationships", StringComparison.Ordinal))
                    id = xml.Value;
            }
            while (id is null && xml.MoveToNextAttribute());
            xml.MoveToElement();
        }
        return id;
    }

    /// <summary>The XML of a part, or null when the archive has none of that name.</summary>
    public XmlReader? Xml(string part)
    {
        if (!entries.TryGetValue(part, out var entry))
            return null;
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            CloseInput = true,
        };
        return XmlReader.Create(new BoundedStream(entry.Open(), this), settings);
    }

    /// <summary>The main part named by the relations of the root, or the usual name of the format when they name none.</summary>
    public string Main(string relation, string usual) => Target(Relations(""), relation) ?? usual;

    /// <summary>The part of the first relation of a type (`/styles`, `/pages`) that the archive holds; null when none.</summary>
    public string? Target(IReadOnlyDictionary<string, (string Part, string Type)> relations, string type) =>
        relations.Values.Where(target => target.Type.EndsWith(type, StringComparison.Ordinal) && Has(target.Part)).Select(target => target.Part).FirstOrDefault();

    /// <summary>The relations of a part (`""` for the root): by identifier, the part each targets inside the archive and its type.</summary>
    public IReadOnlyDictionary<string, (string Part, string Type)> Relations(string part)
    {
        var slash = part.LastIndexOf('/');
        var folder = slash < 0 ? "" : part[..(slash + 1)];
        var name = slash < 0 ? part : part[(slash + 1)..];
        var relations = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        using var xml = Xml($"{folder}_rels/{name}.rels");
        if (xml is null)
            return relations;
        while (xml.Read())
        {
            if (xml.NodeType != XmlNodeType.Element || xml.LocalName != "Relationship" || xml.GetAttribute("TargetMode") == "External")
                continue;
            var id = xml.GetAttribute("Id");
            var target = xml.GetAttribute("Target");
            if (id is not null && target is not null && Resolve(folder, target) is { } resolved)
                relations[id] = (resolved, xml.GetAttribute("Type") ?? "");
        }
        return relations;
    }

    /// <summary>A target relative to the folder of its part, or from the root (`/xl/...`); null when it leaves the archive.</summary>
    private static string? Resolve(string folder, string target)
    {
        var path = target.StartsWith('/') ? target[1..] : folder + target;
        var parts = new List<string>();
        foreach (var piece in path.Replace('\\', '/').Split('/'))
        {
            if (piece is "" or ".")
                continue;
            if (piece == "..")
            {
                if (parts.Count == 0)
                    return null;
                parts.RemoveAt(parts.Count - 1);
            }
            else
                parts.Add(piece);
        }
        return string.Join('/', parts);
    }

    public void Dispose() => archive.Dispose();

    /// <summary>The stream of an entry, which counts what it gives against the limit of an entry and of the whole archive.</summary>
    private sealed class BoundedStream(Stream inner, OfficeArchive owner) : Stream
    {
        private long given;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => given; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var got = inner.Read(buffer, offset, count);
            given += got;
            owner.read += got;
            if (given > owner.limits.Entry || owner.read > owner.limits.Total)
                throw new InvalidDataException("An entry of the archive is too large once uncompressed.");
            return got;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
