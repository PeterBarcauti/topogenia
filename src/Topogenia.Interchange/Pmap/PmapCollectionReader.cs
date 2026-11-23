// PmapCollectionReader.cs
// Reading of a template collection .pmaps (ADR 114): the envelope by the rules of the elements of a .pmap, then each process handed whole to the reader of the .pmap.

using System.Text.Json;
using Topogenia.Domain.Errors;

namespace Topogenia.Interchange.Pmap;

/// <summary>A process of a collection: its rank, its text as read, and what the reader of the `.pmap` made of it.</summary>
public sealed record PmapCollectionProcess(int Index, string Content, PmapRead Read)
{
    public string? Name { get; init; }
}

/// <summary>A collection read: its name, its description and its processes; none of them, and the faults, when its envelope is refused.</summary>
public sealed record PmapCollectionRead(string? Name, string? Description, IReadOnlyList<PmapCollectionProcess> Processes, IReadOnlyList<FileError> Errors);

/// <summary>
/// Reads a template collection (ADR 114, `docs/format-collection.md`). The envelope is cleaned and parsed as a `.pmap` is,
/// then checked by the same rules of its elements (known properties, required ones, texts under the rules of every entered
/// text): a fault of the envelope refuses the whole file and no process is read. Each process is then handed, as the text
/// the file holds, to <see cref="PmapReader.Read"/> with the settings given: no rule of the `.pmap` is written twice, and a
/// fault of a process refuses that process alone.
/// </summary>
public static class PmapCollectionReader
{
    public static PmapCollectionRead Read(string? text, PmapReadSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new PmapReading(settings).ReadCollection(text ?? string.Empty);
    }
}

/// <summary>The envelope of a collection, read by the elements of the reading of a file.</summary>
internal sealed partial class PmapReading
{
    private static readonly string[] CollectionProperties = ["$schema", "format", "version", "name", "description", "processes"];

    public PmapCollectionRead ReadCollection(string text)
    {
        using var json = Parse(text);
        if (json is null)
            return CollectionRefused();
        var root = json.RootElement;
        Properties(root, "", CollectionProperties);
        var format = Text(root, "format", "", required: true);
        if (format is not null && format != PmapFormat.CollectionName)
        {
            Fault("format", FileErrorCodes.WrongFormat, ("expected", PmapFormat.CollectionName));
            return CollectionRefused();
        }
        var version = Text(root, "version", "", required: true);
        if (version is not null && version != PmapFormat.CollectionVersion)
        {
            Fault("version", FileErrorCodes.WrongVersion, ("version", version), ("supported", PmapFormat.CollectionVersion));
            return CollectionRefused();
        }
        var name = Entered(root, "name", "", PmapFormat.ProjectNameMax, required: true);
        var description = Entered(root, "description", "", PmapFormat.DescriptionMax, required: false);
        var processes = Processes(root);
        if (errors.Count > 0 || name is null || processes is null)
            return CollectionRefused();
        return new PmapCollectionRead(name, description, [.. processes.Select((process, index) =>
        {
            var content = process.GetRawText();
            return new PmapCollectionProcess(index, content, PmapReader.Read(content, settings)) { Name = WrittenName(process) };
        })], []);
    }

    private PmapCollectionRead CollectionRefused() => new(null, null, [], errors);

    /// <summary>
    /// The name of a process as the file writes it (`project.name`), so that a process in fault can be recognised: only a
    /// text of 1 to <see cref="PmapFormat.ProjectNameMax"/> characters without a control character, null otherwise. It is
    /// a text, never interpreted.
    /// </summary>
    private static string? WrittenName(JsonElement process)
    {
        if (!process.TryGetProperty("project", out var project) || project.ValueKind != JsonValueKind.Object
            || !project.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
            return null;
        var text = name.GetString();
        return text is { Length: > 0 and <= PmapFormat.ProjectNameMax } && !text.Any(char.IsControl) ? text : null;
    }

    /// <summary>The processes of the envelope: 1 to <see cref="PmapFormat.ProcessesMax"/>, each an object; null with the faults otherwise.</summary>
    private List<JsonElement>? Processes(JsonElement root)
    {
        if (List(root, "processes", "", required: true) is not { } list)
            return null;
        var count = list.GetArrayLength();
        if (count == 0)
        {
            Fault("processes", FileErrorCodes.Required, ("name", "processes"));
            return null;
        }
        if (count > PmapFormat.ProcessesMax)
        {
            Fault("processes", CollectionErrors.TooManyProcesses, ("count", count), ("max", PmapFormat.ProcessesMax));
            return null;
        }
        // An element that is not an object is a fault of the envelope, as its schema says it (ADR 114): the whole file is refused.
        var processes = new List<JsonElement>();
        foreach (var (process, index) in list.EnumerateArray().Select((process, index) => (process, index)))
        {
            if (process.ValueKind == JsonValueKind.Object)
                processes.Add(process);
            else
                Fault($"processes[{index}]", FileErrorCodes.NotAnObject, ("name", $"processes[{index}]"));
        }
        return processes.Count == count ? processes : null;
    }
}
