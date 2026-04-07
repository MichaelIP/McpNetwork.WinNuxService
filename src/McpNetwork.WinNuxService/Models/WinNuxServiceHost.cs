using McpNetwork.WinNuxService.Interfaces;

namespace McpNetwork.WinNuxService.Models;

public class WinNuxServiceHost
{
    private readonly IHost _host;

    public WinNuxServiceInfo Info { get; }

    /// <summary>
    /// The application's service provider. Use this to resolve registered services,
    /// or to pass into plugin loading calls.
    /// </summary>
    public IServiceProvider Services => _host.Services;

    /// <summary>
    /// Provides runtime plugin management: load, start, stop, reload, unload.
    /// </summary>
    public IPluginManager Plugins { get; }

    internal WinNuxServiceHost(IHost host, WinNuxServiceInfo info)
    {
        _host = host;
        Info = info;
        Plugins = host.Services.GetService(typeof(IPluginManager)) as IPluginManager
            ?? throw new InvalidOperationException("IPluginManager is not registered in the service container.");
    }

    public WinNuxServiceHost ConfigureServices(Action<IServiceProvider> configure)
    {
        configure(_host.Services);
        return this;
    }

    public Task RunAsync() => _host.RunAsync();

    public Task StartAsync() => _host.StartAsync();

    public Task StopAsync() => _host.StopAsync();
}