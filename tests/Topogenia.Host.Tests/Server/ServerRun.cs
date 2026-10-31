// ServerRun.cs
// The host started for real in server mode: Kestrel in HTTPS and HTTP on the loopback, Windows authentication, a migrated SQL Server test base.

using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Topogenia.Host.Composition;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Server;

/// <summary>
/// One run of the executable in server mode, built by <see cref="HostApplication"/> like the executable: SQL Server, the
/// allowed hosts <see cref="ServerName"/> and `localhost`, a certificate made for the run (never installed: kept in memory
/// and loaded from its PKCS#12 form, which the TLS of Windows needs), one HTTPS and one HTTP address on free ports, the
/// static files, the log and the administrators (a JSON file read again at each change) in a temporary folder. The
/// clients keep no cookie and follow no redirection; <see cref="Client"/> authenticates as the Windows account of the
/// tests (Negotiate, NTLM on the loopback), <see cref="Anonymous"/> sends no credentials.
/// </summary>
public sealed class ServerRun : IAsyncDisposable
{
    /// <summary>The fictitious name of the server, an allowed host outside the hosts that HSTS leaves out (localhost).</summary>
    public const string ServerName = "topogenia.test";

    private readonly WebApplication application;
    private readonly X509Certificate2 certificate;
    private readonly TestDatabase? database;

    private ServerRun(WebApplication application, X509Certificate2 certificate, TestDatabase? database, DirectoryInfo folder)
    {
        this.application = application;
        this.certificate = certificate;
        this.database = database;
        Folder = folder;
    }

    public DirectoryInfo Folder { get; }

    public IServiceProvider Services => application.Services;

    public string AdministratorsFile => Path.Combine(Folder.FullName, "administrators.json");

    public string LogsFolder => Path.Combine(Folder.FullName, "logs");

    public string WebRoot => Path.Combine(Folder.FullName, "wwwroot");

    private IReadOnlyList<Uri> Addresses => [.. application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Select(address => new Uri(address))];

    public Uri Https => Addresses.Single(address => address.Scheme == Uri.UriSchemeHttps);

    public Uri Http => Addresses.Single(address => address.Scheme == Uri.UriSchemeHttp);

    /// <summary>Starts on a new migrated SQL Server base, with <paramref name="settings"/> laid over the configuration.</summary>
    public static async Task<ServerRun> StartAsync(IReadOnlyDictionary<string, string?>? settings = null) =>
        await StartAsync(await TestDatabase.MigratedAsync(EngineKind.SqlServer), settings, ownsDatabase: true);

    /// <summary>Starts on <paramref name="database"/>, which the caller keeps; a refused start is thrown as it is.</summary>
    public static async Task<ServerRun> StartAsync(TestDatabase database, IReadOnlyDictionary<string, string?>? settings, bool ownsDatabase) =>
        await StartAsync(database, settings, ownsDatabase, withHttps: true);

    /// <summary>Starts on <paramref name="database"/>; without <paramref name="withHttps"/>, the server listens on HTTP only.</summary>
    public static async Task<ServerRun> StartAsync(TestDatabase database, IReadOnlyDictionary<string, string?>? settings, bool ownsDatabase, bool withHttps)
    {
        ArgumentNullException.ThrowIfNull(database);
        var folder = Directory.CreateTempSubdirectory($"{TestDatabase.FolderPrefix}{Environment.ProcessId}-");
        var certificate = Certificate();
        var section = ProductOptions.Section;
        var configuration = new Dictionary<string, string?>
        {
            [$"{section}:Mode"] = nameof(HostMode.Server),
            [$"{section}:Database:Provider"] = nameof(DatabaseKind.SqlServer),
            [$"{section}:Database:ConnectionString"] = database.ConnectionString,
            [$"{section}:Server:AllowedHosts:0"] = ServerName,
            [$"{section}:Server:AllowedHosts:1"] = "localhost",
            [$"{section}:Server:LogsFolder"] = Path.Combine(folder.FullName, "logs"),
            [$"{section}:Languages:Supported:0"] = "fr",
            [$"{section}:Languages:Supported:1"] = "en",
            [$"{section}:Languages:Default"] = "fr",
            [$"{section}:Languages:FirstLaneNames:fr"] = "Acteur 1",
            [$"{section}:Languages:FirstLaneNames:en"] = "Actor 1",
        };
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
            configuration[key] = value;
        var webRoot = Directory.CreateDirectory(Path.Combine(folder.FullName, "wwwroot"));
        await File.WriteAllTextAsync(Path.Combine(webRoot.FullName, "index.html"), "<!doctype html><title>Topogenia</title>", TestContext.Current.CancellationToken);
        var administrators = Path.Combine(folder.FullName, "administrators.json");
        await File.WriteAllTextAsync(administrators, "{}", TestContext.Current.CancellationToken);
        var application = HostApplication.Build(["--webroot", webRoot.FullName], builder =>
        {
            builder.Configuration.AddInMemoryCollection(configuration);
            builder.Configuration.AddJsonFile(administrators, optional: false, reloadOnChange: true);
            builder.WebHost.ConfigureKestrel(kestrel =>
            {
                if (withHttps)
                    kestrel.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate));
                kestrel.Listen(IPAddress.Loopback, 0);
            });
        });
        try
        {
            await application.StartAsync(TestContext.Current.CancellationToken);
        }
        catch
        {
            await application.DisposeAsync();
            certificate.Dispose();
            folder.Delete(recursive: true);
            if (ownsDatabase)
                await database.DisposeAsync();
            throw;
        }
        return new ServerRun(application, certificate, ownsDatabase ? database : null, folder);
    }

    /// <summary>A client of the HTTPS address that authenticates as the Windows account of the tests.</summary>
    public HttpClient Client() => NewClient(Https, CredentialCache.DefaultCredentials);

    /// <summary>A client of the HTTPS address without credentials.</summary>
    public HttpClient Anonymous() => NewClient(Https, null);

    /// <summary>A client of the HTTP address without credentials.</summary>
    public HttpClient Plain() => NewClient(Http, null);

    /// <summary>Names the administrators in the file the host watches.</summary>
    public Task SetAdministratorsAsync(params string[] logins) =>
        File.WriteAllTextAsync(AdministratorsFile,
            System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object> { [ProductOptions.Section] = new { Administrators = logins } }),
            TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        await application.StopAsync(CancellationToken.None);
        await application.DisposeAsync();
        certificate.Dispose();
        Folder.Delete(recursive: true);
        if (database is not null)
            await database.DisposeAsync();
    }

    private HttpClient NewClient(Uri address, ICredentials? credentials)
    {
        var handler = new SocketsHttpHandler
        {
            UseCookies = false,
            AllowAutoRedirect = false,
            Credentials = credentials,
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, presented, _, _) =>
                    presented is not null && presented.GetCertHashString() == certificate.GetCertHashString(),
            },
        };
        return new HttpClient(handler) { BaseAddress = address };
    }

    private static X509Certificate2 Certificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={ServerName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(ServerName);
        names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], critical: false));
        using var made = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(made.Export(X509ContentType.Pfx), null);
    }
}
