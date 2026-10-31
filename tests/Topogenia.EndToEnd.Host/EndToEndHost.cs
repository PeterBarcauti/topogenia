// EndToEndHost.cs
// The host of the journeys: built like the executable, in desktop mode on SQLite or in server mode on a SQL Server base of the product, a fixed port, the built interface, no browser, fake connectors of the assistant; the address to open written for the set-up of the journeys.

using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Topogenia.Host.Composition;
using Topogenia.Host.Desktop;
using Topogenia.Host.Startup;

namespace Topogenia.EndToEnd;

/// <summary>
/// Starts the application built by <see cref="HostApplication"/> like the executable, the static files of the built
/// interface from <c>--webroot</c>, in the folder of the run <c>--folder</c>:
/// <list type="bullet">
/// <item>desktop mode (spec 6.1): its SQLite file, backups, logs and keys in the folder, 127.0.0.1 and the port, no
/// browser opened. The launch token goes only to a browser (spec 5.2): instead of opening the default browser, the
/// starter writes the launch address to <see cref="LaunchFile"/>, which the set-up of the journeys opens in Chromium. The
/// token is read from the session of the run, as the tests of the host read it (<c>DesktopRun</c>): the product has no
/// other way out for it;</item>
/// <item>server mode (spec 6.2, <c>--mode server</c>): the base <see cref="StarterOptions.ServerDatabase"/> migrated first by the command of the product
/// (<c>--migrate</c>, which creates it), Windows authentication (Negotiate) on <c>localhost</c> and the port over HTTPS
/// (the server mode refuses HTTP, ADR 045) with a certificate made for the run, kept in memory and never installed, as the
/// tests of the host make it (<c>ServerRun</c>); the account that runs the journeys is administrator;
/// <see cref="LaunchFile"/> then holds the address of the application.</item>
/// </list>
/// In both modes, the connectors of the assistant are fakes (<see cref="JourneyServicesFactory"/>): no journey reaches a
/// real model. The host runs until it is stopped.
/// </summary>
public static class EndToEndHost
{
    /// <summary>The file of the folder that holds the address to open once the host listens.</summary>
    public const string LaunchFile = "launch-address.txt";

    public const int Success = 0;

    public const int WrongOptions = 2;

    public static async Task<int> RunAsync(string[] args, TextWriter output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (StarterOptions.Parse(args) is not { } options)
        {
            await output.WriteLineAsync(StarterOptions.Usage);
            return WrongOptions;
        }
        Directory.CreateDirectory(options.Folder);
        if (options.IsServer)
        {
            var migrated = await HostCommands.RunAsync([HostCommands.Migrate], output, builder => Configure(builder, options), cancellationToken);
            if (migrated != HostCommands.Success)
                return migrated;
        }
        await using var application = HostApplication.Build([StarterOptions.WebRootArgument, options.WebRoot], builder => Configure(builder, options));
        await application.StartAsync(cancellationToken);
        await WriteAddressAsync(options.IsServer ? $"https://localhost:{options.Port}/" : LaunchAddress(application.Services), options.Folder, cancellationToken);
        await application.WaitForShutdownAsync(cancellationToken);
        return Success;
    }

    private static void Configure(WebApplicationBuilder builder, StarterOptions options)
    {
        var section = ProductOptions.Section;
        var port = options.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var settings = new Dictionary<string, string?>();
        if (options.IsServer)
        {
            settings[$"{section}:Mode"] = nameof(HostMode.Server);
            settings[$"{section}:Database:Provider"] = nameof(DatabaseKind.SqlServer);
            settings[$"{section}:Database:ConnectionString"] = new SqlConnectionStringBuilder
            {
                DataSource = ".", InitialCatalog = StarterOptions.ServerDatabase, IntegratedSecurity = true, TrustServerCertificate = true, ApplicationName = "e2e",
            }.ConnectionString;
            settings[$"{section}:Server:AllowedHosts:0"] = "localhost";
            settings[$"{section}:Server:LogsFolder"] = Path.Combine(options.Folder, "logs");
            settings[$"{section}:Administrators:0"] = $@"{Environment.UserDomainName}\{Environment.UserName}";
            builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, options.Port, listen => listen.UseHttps(RunCertificate.Value)));
        }
        else
        {
            settings[$"{section}:Mode"] = nameof(HostMode.Desktop);
            settings[$"{section}:Desktop:Folder"] = options.Folder;
            settings[$"{section}:Desktop:Port"] = port;
            settings[$"{section}:Desktop:OpenBrowser"] = "false";
        }
        builder.Host.UseServiceProviderFactory(new JourneyServicesFactory(options.IsServer ? null : options.Account, options.RealAssistant));
        builder.Configuration.AddInMemoryCollection(settings);
    }

    /// <summary>One certificate for the run, for the migration and the server alike (only the server listens).</summary>
    private static readonly Lazy<X509Certificate2> RunCertificate = new(Certificate);

    /// <summary>A certificate of <c>localhost</c> for a day, in memory, loaded from its PKCS#12 form, which the TLS of Windows needs.</summary>
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

    private static string LaunchAddress(IServiceProvider services)
    {
        var token = services.GetRequiredService<DesktopSession>().LaunchToken
            ?? throw new InvalidOperationException("The launch token was spent before the starter read it.");
        return services.GetRequiredService<DesktopAddress>().LaunchAddress(token);
    }

    /// <summary>Written aside then moved into place, so that the set-up never reads half an address.</summary>
    private static async Task WriteAddressAsync(string address, string folder, CancellationToken cancellationToken)
    {
        var file = Path.Combine(folder, LaunchFile);
        var aside = file + ".part";
        await File.WriteAllTextAsync(aside, address, cancellationToken);
        File.Move(aside, file, overwrite: true);
    }
}
