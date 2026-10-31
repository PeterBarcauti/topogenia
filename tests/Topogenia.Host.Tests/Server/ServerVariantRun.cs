// ServerVariantRun.cs
// The host started for real in server mode, with sources of configuration of the test that the settings of ServerRun cannot give.

using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
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
/// One run of the server mode built by <see cref="HostApplication"/> like the executable (as <see cref="ServerRun"/>):
/// SQL Server, the allowed host `localhost`, Kestrel in HTTPS on a free port of the loopback with a certificate made for
/// the run, Negotiate, the administrators in a JSON file read again at each change. The test may add its own sources of
/// configuration after those (<c>configure</c>, read by the services like the others), such as a source that counts its
/// readings. The client authenticates as the Windows account of the tests.
/// </summary>
public sealed class ServerVariantRun : IAsyncDisposable
{
    private readonly WebApplication application;
    private readonly X509Certificate2 certificate;
    private readonly DirectoryInfo folder;

    private ServerVariantRun(WebApplication application, X509Certificate2 certificate, DirectoryInfo folder)
    {
        this.application = application;
        this.certificate = certificate;
        this.folder = folder;
    }

    public IServiceProvider Services => application.Services;

    private static string AdministratorsFileOf(DirectoryInfo folder) => Path.Combine(folder.FullName, "administrators.json");

    private Uri Https => new(application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());

    /// <summary>Starts on <paramref name="database"/>, which the caller keeps, with <paramref name="administrators"/> in the watched file.</summary>
    public static async Task<ServerVariantRun> StartAsync(TestDatabase database, IReadOnlyList<string> administrators,
        Action<IConfigurationBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        var folder = Directory.CreateTempSubdirectory($"{TestDatabase.FolderPrefix}{Environment.ProcessId}-");
        var certificate = Certificate();
        var section = ProductOptions.Section;
        var settings = new Dictionary<string, string?>
        {
            [$"{section}:Mode"] = nameof(HostMode.Server),
            [$"{section}:Database:Provider"] = nameof(DatabaseKind.SqlServer),
            [$"{section}:Database:ConnectionString"] = database.ConnectionString,
            [$"{section}:Server:AllowedHosts:0"] = "localhost",
            [$"{section}:Server:LogsFolder"] = Path.Combine(folder.FullName, "logs"),
        };
        await WriteAdministratorsAsync(folder, [.. administrators]);
        var application = HostApplication.Build([], builder =>
        {
            builder.Configuration.AddInMemoryCollection(settings);
            builder.Configuration.AddJsonFile(AdministratorsFileOf(folder), optional: false, reloadOnChange: true);
            configure?.Invoke(builder.Configuration);
            builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
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
            throw;
        }
        return new ServerVariantRun(application, certificate, folder);
    }

    /// <summary>A client of `https://localhost:port` that authenticates as the Windows account of the tests.</summary>
    public HttpClient Client()
    {
        var handler = new SocketsHttpHandler
        {
            UseCookies = false,
            AllowAutoRedirect = false,
            Credentials = CredentialCache.DefaultCredentials,
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, presented, _, _) =>
                    presented is not null && presented.GetCertHashString() == certificate.GetCertHashString(),
            },
        };
        return new HttpClient(handler) { BaseAddress = new Uri($"https://localhost:{Https.Port}") };
    }

    /// <summary>Names the administrators in the file the host watches.</summary>
    public Task SetAdministratorsAsync(params string[] logins) => WriteAdministratorsAsync(folder, logins);

    private static Task WriteAdministratorsAsync(DirectoryInfo folder, string[] logins) =>
        File.WriteAllTextAsync(AdministratorsFileOf(folder),
            JsonSerializer.Serialize(new Dictionary<string, object> { [ProductOptions.Section] = new { Administrators = logins } }),
            TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        await application.StopAsync(CancellationToken.None);
        await application.DisposeAsync();
        certificate.Dispose();
        folder.Delete(recursive: true);
    }

    private static X509Certificate2 Certificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], critical: false));
        using var made = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(made.Export(X509ContentType.Pfx), null);
    }
}
