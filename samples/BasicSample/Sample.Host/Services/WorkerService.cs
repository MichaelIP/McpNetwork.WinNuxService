using McpNetwork.SystemMetrics;
using McpNetwork.WinNuxService.Interfaces;
using McpNetwork.WinNuxService.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Sample.Host.Services;

/// <summary>
///     Implements a simple worker service that logs system metrics every 3 seconds.
/// </summary>
/// <remarks>
///     This service uses the McpNetwork.SystemMetrics library version 7.0.0 to gather CPU usage information. 
///     It demonstrates how a service can run in the background and perform tasks independently of the main application flow. 
///     The service will continue to log metrics until it receives a stop signal, at which point it will attempt to shut down gracefully by awaiting the completion of its background task.
/// </remarks>  
public class WorkerService : IWinNuxService
{
    private Task? loop;
    private readonly WinNuxServiceInfo info;
    private readonly ILogger<HeartbeatService> logger;

    public WorkerService(WinNuxServiceInfo serviceInfo, ILogger<HeartbeatService> logger)
    {
        this.logger = logger;
        info = serviceInfo;
    }

    public Task OnStartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(">>>>>> WorkerService started");
        loop = RunLoop(cancellationToken);
        return Task.CompletedTask;
    }

    public async Task OnStopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(">>>>>> WorkerService stopping");
        if (loop != null)
            await loop;
    }

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
                    output.Append("Called from WorkerService");
                    logger.LogInformation($">>>>>> {output}");
                }

                await Task.Delay(TimeSpan.FromSeconds(3), token);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation(">>>>>> WorkerService canceled gracefully.");
        }
        catch (Exception ex)
        {
            logger.LogError($">>>>>> WorkerService exception in RunLoop: {ex}");
        }
    }

}