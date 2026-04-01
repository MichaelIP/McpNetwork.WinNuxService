using McpNetwork.SystemMetrics;
using McpNetwork.WinNuxService.Interfaces;
using McpNetwork.WinNuxService.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Sample.Plugin.External;

/// <summary>
///     This is a simple service that will be registered by the plugin. 
///     It implements IWinNuxService, which allows it to be managed by the host application (start/stop).
/// </summary>
/// <remarks>
///     This service will log system metrics every 2 seconds, demonstrating that it can run independently of the host application's services.
///     System metrics are gathered using the McpNetwork.SystemMetrics library version 5.0.1 whereas the main application uses version 7.0.0 of this Nuget package. 
///     This demonstrates that the plugin can use a different version of a library than the host application without conflicts, thanks to the isolation provided by the plugin load context.
/// </remarks>
public class ExternalPluginService : IWinNuxService
{

    private Task? loop;
    private readonly WinNuxServiceInfo info;
    private readonly Guid _guid = Guid.NewGuid();
    private readonly ILogger<ExternalPluginService> logger;


    /// <summary>
    ///     Constructor that takes in the service information and a logger. 
    ///     The service information is provided by the host application when the plugin is loaded, and 
    ///     the logger is injected by the dependency injection container.
    /// </summary>
    /// <param name="serviceInfo"></param>
    /// <param name="logger"></param>
    public ExternalPluginService(WinNuxServiceInfo serviceInfo, ILogger<ExternalPluginService> logger)
    {
        this.info = serviceInfo;
        this.logger = logger;
    }

    /// <summary>
    ///     Starts the service by launching a background task that runs the RunLoop method. 
    ///     The cancellation token is passed to allow for graceful shutdown when requested.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task OnStartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation($"ExternalPluginService started [{_guid}]");
        loop = RunLoop(cancellationToken);
        return Task.CompletedTask;

    }

    /// <summary>
    ///     Stops the service by signaling cancellation to the background task and awaiting its completion.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task OnStopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation($"ExternalPluginService stopping  [{_guid}]");
        if (loop != null)
            await loop;
    }

    /// <summary>
    ///     Loops until cancellation is requested, logging system metrics every 2 seconds.
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task RunLoop(CancellationToken token)
    {

        try
        {
            while (!token.IsCancellationRequested)
            {
                using (var systemMetrics = new SystemMetrics())
                {
                    var result = systemMetrics.GetMetrics();
                    var output = new StringBuilder();
                    output.Append($"Application name: [{info.ServiceName}] - ");
                    output.Append($"Process: {systemMetrics.ProcessId} - ");
                    output.Append($"Thread : {Thread.CurrentThread.ManagedThreadId} - ");
                    output.Append($"Process CPU usage: {result.ProcessCpuUsage:##0}% - ");
                    output.Append($"Total CPU usage: {result.TotalCpuUsage:##0}% - ");
                    output.Append("Called from ExternalPluginService");
                    logger.LogInformation($">>>>>> {output}");
                }

                await Task.Delay(TimeSpan.FromSeconds(2), token);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown — suppress
            Console.WriteLine("ExternalPluginService canceled gracefully.");
        }
        catch (Exception ex)
        {
            logger.LogError($"Unexpected exception in RunLoop: {ex}");
        }
    }

}
