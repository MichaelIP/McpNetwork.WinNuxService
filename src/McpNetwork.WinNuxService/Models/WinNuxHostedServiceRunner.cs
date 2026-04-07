using McpNetwork.WinNuxService.Interfaces;

namespace McpNetwork.WinNuxService.Models;

internal sealed class WinNuxHostedServiceRunner : IHostedService
{
    private readonly IEnumerable<IWinNuxService> _services;
    private readonly ILogger<WinNuxHostedServiceRunner> _logger;

    public WinNuxHostedServiceRunner(IEnumerable<IWinNuxService> services, ILogger<WinNuxHostedServiceRunner> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var svc in _services)
        {
            _logger.LogInformation("Starting {Service}", svc.GetType().Name);
            await svc.OnStartAsync(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var svc in _services.Reverse())
        {
            _logger.LogInformation("Stopping {Service}", svc.GetType().Name);
            await svc.OnStopAsync(cancellationToken);
        }
    }
}