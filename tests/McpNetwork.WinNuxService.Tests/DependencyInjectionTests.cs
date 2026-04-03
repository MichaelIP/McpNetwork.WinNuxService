using McpNetwork.WinNuxService.Tests.Helpers;
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
    [Test]
    public async Task RegisteredSingleton_IsInjectedIntoService()
    {
        var host = WinNuxService
            .Create()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton<IGreeter, HelloGreeter>();
            })
            .AddService<GreetingService>()
            .Build();

        await host.StartAsync();

        var greeter = host.Services.GetRequiredService<IGreeter>();
        Assert.That(greeter, Is.InstanceOf<HelloGreeter>(),
            "The DI container should return HelloGreeter for IGreeter");

        await host.StopAsync();
    }

    [Test]
    public async Task Service_ReceivesDependency_ViaConstructorInjection()
    {
        var host = WinNuxService
            .Create()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton<IGreeter, HelloGreeter>();
            })
            .AddService<GreetingService>()
            .Build();

        await host.StartAsync();

        // AddService registers the singleton — resolve that same instance
        var resolvedService = host.Services.GetRequiredService<GreetingService>();

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
                services.AddSingleton<ICounter>(sharedCounter);
            })
            .AddService<FirstConsumer>()
            .AddService<SecondConsumer>()
            .Build();

        await host.StartAsync();

        Assert.That(sharedCounter.Value, Is.EqualTo(2), "Both services should have incremented the same singleton counter");

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