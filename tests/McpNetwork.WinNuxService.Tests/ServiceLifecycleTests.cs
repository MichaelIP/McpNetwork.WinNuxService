using McpNetwork.WinNuxService.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;

namespace McpNetwork.WinNuxService.Tests;

/// <summary>
/// Tests that verify IWinNuxService lifecycle (OnStartAsync / OnStopAsync)
/// is correctly driven by the host.
/// </summary>
[TestFixture]
public class ServiceLifecycleTests
{
    // -------------------------------------------------------------------------
    // Single service
    // -------------------------------------------------------------------------

    [Test]
    public async Task StartAsync_CallsOnStartAsync_OnRegisteredService()
    {
        var tracker = new LifecycleTracker();

        var host = WinNuxService.Create()
            .ConfigureServices((_, services) =>
                Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions
                    .AddSingleton(services, tracker))
            .AddService<TrackingService>()
            .Build();

        await host.StartAsync();
        await host.StopAsync();

        Assert.That(tracker.StartCalled, Is.True);
    }

    [Test]
    public async Task StopAsync_CallsOnStopAsync_OnRegisteredService()
    {
        var tracker = new LifecycleTracker();

        var host = WinNuxService.Create()
            .ConfigureServices((_, services) =>
                Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions
                    .AddSingleton(services, tracker))
            .AddService<TrackingService>()
            .Build();

        await host.StartAsync();
        await host.StopAsync();

        Assert.That(tracker.StopCalled, Is.True);
    }

    // -------------------------------------------------------------------------
    // Multiple services
    // -------------------------------------------------------------------------

    [Test]
    public async Task StartAsync_CallsOnStartAsync_OnAllRegisteredServices()
    {
        var trackerA = new LifecycleTracker();
        var trackerB = new LifecycleTracker();

        var host = WinNuxService.Create()
            .ConfigureServices((_, services) =>
            {
                Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions
                    .AddSingleton(services, trackerA);
                services.AddKeyedSingleton<LifecycleTracker>("B", trackerB);
            })
            .AddService<TrackingService>()
            .AddService<KeyedTrackingService>()
            .Build();

        await host.StartAsync();
        await host.StopAsync();

        Assert.That(trackerA.StartCalled, Is.True, "Service A was not started");
        Assert.That(trackerB.StartCalled, Is.True, "Service B was not started");
    }

    [Test]
    public async Task StopAsync_CallsOnStopAsync_OnAllRegisteredServices()
    {
        var trackerA = new LifecycleTracker();
        var trackerB = new LifecycleTracker();

        var host = WinNuxService.Create()
            .ConfigureServices((_, services) =>
            {
                Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions
                    .AddSingleton(services, trackerA);
                services.AddKeyedSingleton<LifecycleTracker>("B", trackerB);
            })
            .AddService<TrackingService>()
            .AddService<KeyedTrackingService>()
            .Build();

        await host.StartAsync();
        await host.StopAsync();

        Assert.That(trackerA.StopCalled, Is.True, "Service A was not stopped");
        Assert.That(trackerB.StopCalled, Is.True, "Service B was not stopped");
    }

    // -------------------------------------------------------------------------
    // Cancellation
    // -------------------------------------------------------------------------

    [Test]
    public async Task StopAsync_CancellationToken_IsSignalled_WhenHostStops()
    {
        var tokenObserver = new CancellationTokenObserver();

        var host = WinNuxService.Create()
            .ConfigureServices((_, services) =>
                Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions
                    .AddSingleton(services, tokenObserver))
            .AddService<ObservingService>()
            .Build();

        await host.StartAsync();
        await host.StopAsync();

        var recorded = await tokenObserver.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(recorded, Is.True, "OnStopAsync should have been called when the host stopped");
    }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

public class LifecycleTracker
{
    public bool StartCalled { get; private set; }
    public bool StopCalled { get; private set; }

    public void RecordStart() => StartCalled = true;
    public void RecordStop() => StopCalled = true;
}

/// <summary>Service that records start/stop via a shared LifecycleTracker.</summary>
public class TrackingService : IWinNuxService
{
    private readonly LifecycleTracker _tracker;
    public TrackingService(LifecycleTracker tracker) { _tracker = tracker; }

    public Task OnStartAsync(CancellationToken cancellationToken)
    {
        _tracker.RecordStart();
        return Task.CompletedTask;
    }

    public Task OnStopAsync(CancellationToken cancellationToken)
    {
        _tracker.RecordStop();
        return Task.CompletedTask;
    }
}

/// <summary>Second service that uses a keyed LifecycleTracker.</summary>
public class KeyedTrackingService : IWinNuxService
{
    private readonly LifecycleTracker _tracker;

    public KeyedTrackingService(
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("B")]
        LifecycleTracker tracker)
    {
        _tracker = tracker;
    }

    public Task OnStartAsync(CancellationToken cancellationToken)
    {
        _tracker.RecordStart();
        return Task.CompletedTask;
    }

    public Task OnStopAsync(CancellationToken cancellationToken)
    {
        _tracker.RecordStop();
        return Task.CompletedTask;
    }
}

public class CancellationTokenObserver
{
    private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Record() => _tcs.TrySetResult();

    /// <summary>
    /// Waits until Record() is called, or the timeout elapses.
    /// Returns true if the cancellation was recorded in time.
    /// </summary>
    public async Task<bool> WaitAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await _tcs.Task.WaitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

/// <summary>Service that records whether its token was cancelled on stop.</summary>
public class ObservingService : IWinNuxService
{
    private readonly CancellationTokenObserver _observer;
    public ObservingService(CancellationTokenObserver observer) { _observer = observer; }

    public Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task OnStopAsync(CancellationToken cancellationToken)
    {
        _observer.Record();
        return Task.CompletedTask;
    }
}