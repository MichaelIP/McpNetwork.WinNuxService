namespace McpNetwork.WinNuxService.Interfaces;

public interface IWinNuxPlugin
{
    void ConfigureServices(HostBuilderContext context, IServiceCollection services);
}