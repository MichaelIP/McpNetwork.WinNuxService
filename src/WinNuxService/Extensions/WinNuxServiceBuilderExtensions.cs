using McpNetwork.WinNuxService.Adapters;
using McpNetwork.WinNuxService.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace McpNetwork.WinNuxService.Extensions;

public static class WinNuxServiceBuilderExtensions
{
    public static WinNuxServiceBuilder AddPluginService<TService>(
        this WinNuxServiceBuilder builder)
        where TService : class, IWinNuxService
    {
        builder.ConfigureServices((ctx, services) =>
        {
            services.AddSingleton<TService>();
            services.AddSingleton<IHostedService, ServiceAdapter<TService>>();
        });

        return builder;
    }
}