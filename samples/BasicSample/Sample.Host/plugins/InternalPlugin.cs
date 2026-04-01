using McpNetwork.WinNuxService.Adapters;
using McpNetwork.WinNuxService.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sample.Host.Services;

namespace Sample.Host.plugins
{
    internal class InternalPlugin : IWinNuxPlugin
    {
        public void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            services.AddSingleton<InternalPluginService>();
            services.AddSingleton<IHostedService, ServiceAdapter<InternalPluginService>>();
        }
    }
}
