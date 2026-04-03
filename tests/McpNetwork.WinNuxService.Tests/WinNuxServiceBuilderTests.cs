using McpNetwork.WinNuxService.Interfaces;
using McpNetwork.WinNuxService.Models;
using McpNetwork.WinNuxService.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace McpNetwork.WinNuxService.Tests;

[TestFixture]
public class WinNuxServiceBuilderTests
{
    // -------------------------------------------------------------------------
    // Service metadata
    // -------------------------------------------------------------------------

    [Test]
    public void Build_WithName_SetsServiceName()
    {
        var host = WinNuxService.Create()
            .WithName("MyService")
            .Build();

        Assert.That(host.Info.ServiceName, Is.EqualTo("MyService"));
    }

    [Test]
    public void Build_WithVersion_SetsVersion()
    {
        var host = WinNuxService.Create()
            .WithVersion("3.2.1")
            .Build();

        Assert.That(host.Info.Version, Is.EqualTo("3.2.1"));
    }

    [Test]
    public void Build_WithEnvironment_SetsEnvironment()
    {
        var host = WinNuxService.Create()
            .WithEnvironment("Staging")
            .Build();

        Assert.That(host.Info.Environment, Is.EqualTo("Staging"));
    }

    [Test]
    public void Build_AddProperty_StoresCustomProperty()
    {
        var host = WinNuxService.Create()
            .AddProperty("GitCommit", "abc123")
            .AddProperty("Region", "eu-west-1")
            .Build();

        Assert.That(host.Info.Properties["GitCommit"], Is.EqualTo("abc123"));
        Assert.That(host.Info.Properties["Region"], Is.EqualTo("eu-west-1"));
    }

    [Test]
    public void Build_WithoutName_UsesDefaultServiceName()
    {
        var host = WinNuxService.Create().Build();

        Assert.That(host.Info.ServiceName, Is.Not.Null.And.Not.Empty);
    }

    // -------------------------------------------------------------------------
    // host.Services
    // -------------------------------------------------------------------------

    [Test]
    public void Services_IsNotNull_AfterBuild()
    {
        var host = WinNuxService.Create().Build();

        Assert.That(host.Services, Is.Not.Null);
    }

    [Test]
    public void Services_ResolvesRegisteredSingleton()
    {
        var host = WinNuxService.Create()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton<IGreeter, HelloGreeter>();
            })
            .Build();

        var greeter = host.Services.GetRequiredService<IGreeter>();

        Assert.That(greeter, Is.InstanceOf<HelloGreeter>());
    }

    [Test]
    public void Services_ResolvesWinNuxServiceInfo()
    {
        var host = WinNuxService.Create()
            .WithName("InfoTest")
            .Build();

        var info = host.Services.GetRequiredService<WinNuxServiceInfo>();

        Assert.That(info.ServiceName, Is.EqualTo("InfoTest"));
    }

    [Test]
    public void Services_ResolvesIPluginManager()
    {
        var host = WinNuxService.Create().Build();

        var manager = host.Services.GetRequiredService<IPluginManager>();

        Assert.That(manager, Is.Not.Null);
    }

    [Test]
    public void Services_ConstructorInjectionWorks_BetweenRegisteredTypes()
    {
        var host = WinNuxService.Create()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton<IGreeter, HelloGreeter>();
                services.AddSingleton<GreetingService>();
            })
            .AddService<GreetingService>()
            .Build();

        var service = host.Services.GetRequiredService<GreetingService>();

        Assert.That(service, Is.Not.Null);
        Assert.That(service.Greet("World"), Is.EqualTo("Hello, World!"));
    }

    // -------------------------------------------------------------------------
    // ConfigureServices (post-build)
    // -------------------------------------------------------------------------

    [Test]
    public void ConfigureServices_PostBuild_IsInvokedWithServiceProvider()
    {
        IServiceProvider? captured = null;

        var host = WinNuxService.Create()
            .Build()
            .ConfigureServices(sp => captured = sp);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured, Is.SameAs(host.Services));
    }

    [Test]
    public void ConfigureServices_PostBuild_IsChainable()
    {
        var calls = 0;

        WinNuxService.Create()
            .Build()
            .ConfigureServices(_ => calls++)
            .ConfigureServices(_ => calls++);

        Assert.That(calls, Is.EqualTo(2));
    }

    // -------------------------------------------------------------------------
    // AddService
    // -------------------------------------------------------------------------

    [Test]
    public void AddService_RegisteredServiceIsResolvable()
    {
        var host = WinNuxService.Create()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton<IGreeter, HelloGreeter>();
            })
            .AddService<GreetingService>()
            .Build();

        var service = host.Services.GetRequiredService<GreetingService>();

        Assert.That(service, Is.Not.Null);
    }

    // -------------------------------------------------------------------------
    // host.Plugins
    // -------------------------------------------------------------------------

    [Test]
    public void Plugins_IsNotNull_AfterBuild()
    {
        var host = WinNuxService.Create().Build();

        Assert.That(host.Plugins, Is.Not.Null);
    }

    [Test]
    public void Plugins_IsSameInstance_AsResolvedFromDI()
    {
        var host = WinNuxService.Create().Build();

        var fromDI = host.Services.GetRequiredService<IPluginManager>();

        Assert.That(host.Plugins, Is.SameAs(fromDI));
    }
}

// Shared test helpers live in Helpers/TestHelpers.cs