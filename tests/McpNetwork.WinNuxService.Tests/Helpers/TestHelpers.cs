using McpNetwork.WinNuxService.Interfaces;

namespace McpNetwork.WinNuxService.Tests.Helpers;

// ===========================================================================
// Greeter abstractions
// ===========================================================================

public interface IGreeter { string Greet(string name); }

public class HelloGreeter : IGreeter
{
    public string Greet(string name) => $"Hello, {name}!";
}

/// <summary>
/// Service that greets on start and exposes the last greeting for assertions.
/// </summary>
public class GreetingService : IWinNuxService
{
    private readonly IGreeter _greeter;
    public string LastGreeting { get; private set; } = string.Empty;

    public GreetingService(IGreeter greeter) { _greeter = greeter; }

    public string Greet(string name) => _greeter.Greet(name);

    public Task OnStartAsync(CancellationToken token)
    {
        LastGreeting = _greeter.Greet("World");
        return Task.CompletedTask;
    }

    public Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

// ===========================================================================
// Counter abstractions
// ===========================================================================

public interface ICounter { void Increment(); int Value { get; } }

public class InMemoryCounter : ICounter
{
    public int Value { get; private set; }
    public void Increment() => Value++;
}

// ===========================================================================
// Services used by DI and lifecycle tests
// ===========================================================================

/// <summary>Service that increments a counter on start.</summary>
public class CountingService : IWinNuxService
{
    private readonly ICounter _counter;
    public CountingService(ICounter counter) => _counter = counter;

    public Task OnStartAsync(CancellationToken token)
    {
        _counter.Increment();
        return Task.CompletedTask;
    }

    public Task OnStopAsync(CancellationToken token) => Task.CompletedTask;
}

/// <summary>First of two services sharing a singleton counter.</summary>
public class FirstConsumer : IWinNuxService
{
    private readonly ICounter _counter;
    public FirstConsumer(ICounter counter) => _counter = counter;

    public Task OnStartAsync(CancellationToken token)
    {
        _counter.Increment();
        return Task.CompletedTask;
    }

    public Task OnStopAsync(CancellationToken token) => Task.CompletedTask;
}

/// <summary>Second of two services sharing a singleton counter.</summary>
public class SecondConsumer : IWinNuxService
{
    private readonly ICounter _counter;
    public SecondConsumer(ICounter counter) => _counter = counter;

    public Task OnStartAsync(CancellationToken token)
    {
        _counter.Increment();
        return Task.CompletedTask;
    }

    public Task OnStopAsync(CancellationToken token) => Task.CompletedTask;
}

/// <summary>Service with an unregistered dependency — used to test DI failure handling.</summary>
public class MissingDepService : IWinNuxService
{
    public MissingDepService(IGreeter greeter) { }
    public Task OnStartAsync(CancellationToken token) => Task.CompletedTask;
    public Task OnStopAsync(CancellationToken token) => Task.CompletedTask;
}