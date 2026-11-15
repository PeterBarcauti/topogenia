// DesktopAddress.cs
// The address the desktop mode listens on: the only authority its requests may name (spec 5.2, A-4-19).

using System.Net;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using Topogenia.Host.Composition;
using Topogenia.Host.Security;

namespace Topogenia.Host.Desktop;

/// <summary>
/// The authority of the desktop mode, `127.0.0.1:port`: the port the server listens on once started (a free port when
/// the configuration gives 0), else the port of the configuration (a server that reports no address). `Host` must be
/// exactly this authority: a page of another site that rebinds its name onto the loopback (DNS rebinding) is refused.
/// </summary>
public sealed class DesktopAddress(IServer server, IOptions<ProductOptions> options) : IRequestAuthority
{
    public string Authority
    {
        get
        {
            var listened = server.Features.Get<IServerAddressesFeature>()?.Addresses
                .Select(address => Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri.Port : (int?)null)
                .FirstOrDefault(port => port is > 0);
            return $"{IPAddress.Loopback}:{listened ?? options.Value.Desktop.Port}";
        }
    }

    /// <summary>The origin of the application in the browser, `http://127.0.0.1:port`.</summary>
    public string Origin => $"{Uri.UriSchemeHttp}://{Authority}";

    /// <summary>The address the browser is opened on: the exchange of the launch token.</summary>
    public string LaunchAddress(string token) => $"{Origin}{DesktopSession.LaunchPath}?{DesktopSession.TokenParameter}={Uri.EscapeDataString(token)}";

    public bool Accepts(HostString host) => string.Equals(host.Value, Authority, StringComparison.OrdinalIgnoreCase);

    public string OriginOf(HttpRequest request) => Origin;
}
