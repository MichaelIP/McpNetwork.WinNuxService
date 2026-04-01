using System.Threading;
using System.Threading.Tasks;

namespace McpNetwork.WinNuxService.Interfaces;

public interface IWinNuxService
{
    Task OnStartAsync(CancellationToken cancellationToken);

    Task OnStopAsync(CancellationToken cancellationToken);
}