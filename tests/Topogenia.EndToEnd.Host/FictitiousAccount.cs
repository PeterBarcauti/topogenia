// FictitiousAccount.cs
// The services of a run of the journeys, replaced once the host declared them all: the connectors of the assistant by fakes, and the login given to the desktop mode in the place of the Windows account of the process.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Host.Desktop;

namespace Topogenia.EndToEnd;

/// <summary>The account of the process as the desktop mode reads it, here a login given by <c>--account</c>.</summary>
public sealed class FictitiousAccount(string login) : IProcessAccount
{
    public string Login { get; } = login;
}

/// <summary>
/// Builds the services of the host once all of them are declared: the host declares its connectors and its own account
/// in <c>AddTopogenia</c>, so only the building of the services comes after them. The connectors of the assistant are
/// always replaced by <see cref="JourneyConnector"/>; the account of the process by <see cref="FictitiousAccount"/> when a
/// login is given. Nothing else changes; the product has no such option.
/// </summary>
public sealed class JourneyServicesFactory(string? login, bool realAssistant = false) : IServiceProviderFactory<IServiceCollection>
{
    private readonly DefaultServiceProviderFactory defaults = new();

    public IServiceCollection CreateBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!realAssistant)
        {
            services.RemoveAll<IAssistantConnector>();
            services.AddSingleton<IAssistantConnector>(new JourneyConnector(AssistantConnector.Anthropic));
            services.AddSingleton<IAssistantConnector>(new JourneyConnector(AssistantConnector.Bedrock));
        }
        if (login is not null)
            services.AddSingleton<IProcessAccount>(new FictitiousAccount(login));
        return services;
    }

    public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder) => defaults.CreateServiceProvider(containerBuilder);
}
