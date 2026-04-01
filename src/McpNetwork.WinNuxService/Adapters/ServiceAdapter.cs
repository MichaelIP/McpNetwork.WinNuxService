using McpNetwork.WinNuxService.Interfaces;
using Microsoft.Extensions.Hosting;

namespace McpNetwork.WinNuxService.Adapters;

public class ServiceAdapter<TService> : BackgroundService
    where TService : class, IWinNuxService
{
    private readonly TService _service;

    private Task? _startTask;
    private readonly CancellationTokenSource _internalCts = new();

    public ServiceAdapter(TService service)
    {
        _service = service;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _internalCts.Token);

        var token = linkedCts.Token;

        _startTask = _service.OnStartAsync(token);

        await _startTask;

        try
        {
            await Task.Delay(Timeout.Infinite, token);
        }
        catch (TaskCanceledException)
        {
            // Expected when the service is stopping
        }
        catch (OperationCanceledException)
        {
            // Expected when the service is stopping
        }

    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _internalCts.Cancel();

        if (_startTask != null)
        {
            await _startTask;
        }

        await _service.OnStopAsync(cancellationToken);

        await base.StopAsync(cancellationToken);
    }
}
