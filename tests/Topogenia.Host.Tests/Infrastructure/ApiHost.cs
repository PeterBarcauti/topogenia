// ApiHost.cs
// The host started in memory on a migrated test database, SQLite or SQL Server, and its clients, one per login.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Host.Composition;
using Topogenia.Host.Desktop;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Infrastructure;

/// <summary>
/// The whole host (composition, middlewares, routes) on a base of its own migrated by the code under test, with the
/// authentication of the tests in place of the one of the mode (AR-17): the SQLite base in desktop mode (its checks of
/// `Host`, `Origin` and `Sec-Fetch-Site`, its start, its backups), the SQL Server base in server mode (HTTPS, its allowed
/// host, If-Match required). The configuration names two administrators (<see cref="Logins.Admin"/>, and
/// <see cref="Logins.Owner"/> who creates the projects that the administrator must not be a member of) and the languages
/// French and English, French by default. Each test creates its own project: the tests of a class share the base.
/// </summary>
public sealed class ApiHost : IAsyncDisposable
{
    private readonly TestDatabase database;
    private readonly WebApplicationFactory<Program> factory;

    private ApiHost(TestDatabase database, WebApplicationFactory<Program> factory)
    {
        this.database = database;
        this.factory = factory;
    }

    public EngineKind Kind => database.Kind;

