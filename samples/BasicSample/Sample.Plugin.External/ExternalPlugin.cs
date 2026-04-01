using McpNetwork.WinNuxService.Adapters;
using McpNetwork.WinNuxService.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Sample.Plugin.External;

/// <summary>
///     This class is the entry point for the plugin. 
///     It implements IWinNuxPlugin, which allows it to register services and hosted services with the host application.
/// </summary>
public class ExternalPlugin : IWinNuxPlugin
{
    public void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<ExternalPluginService>();
        services.AddSingleton<IHostedService, ServiceAdapter<ExternalPluginService>>();
    }
}
