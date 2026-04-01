using McpNetwork.WinNuxService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sample.Host.Interfaces;
using Sample.Host.plugins;
using Sample.Host.Services;
using System.Diagnostics;
using System.Runtime.Versioning;

class Program
{
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    static async Task Main(string[] args)
    {

        if (Debugger.IsAttached)
        {
            Console.WriteLine("Running in console mode");
        }

        var builder = WinNuxService
            .Create()

            .WithName("WinNuxService-Demo")
            .WithEnvironment("Demo")
            .WithVersion("1.0.0-beta.1")

            .AddProperty("Author", "McpNetwork")
            .AddProperty(string.Format("StartedAt-{0}", DateTimeOffset.Now), DateTimeOffset.Now.ToString("o"))

            .ConfigureLogging(logging =>
            {
                logging.AddConsole();
                logging.AddDebug();
            })

            .AddService<HeartbeatService>()
            .AddService<WorkerService>()

            .ConfigureServices((ctx, services) =>
            {
                services.AddSingleton<IDependency, InternalDependency>();
            })

            .LoadPlugin<InternalPlugin>()

            .LoadExternalPlugin("plugins/Sample.Plugin.External/Sample.Plugin.External.dll")
            
            .Build();

        await builder.RunAsync();
    }
}