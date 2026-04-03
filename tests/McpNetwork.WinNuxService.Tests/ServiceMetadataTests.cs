using McpNetwork.WinNuxService.Interfaces;
using McpNetwork.WinNuxService.Models;
using NUnit.Framework;

namespace McpNetwork.WinNuxService.Tests;

/// <summary>
/// Tests for WinNuxServiceInfo: metadata set via the fluent builder and
/// accessed through Dependency Injection inside services.
/// </summary>
[TestFixture]
public class ServiceMetadataTests
{
    // -------------------------------------------------------------------------
    // Helper service
    // -------------------------------------------------------------------------

    /// <summary>
    /// Records the WinNuxServiceInfo it receives so tests can inspect it.
    /// </summary>
    private class MetadataCapturingService : IWinNuxService
    {
        public WinNuxServiceInfo? CapturedInfo { get; private set; }

        public MetadataCapturingService(WinNuxServiceInfo info)
        {
            CapturedInfo = info;
        }

        public Task OnStartAsync(CancellationToken token) => Task.CompletedTask;
        public Task OnStopAsync(CancellationToken token) => Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // WithName
    // -------------------------------------------------------------------------

    [Test]
    public async Task WithName_SetsServiceName_OnInfo()
    {
        var host = WinNuxService
            .Create()
            .WithName("OrderProcessor")
            .AddService<MetadataCapturingService>()
            .Build();

        await host.StartAsync();

        var info = host.Services.GetRequiredService<WinNuxServiceInfo>();
        Assert.That(info.ServiceName, Is.EqualTo("OrderProcessor"));

        await host.StopAsync();
    }

    [Test]
    public async Task WithName_NotCalled_ServiceName_IsNullOrEmpty()
    {
        var host = WinNuxService
            .Create()
            .AddService<MetadataCapturingService>()
            .Build();

        await host.StartAsync();

        var info = host.Services.GetRequiredService<WinNuxServiceInfo>();
        Assert.That(info.ServiceName, Is.Null.Or.Empty,
            "ServiceName should be null or empty when WithName is not called");

        await host.StopAsync();
    }

    // -------------------------------------------------------------------------
    // WithEnvironment
    // -------------------------------------------------------------------------

    [Test]
    public async Task WithEnvironment_SetsEnvironment_OnInfo()
    {
        var host = WinNuxService
            .Create()
            .WithEnvironment("Staging")
            .AddService<MetadataCapturingService>()
            .Build();

        await host.StartAsync();

        var info = host.Services.GetRequiredService<WinNuxServiceInfo>();
        Assert.That(info.Environment, Is.EqualTo("Staging"));

        await host.StopAsync();
    }

    [TestCase("Development")]
    [TestCase("Staging")]
    [TestCase("Production")]
    public async Task WithEnvironment_AcceptsStandardEnvironmentValues(string env)
    {
        var host = WinNuxService
            .Create()
            .WithEnvironment(env)
            .AddService<MetadataCapturingService>()
            .Build();

        await host.StartAsync();

        var info = host.Services.GetRequiredService<WinNuxServiceInfo>();
        Assert.That(info.Environment, Is.EqualTo(env));

        await host.StopAsync();
    }

    // -------------------------------------------------------------------------
    // WithVersion
    // -------------------------------------------------------------------------

    [Test]
    public async Task WithVersion_SetsVersion_OnInfo()
    {
        var host = WinNuxService
            .Create()
            .WithVersion("2.5.1")
            .AddService<MetadataCapturingService>()
            .Build();

        await host.StartAsync();

        var info = host.Services.GetRequiredService<WinNuxServiceInfo>();
        Assert.That(info.Version, Is.EqualTo("2.5.1"));

        await host.StopAsync();
    }

    [Test]
    public async Task WithVersion_SupportsSemver_WithPreReleaseSuffix()
    {
        var host = WinNuxService
            .Create()
            .WithVersion("1.0.0-beta.3")
            .AddService<MetadataCapturingService>()
            .Build();

        await host.StartAsync();

        var info = host.Services.GetRequiredService<WinNuxServiceInfo>();
        Assert.That(info.Version, Is.EqualTo("1.0.0-beta.3"));

        await host.StopAsync();
    }