    public static async Task<ApiHost> StartAsync(EngineKind kind)
    {
        var database = await TestDatabase.MigratedAsync(kind);
        var section = ProductOptions.Section;
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            if (kind == EngineKind.Sqlite)
            {
                builder.UseSetting($"{section}:Mode", nameof(HostMode.Desktop));
                builder.UseSetting($"{section}:Desktop:Folder", Path.GetDirectoryName(database.Name));
                builder.UseSetting($"{section}:Desktop:DataFile", database.Name);
                builder.UseSetting($"{section}:Desktop:Port", DesktopPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.UseSetting($"{section}:Desktop:OpenBrowser", "false");
            }
            else
            {
                builder.UseSetting($"{section}:Mode", nameof(HostMode.Server));
                builder.UseSetting($"{section}:Database:Provider", nameof(DatabaseKind.SqlServer));
                builder.UseSetting($"{section}:Database:ConnectionString", database.ConnectionString);
                builder.UseSetting($"{section}:Server:AllowedHosts:0", ServerHost);
                builder.UseSetting($"{section}:Server:LogsFolder", Path.Combine(Path.GetTempPath(), $"{TestDatabase.FolderPrefix}{Environment.ProcessId}-logs"));
            }
            builder.UseSetting($"{section}:Administrators:0", Logins.Admin);
            builder.UseSetting($"{section}:Administrators:1", Logins.Owner);
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(TestAuthentication.Name)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthentication>(TestAuthentication.Name, null);
                // The connectors of the assistant answer in memory: no test of the host reaches a real model (spec 3.0 section 8).
                services.RemoveAll<IAssistantConnector>();
                services.AddSingleton<IAssistantConnector>(new TestConnector(AssistantConnector.Anthropic));
                services.AddSingleton<IAssistantConnector>(new TestConnector(AssistantConnector.Bedrock));
                // The Negotiate scheme of the server mode reads the connection that Kestrel gives and the server in memory
                // does not: it stays registered, run by the authentication of the tests (AR-17). The real Negotiate runs on
                // Kestrel in `Server/ServerRunTests`.
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    if (options.SchemeMap.TryGetValue(NegotiateDefaults.AuthenticationScheme, out var negotiate))
                        negotiate.HandlerType = typeof(TestAuthentication);
                });
            });
        });
        factory.ClientOptions.BaseAddress = kind == EngineKind.Sqlite ? new Uri($"http://127.0.0.1:{DesktopPort}") : new Uri($"https://{ServerHost}");
        return new ApiHost(database, factory);
    }

    /// <summary>The port of the desktop mode in the tests: the address of the clients, so that `Host` is `127.0.0.1:port`.</summary>
    public const int DesktopPort = 5170;

    /// <summary>The name of the server mode in the tests, its only allowed host: the clients call it in HTTPS (spec 5.3).</summary>
    public const string ServerHost = "topogenia.test";

    /// <summary>A client authenticated as <paramref name="login"/>, or anonymous when it is null.</summary>
    public ApiClient As(string? login)
    {
        var client = factory.CreateClient();
        if (login is not null)
            client.DefaultRequestHeaders.Add(TestAuthentication.Header, login);
        return new ApiClient(client);
    }

    /// <summary>
    /// A client of <paramref name="login"/> that sends each body as it is given, straight to the server in memory: the
    /// clients of <see cref="As"/> pass through the redirect handler of the factory, which buffers a body to send it
    /// again and so always announces its length; this one sends a body of unknown length in chunks, without length.
    /// </summary>
    public ApiClient Unbuffered(string login)
    {
        var client = new HttpClient(factory.Server.CreateHandler()) { BaseAddress = factory.ClientOptions.BaseAddress };
        client.DefaultRequestHeaders.Add(TestAuthentication.Header, login);
        return new ApiClient(client);
    }

    /// <summary>
    /// Runs <paramref name="work"/> with a client of <paramref name="login"/> on a variant of the host whose services are
    /// changed (a failing adapter), on the same base; the variant is stopped afterwards.
    /// </summary>
    public async Task WithServicesAsync(Action<IServiceCollection> services, string login, Func<ApiClient, Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        await using var variant = VariantOf(services);
        var client = variant.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthentication.Header, login);
        using var api = new ApiClient(client);
        await work(api);
    }

    /// <summary>
    /// Runs <paramref name="work"/> on a variant of the host whose services are changed, on the same base, with the
    /// clients of the variant (<paramref name="work"/> gets a maker of clients by login, anonymous for null, which it
    /// disposes); the variant is stopped afterwards.
    /// </summary>
    public async Task WithVariantAsync(Action<IServiceCollection> services, Func<Func<string?, ApiClient>, Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        await using var variant = VariantOf(services);
        await work(login =>
        {
            var client = variant.CreateClient();
            if (login is not null)
                client.DefaultRequestHeaders.Add(TestAuthentication.Header, login);
            return new ApiClient(client);
        });
    }

    /// <summary>
    /// A second host on the same base, with <paramref name="services"/> changed. In desktop mode the named lock of the data
    /// file (<see cref="DesktopInstance"/>), held by the first host, would refuse its start (INSTANCE_DEJA_LANCEE): the
    /// variant takes the lock of another name, the two hosts of one test sharing the file as one application would.
    /// </summary>
    private WebApplicationFactory<Program> VariantOf(Action<IServiceCollection> services) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(all =>
        {
            if (Kind == EngineKind.Sqlite)
            {
                all.AddSingleton(provider =>
                {
                    var paths = provider.GetRequiredService<DesktopPaths>();
                    return new DesktopInstance(paths with { DataFile = $"{paths.DataFile}.variant" });
                });
            }
            services(all);
        }));

    /// <summary>The services of the host, for what a test must read without a route.</summary>
    public IServiceProvider Services => factory.Services;

    public async ValueTask DisposeAsync()
    {
        await factory.DisposeAsync();
        await database.DisposeAsync();
    }
}

/// <summary>
/// The logins of the tests: the administrators of the configuration, the members of a project of <see cref="ApiProject"/>,
/// a stranger. <see cref="Owner"/> is a second administrator, used only to create the projects of which
/// <see cref="Admin"/> is no member.
/// </summary>
public static class Logins
{
    public const string Admin = @"TEST\admin";
    public const string Owner = @"TEST\olga";
    public const string Facilitator = @"TEST\anne";
    public const string Contributor = @"TEST\paul";
    public const string Reader = @"TEST\lea";
    public const string Stranger = @"TEST\marc";
}

/// <summary>One host per engine, started at its first use and shared by the tests of a class.</summary>
public sealed class ApiHosts : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<EngineKind, ApiHost> hosts = [];

    public async Task<ApiHost> OfAsync(EngineKind kind)
    {
        await gate.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            if (!hosts.TryGetValue(kind, out var host))
                hosts[kind] = host = await ApiHost.StartAsync(kind);
            return host;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var host in hosts.Values)
            await host.DisposeAsync();
        gate.Dispose();
    }
}

