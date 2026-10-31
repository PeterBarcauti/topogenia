// StarterOptions.cs
// The options of the starter of the host for the journeys: the folder of the run, the port, the built interface, the mode, and the account of the demonstration.

using System.Globalization;

namespace Topogenia.EndToEnd;

/// <summary>
/// The options <c>--folder</c>, <c>--port</c> and <c>--webroot</c>, each given once with its value, <c>--mode</c>
/// (<c>desktop</c> by default, or <c>server</c>) and <c>--account</c>. The server mode writes only the base
/// <see cref="ServerDatabase"/> of the local instance: a base of the product, never one of the 1.x (prompt 4-2).
/// <c>--account</c> (desktop mode only) gives a fictitious login <c>DOMAIN\account</c> to the user of the run, in the
/// place of the Windows account of the process: the captures of the demonstration (P6-T11) show no real login nor name
/// of machine (prompt 4-5). <c>--assistant real</c> keeps the real connectors of the assistant, for an essay by hand
/// with the agreement of the owner of the product (spec 3.0 section 8); never given by the journeys, whose connectors are
/// fakes.
/// </summary>
public sealed record StarterOptions(string Folder, int Port, string WebRoot, bool IsServer, string? Account = null, bool RealAssistant = false)
{
    /// <summary>The argument of the host that names the folder of its static files.</summary>
    public const string WebRootArgument = "--webroot";

    /// <summary>The base of the journeys in server mode (prompt 5: `Topogenia_E2E`).</summary>
    public const string ServerDatabase = "Topogenia_E2E";

    public const string Usage = "Usage: --folder <folder of the run> --port <1-65535> --webroot <folder of the built interface> [--mode desktop|server] [--account DOMAIN\\account] [--assistant fake|real]";

    private static readonly string[] Names = ["--folder", "--port", WebRootArgument, "--mode", "--account", "--assistant"];

    /// <summary>The options read from <paramref name="args"/>, or null when one is missing, repeated, unknown or wrong.</summary>
    public static StarterOptions? Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Count % 2 != 0)
            return null;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Count; index += 2)
        {
            if (!Names.Contains(args[index], StringComparer.Ordinal) || !values.TryAdd(args[index], args[index + 1]))
                return null;
        }
        if (!values.TryGetValue("--folder", out var folder) || !values.TryGetValue("--port", out var port)
            || !values.TryGetValue(WebRootArgument, out var webRoot))
            return null;
        if (!int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number is < 1 or > 65535)
            return null;
        if (!File.Exists(Path.Combine(webRoot, "index.html")))
            return null;
        var mode = values.GetValueOrDefault("--mode", "desktop");
        if (mode is not ("desktop" or "server"))
            return null;
        var account = values.GetValueOrDefault("--account");
        if (account is not null && (mode == "server" || !IsLogin(account)))
            return null;
        var assistant = values.GetValueOrDefault("--assistant", "fake");
        if (assistant is not ("fake" or "real"))
            return null;
        return new StarterOptions(Path.GetFullPath(folder), number, Path.GetFullPath(webRoot), mode == "server", account, assistant == "real");
    }

    /// <summary>A login <c>DOMAIN\account</c>: one backslash, a name on each side.</summary>
    private static bool IsLogin(string value)
    {
        var parts = value.Split('\\');
        return parts.Length == 2 && parts.All(part => part.Length > 0 && part.Trim() == part);
    }
}
