using McpNetwork.WinNuxService.Interfaces;
using Microsoft.Extensions.Hosting;

namespace McpNetwork.WinNuxService.Adapters;

public class ServiceAdapter<TService> : BackgroundService
    where TService : class, IWinNuxService
{
    private readonly TService _service;
    private readonly CancellationTokenSource _internalCts = new();

    public ServiceAdapter(TService service)
    {
        _service = service;
    }

    // Called by the host during StartAsync — awaited before StartAsync returns.
    // This guarantees OnStartAsync is fully complete before the host is considered started.
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await _service.OnStartAsync(cancellationToken);

        // Chain into BackgroundService.StartAsync which schedules ExecuteAsync
        await base.StartAsync(cancellationToken);
    }

    // ExecuteAsync now only keeps the adapter alive until cancellation —
    // the service's actual work is driven by the service itself (e.g. via
    // a background loop started inside OnStartAsync).
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _internalCts.Token);

        try
        {
            await Task.Delay(Timeout.Infinite, linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when the service is stopping
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _internalCts.Cancel();

        await _service.OnStopAsync(cancellationToken);

        await base.StopAsync(cancellationToken);
    }
}