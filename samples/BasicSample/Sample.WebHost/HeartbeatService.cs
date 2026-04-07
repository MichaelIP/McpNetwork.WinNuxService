using McpNetwork.WinNuxService;
using McpNetwork.WinNuxService.Models;
using Microsoft.Extensions.Logging;

namespace Sample.WebHost;

public class HeartbeatService : WinNuxServiceBase
{
    private readonly WinNuxServiceInfo info;
    private readonly ILogger<HeartbeatService> logger;

    public HeartbeatService(WinNuxServiceInfo serviceInfo, ILogger<HeartbeatService> logger)
    {
        this.logger = logger;
        info = serviceInfo;
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            logger.LogInformation(">>>>>> Heartbeat from {Name} at {Time}", info.ServiceName, DateTime.Now);
            await Task.Delay(TimeSpan.FromSeconds(5), token);
        }
    }


}