using McpNetwork.WinNuxService.Interfaces;

namespace McpNetwork.WinNuxService;

/// <summary>
/// Base class for WinNux services. Handles CancellationTokenSource lifecycle,
/// so implementors only need to override ExecuteAsync.
/// </summary>
public abstract class WinNuxServiceBase : IWinNuxService
{
    private CancellationTokenSource? _cts;
    private Task? _execution;

    /// <summary>
    /// Implement your service loop here. The token is cancelled when the host stops.
    /// You don't need to catch OperationCanceledException — the base class handles it.
    /// </summary>
    protected abstract Task ExecuteAsync(CancellationToken token);

    public Task OnStartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _execution = RunSafeAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task OnStopAsync(CancellationToken cancellationToken)
    {
        if (_cts is null) return;

        await _cts.CancelAsync();       // signal the loop to stop

        if (_execution is not null)
            await _execution;           // wait for clean exit

        _cts.Dispose();
        _cts = null;
    }

    private async Task RunSafeAsync(CancellationToken token)
    {
        try
        {
            await ExecuteAsync(token);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path — swallowed intentionally
        }
        catch (Exception ex)
        {
            OnUnhandledException(ex);
        }
    }

    /// <summary>
    /// Override to handle unexpected exceptions from ExecuteAsync.
    /// Default behavior: rethrow.
    /// </summary>
    protected virtual void OnUnhandledException(Exception ex) => throw ex;
}