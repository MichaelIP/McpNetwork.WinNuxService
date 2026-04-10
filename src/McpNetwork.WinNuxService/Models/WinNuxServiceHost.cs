using McpNetwork.WinNuxService.Interfaces;

namespace McpNetwork.WinNuxService.Models;

public class WinNuxServiceHost
{
    internal IHost Host { get; }

    public WinNuxServiceInfo Info { get; }

    /// <summary>
    /// The application's service provider. Use this to resolve registered services,
    /// or to pass into plugin loading calls.
    /// </summary>
    public IServiceProvider Services => Host.Services;

    /// <summary>
    /// Provides runtime plugin management: load, start, stop, reload, unload.
    /// </summary>
    public IPluginManager Plugins { get; }

    internal WinNuxServiceHost(IHost host, WinNuxServiceInfo info)
    {
        Host = host;
        Info = info;
        Plugins = host.Services.GetService(typeof(IPluginManager)) as IPluginManager
            ?? throw new InvalidOperationException("IPluginManager is not registered in the service container.");
    }

    public WinNuxServiceHost ConfigureServices(Action<IServiceProvider> configure)
    {
        configure(Host.Services);
        return this;
    }

    public Task RunAsync() => Host.RunAsync();

    public Task StartAsync() => Host.StartAsync();

    public Task StopAsync() => Host.StopAsync();
}