// HostApplication.cs
// The web application of the host, built the same way by the executable and by the tests that start a real server.

namespace Topogenia.Host.Composition;

/// <summary>
/// Builds the application for its mode: the configuration (with the user settings of the desktop mode), the services,
/// then the pipeline. A test may add its configuration before the services read it. The content root is the folder of
/// the executable, whatever the current folder of the process (a shortcut, a service, a command run from elsewhere): the
/// `appsettings.json` delivered with it, its section of the product and its levels of log are always read.
/// </summary>
public static class HostApplication
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });
        configure?.Invoke(builder);
        builder.AddTopogenia();
        var application = builder.Build();
        application.UseTopogenia();
        return application;
    }
}
