// PackageForge.cs
// Builds altered packages for the tests of the import: an entry changed (with or without its manifest), added, renamed or left out.

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Topogenia.Interchange.Json;
using Topogenia.Interchange.Packages;

namespace Topogenia.Interchange.Tests.Support;

/// <summary>
/// The entries of a package written by the export of <see cref="SampleProject"/>, changed at will and written again as a
/// ZIP. By default a changed entry also changes the manifest (size and SHA-256), so that the package fails only on what
/// the test aims at; <see cref="Raw(string, byte[])"/> writes the bytes without touching the manifest.
/// </summary>
public sealed class PackageForge
{
    private readonly List<(string Path, byte[] Content)> entries;

    public PackageForge(bool withoutPeople = false, SampleProject? sample = null)
    {
        sample ??= new SampleProject();
        var package = PackageWriter.Write(sample.Workspace(), sample.Logins, new PackageOptions(withoutPeople),
            new PackageStamp(SampleProject.At(500), withoutPeople ? null : "EXEMPLE\\alice"));
        entries = [.. new PackageFiles(package).Bytes.Select(pair => (pair.Key, pair.Value))];
        Sample = sample;
    }

    public SampleProject Sample { get; }

    public JsonNode Json(string path) => JsonNode.Parse(entries.Single(entry => entry.Path == path).Content)!;

    /// <summary>Changes the JSON of an entry and the manifest with it.</summary>
    public PackageForge With(string path, Action<JsonNode> change)
    {
        var node = Json(path);
        change(node);
        return Raw(path, CanonicalJson.Write(node)).Announce(path);
    }

    /// <summary>Changes the manifest alone.</summary>
    public PackageForge Manifest(Action<JsonObject> change)
    {
        var node = Json(PackageFormat.Manifest).AsObject();
        change(node);
        return Raw(PackageFormat.Manifest, CanonicalJson.Write(node));
    }

    /// <summary>Puts the bytes of an entry, added at the end when it is not there, the manifest left as it is.</summary>
    public PackageForge Raw(string path, byte[] content)
    {
        var index = entries.FindIndex(entry => entry.Path == path);
        if (index < 0)
            entries.Add((path, content));
        else
            entries[index] = (path, content);
        return this;
    }

    public PackageForge Raw(string path, string content) => Raw(path, Encoding.UTF8.GetBytes(content));

    public PackageForge Rename(string path, string name)
    {
        var index = entries.FindIndex(entry => entry.Path == path);
        entries[index] = (name, entries[index].Content);
        return this;
    }

    public PackageForge Without(string path)
    {
        entries.RemoveAll(entry => entry.Path == path);
        return this;
    }

    /// <summary>Writes the size and the SHA-256 of an entry in the manifest, as the export would.</summary>
    public PackageForge Announce(string path)
    {
        var content = entries.Single(entry => entry.Path == path).Content;
        return Manifest(manifest =>
        {
            var item = manifest["entries"]!.AsArray().OfType<JsonObject>().SingleOrDefault(candidate => (string?)candidate["path"] == path);
            if (item is null)
            {
                item = new JsonObject { ["path"] = path };
                manifest["entries"]!.AsArray().Add(item);
            }
            item["size"] = content.LongLength;
            item["sha256"] = Convert.ToHexStringLower(SHA256.HashData(content));
        });
    }

    public byte[] Bytes()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in entries)
            {
                using var item = archive.CreateEntry(path).Open();
                item.Write(content);
            }
        }
        return stream.ToArray();
    }
}
