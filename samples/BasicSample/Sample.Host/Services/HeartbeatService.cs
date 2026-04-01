using McpNetwork.WinNuxService.Interfaces;
using McpNetwork.WinNuxService.Models;
using Microsoft.Extensions.Logging;

namespace Sample.Host.Services;

public class HeartbeatService : IWinNuxService
{
    private Task? loop;
    private readonly WinNuxServiceInfo info;
    private readonly ILogger<HeartbeatService> logger;

    public HeartbeatService(WinNuxServiceInfo serviceInfo, ILogger<HeartbeatService> logger)
    {
        this.logger = logger;
        info = serviceInfo;
    }


    public Task OnStartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(">>>>>> HeartbeatService started");
        loop = RunLoop(cancellationToken);
        return Task.CompletedTask;
    }

    public async Task OnStopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(">>>>>> HeartbeatService stopping");
        if (loop != null)
            await loop;
    }

    private async Task RunLoop(CancellationToken token)
    {

        try
        {
            while (!token.IsCancellationRequested)
            {
                logger?.LogInformation($">>>>>> Heartbeat from HeartbeatService at {DateTime.Now}");
                await Task.Delay(TimeSpan.FromSeconds(5), token);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation($">>>>>> HeartbeatService canceled gracefully.");
        }
        catch (Exception ex)
        {
            logger.LogError($">>>>>> Unexpected exception in RunLoop: {ex}");
        }
    }

}