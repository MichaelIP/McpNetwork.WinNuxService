using McpNetwork.WinNuxService.Models;
using McpNetwork.WinNuxService.Plugins;
using McpNetwork.WinNuxService.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;

namespace McpNetwork.WinNuxService.Tests.Plugins;

/// <summary>
/// Unit tests for PluginManager.CreateInstance — the method responsible for either
/// returning a plugin instance as-is (same-context) or wrapping it in a
/// PluginInstanceProxy (cross-context / not directly castable).
/// </summary>
[TestFixture]
public class PluginManagerCreateInstanceTests
{
    private CreateInstanceTestablePluginManager _manager = null!;
    private IServiceProvider _services = null!;

    [SetUp]
    public void SetUp()
    {
        _manager = new CreateInstanceTestablePluginManager();
        _services = new ServiceCollection().BuildServiceProvider();
    }

    [Test]
    public void CreateInstance_TypeDirectlyImplementsIWinNuxService_ReturnsItUnwrapped()
    {
        var result = _manager.InvokeCreateInstance(_services, typeof(DirectWinNuxService));

        Assert.That(result, Is.InstanceOf<DirectWinNuxService>(),
            "When the type is directly assignable to IWinNuxService, CreateInstance should " +
            "return it as-is rather than wrapping it in a proxy.");
    }

    [Test]
    public async Task CreateInstance_DuckTypedType_WrapsInProxy_AndProxyForwardsCalls()
    {
        var result = _manager.InvokeCreateInstance(_services, typeof(DuckTypedService));

        Assert.That(result, Is.Not.InstanceOf<DuckTypedService>(),
            "A type that isn't directly assignable to IWinNuxService must be wrapped, " +
            "not cast — casting it would throw InvalidCastException.");
        Assert.That(result, Is.InstanceOf<PluginInstanceProxy>());

        await result.OnStartAsync(CancellationToken.None);
        await result.OnStopAsync(CancellationToken.None);

        var underlying = (DuckTypedService)((PluginInstanceProxy)result).Instance;
        Assert.That(underlying.StartCallCount, Is.EqualTo(1));
        Assert.That(underlying.StopCallCount, Is.EqualTo(1));
    }
}

/// <summary>
/// End-to-end tests through the public PluginManager API (LoadPlugin, StartPlugin,
/// StopPlugin, ConfigurePlugin), using DuckTypingPluginManager so the loaded "plugin
/// type" is one of the duck-typed fixtures instead of a real plugin DLL. CreateInstance
/// itself is NOT mocked here — this exercises the real fix end-to-end, the same way a
/// genuine cross-AssemblyLoadContext plugin would flow through the library.
/// </summary>
[TestFixture]
public class PluginManagerProxyIntegrationTests
{
    private IServiceProvider _services = null!;
    private string _dllPath = null!;

    [SetUp]
    public void SetUp()
    {
        _services = new ServiceCollection().BuildServiceProvider();
        _dllPath = MockablePluginManager.CreateDummyDll();
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_dllPath))
            File.Delete(_dllPath);
    }

    [Test]
    public async Task LoadPlugin_StartPlugin_StopPlugin_RoundTrip_ThroughProxy()
    {
        var manager = new DuckTypingPluginManager(typeof(DuckTypedConfigurableService));
        var plugin = manager.LoadPlugin(_dllPath, _services);

        // Confirms LoadPlugin no longer throws "No public non-abstract type implementing
        // IWinNuxService found" for a type that only implements it cross-context.
        Assert.That(plugin.State, Is.EqualTo(PluginState.Loaded));

        await manager.StartPlugin(plugin);
        Assert.That(plugin.State, Is.EqualTo(PluginState.Running));

        await manager.StopPlugin(plugin);
        Assert.That(plugin.State, Is.EqualTo(PluginState.Stopped));

        var underlying = (DuckTypedConfigurableService)((PluginInstanceProxy)plugin.Instance!).Instance;
        Assert.That(underlying.StartCallCount, Is.EqualTo(1));
        Assert.That(underlying.StopCallCount, Is.EqualTo(1));
    }

    [Test]
    public void ConfigurePlugin_ForwardsThroughProxy_ToUnderlyingDuckTypedInstance()
    {
        var manager = new DuckTypingPluginManager(typeof(DuckTypedConfigurableService));
        var plugin = manager.LoadPlugin(_dllPath, _services);
        var config = Substitute.For<IConfiguration>();

        manager.ConfigurePlugin(plugin, "Sensor-A", config);

        var underlying = (DuckTypedConfigurableService)((PluginInstanceProxy)plugin.Instance!).Instance;
        Assert.That(underlying.ConfiguredInstanceName, Is.EqualTo("Sensor-A"));
        Assert.That(underlying.ConfiguredConfiguration, Is.SameAs(config));
    }

    [Test]
    public void ConfigurePlugin_OnDuckTypedServiceWithoutIConfigurablePlugin_DoesNotThrow()
    {
        var manager = new DuckTypingPluginManager(typeof(DuckTypedService));
        var plugin = manager.LoadPlugin(_dllPath, _services);

        Assert.DoesNotThrow(() =>
            manager.ConfigurePlugin(plugin, "Sensor-A", Substitute.For<IConfiguration>()));
    }
}