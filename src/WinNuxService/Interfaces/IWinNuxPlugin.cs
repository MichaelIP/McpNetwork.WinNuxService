using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace McpNetwork.WinNuxService.Interfaces;

public interface IWinNuxPlugin
{
    void ConfigureServices(HostBuilderContext context, IServiceCollection services);
}