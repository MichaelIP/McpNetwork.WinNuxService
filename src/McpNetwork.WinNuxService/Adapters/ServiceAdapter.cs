using McpNetwork.WinNuxService.Interfaces;

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
        // 1. Signal ExecuteAsync to exit
        _internalCts.Cancel();

        // 2. Wait for ExecuteAsync to fully complete — BEFORE stopping the service
        await base.StopAsync(cancellationToken);

        // 3. Stop the service with a fresh, uncancelled token — immune to the host deadline
        await _service.OnStopAsync(CancellationToken.None);

        // 4. Cleanup
        _internalCts.Dispose();
    }
}