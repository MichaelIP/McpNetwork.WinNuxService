using McpNetwork.WinNuxService.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace McpNetwork.WinNuxService.Tests;

/// <summary>
/// Tests verifying that the standard .NET DI container is wired correctly
/// and that services can resolve dependencies through constructor injection.
/// </summary>
[TestFixture]
public class DependencyInjectionTests
{
    // -------------------------------------------------------------------------
    // Contracts / fakes
    // -------------------------------------------------------------------------

    public interface IGreeter { string Greet(string name); }

    public class HelloGreeter : IGreeter
    {
        public string Greet(string name) => $"Hello, {name}!";
    }

    public interface ICounter { void Increment(); int Value { get; } }

    public class InMemoryCounter : ICounter
    {
        public int Value { get; private set; }
        public void Increment() => Value++;
    }

    // -------------------------------------------------------------------------
    // Services that rely on injected dependencies
    // -------------------------------------------------------------------------

    private class GreetingService : IWinNuxService
    {
        private readonly IGreeter _greeter;
        public string LastGreeting { get; private set; } = string.Empty;

        public GreetingService(IGreeter greeter) => _greeter = greeter;

        public Task OnStartAsync(CancellationToken token)
        {
            LastGreeting = _greeter.Greet("World");
            return Task.CompletedTask;
        }

        public Task OnStopAsync(CancellationToken token) => Task.CompletedTask;
    }

    private class CountingService : IWinNuxService
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

    /// <summary>
    /// Two services that share the same singleton dependency.
    /// </summary>
    private class FirstConsumer : IWinNuxService
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

    private class SecondConsumer : IWinNuxService
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

    /// <summary>Service that requires a dependency which is not registered.</summary>
    private class MissingDepService : IWinNuxService
    {
        // Constructor intentionally requires an unregistered type
        public MissingDepService(IGreeter greeter) { }

        public Task OnStartAsync(CancellationToken token) => Task.CompletedTask;
        public Task OnStopAsync(CancellationToken token) => Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [Test]
    public async Task RegisteredSingleton_IsInjectedIntoService()
    {
        var svc = new GreetingService(new HelloGreeter());   // direct wiring for assertion

        var host = WinNuxService
            .Create()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton<IGreeter, HelloGreeter>();
            })
            .AddService<GreetingService>()
            .Build();

        await host.StartAsync();

        // Resolve and verify via the DI container
        var greeter = host.Services.GetRequiredService<IGreeter>();
        Assert.That(greeter, Is.InstanceOf<HelloGreeter>(),
            "The DI container should return HelloGreeter for IGreeter");

        await host.StopAsync();
    }

    [Test]
    public async Task Service_ReceivesDependency_ViaConstructorInjection()
    {
        GreetingService? resolvedService = null;

        var host = WinNuxService
            .Create()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton<IGreeter, HelloGreeter>();
                services.AddSingleton<GreetingService>();
            })
            .AddService<GreetingService>()
            .Build();

        await host.StartAsync();

        resolvedService = host.Services.GetRequiredService<GreetingService>();

        Assert.That(resolvedService.LastGreeting, Is.EqualTo("Hello, World!"),
            "IGreeter should have been injected and called during OnStartAsync");

        await host.StopAsync();
    }

    [Test]
    public async Task Singleton_IsSharedAcrossMultipleServices()
    {
        var sharedCounter = new InMemoryCounter();

        var host = WinNuxService
            .Create()
            .ConfigureServices((_, services) =>
            {
                // Register the pre-created instance so both consumers share it
                services.AddSingleton<ICounter>(sharedCounter);
            })
            .AddService<FirstConsumer>()
            .AddService<SecondConsumer>()
            .Build();

        await host.StartAsync();

        // Each consumer calls Increment once — both must see the same instance
        Assert.That(sharedCounter.Value, Is.EqualTo(2),
            "Both services should have incremented the same singleton counter");

        await host.StopAsync();
    }

    [Test]
    public async Task Transient_ProvidesNewInstancePerResolution()
    {
        var host = WinNuxService
            .Create()
            .ConfigureServices((_, services) =>
            {
                services.AddTransient<ICounter, InMemoryCounter>();
            })
            .Build();

        await host.StartAsync();

        var a = host.Services.GetRequiredService<ICounter>();
        var b = host.Services.GetRequiredService<ICounter>();

        Assert.That(a, Is.Not.SameAs(b),
            "Transient registration should yield a new instance on every resolution");

        await host.StopAsync();
    }

    [Test]
    public async Task Scoped_ProvidesConsistentInstanceWithinScope()
    {
        var host = WinNuxService
            .Create()
            .ConfigureServices((_, services) =>
            {
                services.AddScoped<ICounter, InMemoryCounter>();
            })
            .Build();

        await host.StartAsync();

        using var scope = host.Services.CreateScope();
        var a = scope.ServiceProvider.GetRequiredService<ICounter>();
        var b = scope.ServiceProvider.GetRequiredService<ICounter>();

        Assert.That(a, Is.SameAs(b),
            "Scoped registration should return the same instance within one scope");

        await host.StopAsync();
    }

    [Test]
    public void UnregisteredDependency_ThrowsAtBuildOrStart()
    {
        // The host should throw either at Build() or StartAsync() —
        // not silently inject null.
        var host = WinNuxService
            .Create()
            .AddService<MissingDepService>()  // IGreeter is NOT registered
            .Build();

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await host.StartAsync(),
            "Missing dependency should produce a clear exception, not a null reference");
    }

    [Test]
    public async Task ConfigureServices_CanRegisterMultipleDependencies()
    {
        var host = WinNuxService
            .Create()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton<IGreeter, HelloGreeter>();
                services.AddSingleton<ICounter, InMemoryCounter>();
            })
            .Build();

        await host.StartAsync();

        Assert.Multiple(() =>
        {
            Assert.That(host.Services.GetService<IGreeter>(), Is.Not.Null);
            Assert.That(host.Services.GetService<ICounter>(), Is.Not.Null);
        });

        await host.StopAsync();
    }

    [Test]
    public async Task ConfigureServices_CallbackReceivesHostBuilderContext()
    {
        bool contextReceived = false;

        var host = WinNuxService
            .Create()
            .WithName("DITest")
            .ConfigureServices((ctx, services) =>
            {
                // ctx should expose environment, configuration, etc.
                contextReceived = ctx is not null;
                services.AddSingleton<IGreeter, HelloGreeter>();
            })
            .Build();

        await host.StartAsync();

        Assert.That(contextReceived, Is.True,
            "The ConfigureServices callback should receive a non-null context");

        await host.StopAsync();
    }
}