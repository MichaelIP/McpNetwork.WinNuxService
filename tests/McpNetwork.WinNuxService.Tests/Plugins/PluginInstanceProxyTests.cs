using McpNetwork.WinNuxService.Interfaces;
using McpNetwork.WinNuxService.Plugins;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NUnit.Framework;

namespace McpNetwork.WinNuxService.Tests.Plugins;

/// <summary>
/// Tests for <see cref="PluginInstanceProxy"/> — the reflection-forwarding wrapper that
/// lets a plugin instance whose concrete type can't be cast to the host's IWinNuxService /
/// IConfigurablePlugin (because it was loaded into a separate AssemblyLoadContext and
/// carries its own copy of those interfaces) still be used as if it were one.
/// </summary>
[TestFixture]
public class PluginInstanceProxyTests
{
    // -------------------------------------------------------------------------
    // Construction
    // -------------------------------------------------------------------------

    [Test]
    public void Constructor_TypeMissingOnStartAsync_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new PluginInstanceProxy(new object(), typeof(object)));

        Assert.That(ex!.Message, Does.Contain("OnStartAsync"));
    }

    [Test]
    public void Constructor_TypeMissingOnStopAsync_ThrowsInvalidOperationException()
    {
        var instance = new IncompleteDuckTypedService();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new PluginInstanceProxy(instance, typeof(IncompleteDuckTypedService)));

        Assert.That(ex!.Message, Does.Contain("OnStopAsync"));
    }

    // -------------------------------------------------------------------------
    // OnStartAsync / OnStopAsync forwarding
    // -------------------------------------------------------------------------

    [Test]
    public async Task OnStartAsync_ForwardsToUnderlyingInstance_WithSameToken()
    {
        var instance = new DuckTypedService();
        var proxy = new PluginInstanceProxy(instance, typeof(DuckTypedService));
        using var cts = new CancellationTokenSource();

        await proxy.OnStartAsync(cts.Token);

        Assert.That(instance.StartCallCount, Is.EqualTo(1));
        Assert.That(instance.LastStartToken, Is.EqualTo(cts.Token));
    }

    [Test]
    public async Task OnStopAsync_ForwardsToUnderlyingInstance_WithSameToken()
    {
        var instance = new DuckTypedService();
        var proxy = new PluginInstanceProxy(instance, typeof(DuckTypedService));
        using var cts = new CancellationTokenSource();

        await proxy.OnStopAsync(cts.Token);

        Assert.That(instance.StopCallCount, Is.EqualTo(1));
        Assert.That(instance.LastStopToken, Is.EqualTo(cts.Token));
    }

    [Test]
    public void Proxy_IsAssignableTo_HostIWinNuxService()
    {
        var instance = new DuckTypedService();
        var proxy = new PluginInstanceProxy(instance, typeof(DuckTypedService));

        Assert.That(proxy, Is.InstanceOf<IWinNuxService>());
    }

    // -------------------------------------------------------------------------
    // IConfigurablePlugin fallback
    // -------------------------------------------------------------------------

    [Test]
    public void SupportsConfiguration_FalseWhenUnderlyingTypeDoesNotImplementIConfigurablePlugin()
    {
        var instance = new DuckTypedService();
        var proxy = new PluginInstanceProxy(instance, typeof(DuckTypedService));

        Assert.That(proxy.SupportsConfiguration, Is.False);
    }

    [Test]
    public void SupportsConfiguration_TrueWhenUnderlyingTypeImplementsIConfigurablePlugin()
    {
        var instance = new DuckTypedConfigurableService();
        var proxy = new PluginInstanceProxy(instance, typeof(DuckTypedConfigurableService));

        Assert.That(proxy.SupportsConfiguration, Is.True);
    }

    [Test]
    public void Proxy_IsAssignableTo_HostIConfigurablePlugin_EvenWhenUnsupported()
    {
        // The proxy itself always implements IConfigurablePlugin, regardless of whether the
        // underlying plugin does. This is what keeps the documented README pattern —
        // `if (loaded.Instance is IConfigurablePlugin configurable) configurable.Configure(...)`
        // — working for every plugin; Configure() is just a safe no-op when unsupported.
        var instance = new DuckTypedService();
        var proxy = new PluginInstanceProxy(instance, typeof(DuckTypedService));

        Assert.That(proxy, Is.InstanceOf<IConfigurablePlugin>());
    }

    [Test]
    public void Configure_WhenSupported_ForwardsInstanceNameAndConfiguration()
    {
        var instance = new DuckTypedConfigurableService();
        var proxy = new PluginInstanceProxy(instance, typeof(DuckTypedConfigurableService));
        var config = Substitute.For<IConfiguration>();

        proxy.Configure("Sensor-A", config);

        Assert.That(instance.ConfiguredInstanceName, Is.EqualTo("Sensor-A"));
        Assert.That(instance.ConfiguredConfiguration, Is.SameAs(config));
    }

    [Test]
    public void Configure_WhenNotSupported_IsASafeNoOp()
    {
        var instance = new DuckTypedService();
        var proxy = new PluginInstanceProxy(instance, typeof(DuckTypedService));
        var config = Substitute.For<IConfiguration>();

        Assert.DoesNotThrow(() => proxy.Configure("Sensor-A", config));
    }
}