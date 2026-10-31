// DesktopSettings.cs
// The optional settings.json of the user in desktop mode (port, data file, language), laid over the configuration (spec 6.1).

using System.Globalization;
using System.Text.Json;
using Topogenia.Host.Composition;

namespace Topogenia.Host.Desktop;

/// <summary>The settings file of the user cannot be used: the start stops with its code and the property at fault.</summary>
public sealed class DesktopSettingsException : Exception
{
    /// <summary>The code of the refusal (<see cref="DesktopSettings.InvalidSettings"/>).</summary>
    public const string Code = DesktopSettings.InvalidSettings;

    public DesktopSettingsException(string? property)
        : base(property is null ? Code : $"{Code}: {property}")
    {
        Property = property;
    }

    public DesktopSettingsException()
        : this(null)
    {
    }

    public DesktopSettingsException(string message, Exception inner)
        : base(message, inner)
    {
    }

    /// <summary>The property of the file at fault, or null when the file is no JSON object.</summary>
    public string? Property { get; }
}

/// <summary>
/// `settings.json` in the folder of the product (spec 6.1): an optional JSON object with `Port` (0 to 65535), `DataFile`
/// (a path, relative to the folder of the product) and `Language` (the default language of the interface). No
/// configuration is needed to start; a file that is present must be right: an unknown property, a value of the wrong type
/// or out of range stops the start with PARAMETRES_INVALIDES and the property, rather than being ignored (a misspelled
/// property would otherwise be silently lost). The values are laid over the configuration of the host.
/// </summary>
public static class DesktopSettings
{
    public const string InvalidSettings = "PARAMETRES_INVALIDES";

    private static readonly Dictionary<string, string> Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Port"] = $"{ProductOptions.Section}:Desktop:Port",
        ["DataFile"] = $"{ProductOptions.Section}:Desktop:DataFile",
        ["Language"] = $"{ProductOptions.Section}:Languages:Default",
    };

    /// <summary>Lays the settings file of the desktop mode over the configuration; nothing in server mode or without the file.</summary>
    public static void AddTo(ConfigurationManager configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = ProductOptions.Of(configuration);
        if (options.Mode != HostMode.Desktop)
            return;
        var file = DesktopPaths.SettingsFileOf(options.Desktop);
        if (File.Exists(file))
            configuration.AddInMemoryCollection(Read(File.ReadAllText(file)));
    }

    /// <summary>The values of a settings file, under the keys of the configuration.</summary>
    public static IReadOnlyDictionary<string, string?> Read(string json)
    {
        JsonElement root;
        try
        {
            root = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip }).RootElement;
        }
        catch (JsonException error)
        {
            throw new DesktopSettingsException(InvalidSettings, error);
        }
        if (root.ValueKind != JsonValueKind.Object)
            throw new DesktopSettingsException(null);
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!Keys.TryGetValue(property.Name, out var key))
                throw new DesktopSettingsException(property.Name);
            values[key] = ValueOf(property);
        }
        return values;
    }

    private static string ValueOf(JsonProperty property)
    {
        if (property.Name.Equals("Port", StringComparison.OrdinalIgnoreCase))
        {
            return property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var port) && port is >= 0 and <= 65535
                ? port.ToString(CultureInfo.InvariantCulture)
                : throw new DesktopSettingsException(property.Name);
        }
        return property.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(property.Value.GetString())
            ? property.Value.GetString()!.Trim()
            : throw new DesktopSettingsException(property.Name);
    }
}
