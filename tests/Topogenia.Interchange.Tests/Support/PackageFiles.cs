// PackageFiles.cs
// Reads the archive of a package in the tests: the paths in their order and the bytes and JSON of each entry.

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Topogenia.Interchange.Tests.Support;

/// <summary>The entries of a package archive, read without the code under test.</summary>
public sealed class PackageFiles
{
    public PackageFiles(byte[] package)
    {
        using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            using var stream = entry.Open();
            using var content = new MemoryStream();
            stream.CopyTo(content);
            Paths.Add(entry.FullName);
            Bytes[entry.FullName] = content.ToArray();
        }
    }

    public List<string> Paths { get; } = [];

    public Dictionary<string, byte[]> Bytes { get; } = new(StringComparer.Ordinal);

    public string Text(string path) => Encoding.UTF8.GetString(Bytes[path]);

    public JsonNode Json(string path) => JsonNode.Parse(Bytes[path])!;

    public JsonElement Element(string path) => JsonDocument.Parse(Bytes[path]).RootElement;
}
