using McpNetwork.WinNuxService.Interfaces;
using NUnit.Framework;

namespace McpNetwork.WinNuxService.Tests;

/// <summary>
/// Tests for service start/stop lifecycle behaviour.
/// </summary>
[TestFixture]
public class ServiceLifecycleTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// A minimal well-behaved service that records lifecycle calls.
    /// </summary>
    private class RecordingService : IWinNuxService
    {
        public bool StartCalled { get; private set; }
        public bool StopCalled { get; private set; }
        public CancellationToken ReceivedStartToken { get; private set; }

        public Task OnStartAsync(CancellationToken token)
        {
            StartCalled = true;
            ReceivedStartToken = token;
            return Task.CompletedTask;
        }

        public Task OnStopAsync(CancellationToken token)
        {
            StopCalled = true;
            return Task.CompletedTask;
        }
    }

    /// <summary>A service that throws during start.</summary>
    private class FaultyStartService : IWinNuxService
    {
        public Task OnStartAsync(CancellationToken token) =>
            throw new InvalidOperationException("Simulated start failure");

        public Task OnStopAsync(CancellationToken token) => Task.CompletedTask;
    }

    /// <summary>A service that throws during stop.</summary>
    private class FaultyStopService : IWinNuxService
    {
        public Task OnStartAsync(CancellationToken token) => Task.CompletedTask;

        public Task OnStopAsync(CancellationToken token) =>
            throw new InvalidOperationException("Simulated stop failure");
    }

    /// <summary>A service that respects cancellation in its run-loop.</summary>
    private class CancellationAwareService : IWinNuxService
    {
        public bool LoopExitedCleanly { get; private set; }

        public Task OnStartAsync(CancellationToken token)
        {
            _ = RunLoop(token);
            return Task.CompletedTask;
        }

        public Task OnStopAsync(CancellationToken token) => Task.CompletedTask;

        private async Task RunLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                    await Task.Delay(50, token);
            }
            catch (OperationCanceledException) { /* expected */ }

            LoopExitedCleanly = true;
        }
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [Test]
    public async Task OnStartAsync_IsCalledWhenHostStarts()
    {
        //var svc = new RecordingService();
        var host = WinNuxService.Create().AddService< RecordingService>().Build();

        await host.StartAsync();

        Assert.That(svc.StartCalled, Is.True,
            "OnStartAsync should have been called after host start");

        await host.StopAsync();
    }

    [Test]
    public async Task OnStopAsync_IsCalledWhenHostStops()
    {
        var svc = new RecordingService();
        var host = WinNuxService.Create().AddService<RecordingService>().Build();

        await host.StartAsync();
        await host.StopAsync();

        Assert.That(svc.StopCalled, Is.True,
            "OnStopAsync should have been called after host stop");
    }

    [Test]
    public async Task StartThenStop_BothLifecycleMethodsAreCalled()
    {
        var svc = new RecordingService();
        var host = WinNuxService.Create().AddService(svc).Build();

        await host.StartAsync();
        await host.StopAsync();

        Assert.Multiple(() =>
        {
            Assert.That(svc.StartCalled, Is.True);
            Assert.That(svc.StopCalled, Is.True);
        });
    }

    [Test]
    public async Task OnStartAsync_ReceivesNonCancelledToken()
    {
        var svc = new RecordingService();
        var host = WinNuxService.Create().AddService(svc).Build();

        await host.StartAsync();

        Assert.That(svc.ReceivedStartToken.IsCancellationRequested, Is.False,
            "The token passed to OnStartAsync should not be cancelled at start time");

        await host.StopAsync();
    }

    [Test]
    public void OnStartAsync_WhenServiceThrows_ExceptionPropagates()
    {
        var host = WinNuxService.Create().AddService<FaultyStartService>().Build();

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await host.StartAsync(),
            "An exception thrown in OnStartAsync should propagate to the caller");
    }

    [Test]
    public async Task OnStopAsync_WhenServiceThrows_ExceptionPropagates()
    {
        var host = WinNuxService.Create().AddService<FaultyStopService>().Build();
        await host.StartAsync();

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await host.StopAsync(),
            "An exception thrown in OnStopAsync should propagate to the caller");
    }

    [Test]
    public async Task CancellationToken_IsCancelledAfterHostStop()
    {
        var svc = new CancellationAwareService();
        var host = WinNuxService.Create().AddService(svc).Build();

        await host.StartAsync();
        await Task.Delay(100);   // let the loop spin up
        await host.StopAsync();
        await Task.Delay(100);   // let the loop observe cancellation

        Assert.That(svc.LoopExitedCleanly, Is.True,
            "The run-loop should exit cleanly once the host cancels the token on stop");
    }

    [Test]
    public async Task MultipleStartStop_CyclesCompleteWithoutError()
    {
        var host = WinNuxService.Create().AddService<RecordingService>().Build();

        for (int i = 0; i < 2; i++)
        {
            Assert.DoesNotThrowAsync(async () =>
            {
                await host.StartAsync();
                await host.StopAsync();
            }, $"Start/stop cycle {i + 1} should complete without throwing");
        }
    }
}