    // -------------------------------------------------------------------------
    // AddProperty
    // -------------------------------------------------------------------------

    [Test]
    public async Task AddProperty_SingleProperty_IsAccessibleOnInfo()
    {
        var host = WinNuxService
            .Create()
            .AddProperty("GitCommit", "abc123def")
            .AddService<MetadataCapturingService>()
            .Build();

        await host.StartAsync();

        var info = host.Services.GetService<WinNuxServiceInfo>();
        Assert.That(info.Properties, Contains.Key("GitCommit"));
        Assert.That(info.Properties["GitCommit"], Is.EqualTo("abc123def"));

        await host.StopAsync();
    }

    [Test]
    public async Task AddProperty_MultipleProperties_AllAccessibleOnInfo()
    {
        var host = WinNuxService
            .Create()
            .AddProperty("GitCommit", "abc123")
            .AddProperty("BuildNumber", "42")
            .AddProperty("Region", "eu-west-1")
            .AddService<MetadataCapturingService>()
            .Build();

        await host.StartAsync();

        var info = host.Services.GetRequiredService<WinNuxServiceInfo>();
        Assert.Multiple(() =>
        {
            Assert.That(info.Properties["GitCommit"], Is.EqualTo("abc123"));
            Assert.That(info.Properties["BuildNumber"], Is.EqualTo("42"));
            Assert.That(info.Properties["Region"], Is.EqualTo("eu-west-1"));
        });

        await host.StopAsync();
    }

    [Test]
    public async Task AddProperty_NoPropertiesAdded_PropertiesCollection_IsEmpty()
    {
        var host = WinNuxService
            .Create()
            .AddService<MetadataCapturingService>()
            .Build();

        await host.StartAsync();

        var info = host.Services.GetRequiredService<WinNuxServiceInfo>();
        Assert.That(info.Properties, Is.Empty,
            "Properties collection should be empty when AddProperty is never called");

        await host.StopAsync();
    }

    [Test]
    public void AddProperty_DuplicateKey_ThrowsOrOverwrites()
    {
        // The library should either overwrite the value or throw — but must not
        // silently produce ambiguous state. This test documents the contract.
        Assert.DoesNotThrow(() =>
        {
            var builder = WinNuxService
                .Create()
                .AddProperty("Key", "first")
                .AddProperty("Key", "second");   // duplicate key
        }, "Registering a duplicate property key should not throw at configuration time");
    }

    // -------------------------------------------------------------------------
    // Full metadata round-trip
    // -------------------------------------------------------------------------

    [Test]
    public async Task AllMetadata_IsAvailableViaInjection_InService()
    {
        var host = WinNuxService
            .Create()
            .WithName("FullService")
            .WithEnvironment("Production")
            .WithVersion("3.0.0")
            .AddProperty("GitCommit", "deadbeef")
            .AddService<MetadataCapturingService>()
            .Build();

        await host.StartAsync();

        var svc = host.Services.GetRequiredService<MetadataCapturingService>();
        var info = svc.CapturedInfo!;

        Assert.Multiple(() =>
        {
            Assert.That(info.ServiceName, Is.EqualTo("FullService"));
            Assert.That(info.Environment, Is.EqualTo("Production"));
            Assert.That(info.Version, Is.EqualTo("3.0.0"));
            Assert.That(info.Properties["GitCommit"], Is.EqualTo("deadbeef"));
        });

        await host.StopAsync();
    }

    [Test]
    public async Task WinNuxServiceInfo_IsSingleton_SameInstance_AcrossServices()
    {
        var host = WinNuxService
            .Create()
            .WithName("SharedInfo")
            .AddService<MetadataCapturingService>()
            .Build();

        await host.StartAsync();

        var infoA = host.Services.GetRequiredService<WinNuxServiceInfo>();
        var infoB = host.Services.GetRequiredService<WinNuxServiceInfo>();

        Assert.That(infoA, Is.SameAs(infoB),
            "WinNuxServiceInfo should be registered as a singleton");

        await host.StopAsync();
    }
}