using Microsoft.Extensions.Hosting;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace McpNetwork.WinNuxService.Models;

public class WinNuxServiceHost
{
    private readonly IHost host;
    public WinNuxServiceInfo Info { get; }

    internal WinNuxServiceHost(IHost host, WinNuxServiceInfo info)
    {
        this.host = host;
        Info = info;
    }

    public WinNuxServiceHost Configure(Action<IServiceProvider> configure)
    {
        configure(host.Services);
        return this;
    }

    public Task RunAsync() => host.RunAsync();

    public Task StartAsync() => host.StartAsync();

    public Task StopAsync() => host.StopAsync();

}