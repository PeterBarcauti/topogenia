// HostServices.cs
// The composition of the host: configuration, mode, database, adapters of the ports, templates, handlers, start, JSON, OpenAPI.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Topogenia.Application.Commands;
using Topogenia.Application.Exchange;
using Topogenia.Application.Maps;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Assistant;
using Topogenia.Host.Api;
using Topogenia.Host.Assistant;
using Topogenia.Host.Desktop;
using Topogenia.Host.Errors;
using Topogenia.Host.Json;
using Topogenia.Host.Maintenance;
using Topogenia.Host.Server;
using Topogenia.Host.Startup;
using Topogenia.Host.Users;
using Topogenia.Interchange.Packages;
using Topogenia.Interchange.Pmap;
using Topogenia.Persistence.Assistant;
using Topogenia.Persistence.Errors;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Purge;
using Topogenia.Persistence.Reading;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Sqlite;
using Topogenia.Persistence.SqlServer;
using Topogenia.Persistence.Writing;

namespace Topogenia.Host.Composition;

/// <summary>
/// Registers what the host uses. The mode (`Mode` of the section of the product, desktop by default, spec 6.1, 6.2) is read once from the
/// configuration, the user settings of the desktop mode laid over it first (<see cref="DesktopSettings"/>); each mode
/// adds its own parts (<see cref="DesktopServices"/>, <see cref="ServerServices"/>). One <see cref="StoreSession"/> per request is shared by the
/// adapters of the persistence (ADR 030); the templates and the handlers of the routes (the classes `*Api` of `Api/`, found
/// by their name) are scoped with it. Every route requires an authenticated user (A-1.1-02): the fallback policy.
/// </summary>
public static class HostServices
{
    public static WebApplicationBuilder AddTopogenia(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        AddLogging(builder.Logging);
        DesktopSettings.AddTo(builder.Configuration);
        var options = ProductOptions.Of(builder.Configuration);
        var services = builder.Services;
        services.Configure<ProductOptions>(builder.Configuration.GetSection(ProductOptions.Section));
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<ProductOptions>>().Value.ToSettings());
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<ProductOptions>>().Value.Languages.ToSettings());
        services.AddSingleton<EngineDialect>(provider => provider.GetRequiredService<DatabaseEngine>() is SqliteEngine ? new SqliteDialect() : new SqlServerDialect());
        AddPersistence(services);
        AddApplication(services);
        // The console of the process; a test may give its own before (TryAdd).
        services.TryAddSingleton(provider => StartupConsole.Of(provider.GetRequiredService<Domain.Values.LanguageSettings>()));
        services.AddHostedService<StartupService>();
        services.AddMaintenance();
        services.AddSingleton<RefusalWatch>();
        if (options.Mode == HostMode.Desktop)
        {
            builder.AddDesktop(options.Desktop);
        }
        else
        {
            builder.AddServer(options.Server);
        }
        services.AddAuthorization(authorization =>
            authorization.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        services.AddSingleton(ErrorMessages.Embedded());
        services.AddSingleton<ErrorResponses>();
        services.ConfigureHttpJsonOptions(json => ApiJson.Configure(json.SerializerOptions));
        services.Configure<RouteHandlerOptions>(routes => routes.ThrowOnBadRequest = true);
        services.AddOpenApi(openApi => openApi.AddSchemaTransformer<OptionalSchemaTransformer>().AddSchemaTransformer<CodeEnumSchemaTransformer>()
            .AddSchemaTransformer<NonNullItemsSchemaTransformer>().AddDocumentTransformer<ErrorSchemaTransformer>());
        return builder;
    }

    /// <summary>
    /// The daily file of the mode is the only log (spec 5.2, ADR 036): the default providers are removed, among them the
    /// event log of Windows, which the users of the machine read. The framework writes paths (content root, web root, keys)
    /// and the query string of each request in its own messages: only its errors are kept, which the file writes with an
    /// exception by its type alone.
    /// </summary>
    public static void AddLogging(ILoggingBuilder logging)
    {
        ArgumentNullException.ThrowIfNull(logging);
        logging.ClearProviders();
        logging.AddFilter("Microsoft", LogLevel.Error);
        logging.AddFilter("System", LogLevel.Error);
    }

    /// <summary>The handlers of the routes: the classes of `Api/` whose name ends with `Api`.</summary>
    public static IReadOnlyList<Type> Handlers() =>
        [.. typeof(HostServices).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, Namespace: not null }
                && type.Namespace.StartsWith($"{typeof(Api.StructureReply).Namespace}.", StringComparison.Ordinal)
                && type.Name.EndsWith("Api", StringComparison.Ordinal))];

    private static void AddApplication(IServiceCollection services)
    {
        services.AddSingleton<IMapViewBuilder>(provider => new MapViewBuilder(provider.GetRequiredService<Domain.Model.AppSettings>()));
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<SignInCache>();
        services.AddScoped<RequestUser>();
        services.AddScoped<ICurrentUser>(provider => provider.GetRequiredService<RequestUser>());
        services.AddScoped<RequestVersions>();
        services.AddScoped<IVersionRequirement>(provider => provider.GetRequiredService<RequestVersions>());
        services.AddScoped<UserPorts>();
        services.AddScoped<CommandRunner>();
        services.AddScoped<QueryRunner>();
        services.AddScoped<UserRunner>();
        services.AddSingleton<IPackageExporter, PackageExporter>();
        services.AddScoped<PackageExport>();
        services.AddSingleton<IPackageImporter, PackageImporter>();
        services.AddScoped<PackageImport>();
        services.AddSingleton<IPmapFormat, PmapFiles>();
        services.AddScoped<PmapCopyExport>();
        AddAssistant(services);
        foreach (var handler in Handlers())
            services.AddScoped(handler);
    }

    /// <summary>
    /// The assistant (3.0): the protection of its secret by the data protection of the host (configured with the keys of the
    /// folder of the product in desktop mode), the connectors of the list, and their ports for the requests. A test host
    /// replaces the connectors by fakes, so that no test reaches a real service.
    /// </summary>
    private static void AddAssistant(IServiceCollection services)
    {
        // The name of the product, not the folder of the installation (the default), isolates the keys: a version deployed
        // in another folder still reads the secret kept. Where the keys live is the choice of each mode.
        services.AddDataProtection().SetApplicationName(Application.ProductIdentity.Name);
        services.AddSingleton<ISecretProtector>(provider => new DataProtectionSecrets(provider.GetRequiredService<IDataProtectionProvider>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAssistantConnector, AnthropicConnector>(_ => new AnthropicConnector()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAssistantConnector, BedrockConnector>(_ => new BedrockConnector()));
        services.AddScoped<AssistantPorts>();
        services.AddSingleton<IAssistantInstructions, AssistantInstructions>();
        services.AddSingleton<IDocumentTextReader>(_ => new Interchange.Documents.DocumentTextReader());
        services.AddScoped<Application.Assistant.AssistantExchanges>();
    }

    private static void AddPersistence(IServiceCollection services)
    {
        services.AddScoped(provider => new StoreSession(provider.GetRequiredService<DatabaseEngine>(), provider.GetRequiredService<EngineDialect>()));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAccessReader, AccessReader>();
        services.AddScoped<IWorkspaceStore, WorkspaceStore>();
        services.AddScoped<IOperationLog, OperationLogStore>();
        services.AddScoped<IUserReader, UserReader>();
        services.AddScoped<IProjectReader, ProjectReader>();
        services.AddScoped<ITrashPurge>(provider => new TrashPurge(provider.GetRequiredService<StoreSession>()));
        services.AddScoped<IAuditStore, AuditStore>();
        services.AddScoped<IAssistantSettingsStore, AssistantSettingsStore>();
        services.AddSingleton<ISchemaMigrations>(provider => new SchemaMigrations(provider.GetRequiredService<DatabaseEngine>(), provider.GetRequiredService<IClock>()));
    }
}
