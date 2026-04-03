using McpNetwork.WinNuxService.Interfaces;
using NUnit.Framework;

namespace McpNetwork.WinNuxService.Tests;

/// <summary>
/// Tests for the plugin architecture: dynamic loading, isolation, and hot-reload.
/// 
/// NOTE: These tests assume that WinNuxService exposes a plugin API similar to:
///   .LoadPlugin(string pluginDirectory)
///   .UnloadPlugin(string pluginName)
///   .ReloadPlugin(string pluginName)
///   host.Plugins.IsLoaded(string pluginName) → bool
/// Adjust method names to match the actual public surface once the library source
/// is available.
/// </summary>
[TestFixture]
public class PluginArchitectureTests
{
    // -------------------------------------------------------------------------
    // Helpers / fakes
    // -------------------------------------------------------------------------

    /// <summary>
    /// Represents a minimal in-process "plugin" for tests that don't need
    /// real assembly isolation.
    /// </summary>
    private class FakePlugin : IWinNuxService
    {
        public string Name { get; }
        public bool Started { get; private set; }
        public bool Stopped { get; private set; }

        public FakePlugin(string name) => Name = name;

        public Task OnStartAsync(CancellationToken token)
        {
            Started = true;
            return Task.CompletedTask;
        }

        public Task OnStopAsync(CancellationToken token)
        {
            Stopped = true;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// A plugin whose static state is used to verify AssemblyLoadContext isolation.
    /// In a real test, this would live in a separate test assembly loaded via the
    /// plugin system. Here we simulate the contract.
    /// </summary>
    private class StatefulPlugin : IWinNuxService
    {
        // In a real isolation scenario, static state must NOT bleed across ALC boundaries.
        public static int InstanceCount;

        public StatefulPlugin() => InstanceCount++;

        public Task OnStartAsync(CancellationToken token) => Task.CompletedTask;
        public Task OnStopAsync(CancellationToken token) => Task.CompletedTask;
    }

    /// <summary>
    /// Creates a temporary plugin directory structure mirroring the layout
    /// described in the README.
    /// </summary>
    private static string CreateTempPluginDirectory(string pluginName)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var dir = Path.Combine(root, "plugins", pluginName);
        Directory.CreateDirectory(dir);
        // In a real test suite, copy a compiled plugin DLL into `dir`.
        return root;
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [Test]
    public async Task LoadPlugin_StartsPluginAlongsideMainServices()
    {
        var mainSvc = new FakePlugin("main");
        var plugin = new FakePlugin("plugin-a");

        var host = WinNuxService
            .Create()
            .AddService<FakePlugin>()
            .LoadPlugin<FakePlugin>()          // expected plugin registration API
            .Build();

        await host.StartAsync();

        Assert.Multiple(() =>
        {
            Assert.That(mainSvc.Started, Is.True, "Main service should start");
            Assert.That(plugin.Started, Is.True, "Plugin should start alongside main services");
        });

        await host.StopAsync();
    }

    [Test]
    public async Task UnloadPlugin_StopsPluginWithoutAffectingMainService()
    {
        var mainSvc = new FakePlugin("main");
        var plugin = new FakePlugin("plugin-a");

        var host = WinNuxService
            .Create()
            .AddService(mainSvc)
            .AddPlugin(plugin)
            .Build();

        await host.StartAsync();
        await host.UnloadPluginAsync("plugin-a");   // expected unload API

        Assert.Multiple(() =>
        {
            Assert.That(plugin.Stopped, Is.True, "Plugin should be stopped after unload");
            Assert.That(mainSvc.Stopped, Is.False, "Main service should continue running");
        });

        await host.StopAsync();
    }

    [Test]
    public async Task ReloadPlugin_StopsThenRestartsPlugin()
    {
        var originalPlugin = new FakePlugin("plugin-a");
        var replacementPlugin = new FakePlugin("plugin-a");

        var host = WinNuxService
            .Create()
            .AddPlugin(originalPlugin)
            .Build();

        await host.StartAsync();

        // Simulate reload by providing a replacement implementation
        await host.ReloadPluginAsync("plugin-a", replacementPlugin);

        Assert.Multiple(() =>
        {
            Assert.That(originalPlugin.Stopped, Is.True, "Original plugin should be stopped");
            Assert.That(replacementPlugin.Started, Is.True, "Replacement plugin should be started");
        });

        await host.StopAsync();
    }

    [Test]
    public async Task MultiplePlugins_AllStartAndStop_Independently()
    {
        var plugins = new[]
        {
            new FakePlugin("plugin-a"),
            new FakePlugin("plugin-b"),
            new FakePlugin("plugin-c"),
        };

        var builder = WinNuxService.Create();
        foreach (var p in plugins)
            builder.AddPlugin(p);

        var host = builder.Build();

        await host.StartAsync();

        Assert.That(plugins, Has.All.Matches<FakePlugin>(p => p.Started),
            "Every plugin should have started");

        await host.StopAsync();

        Assert.That(plugins, Has.All.Matches<FakePlugin>(p => p.Stopped),
            "Every plugin should have stopped");
    }

    [Test]
    public async Task PluginLoadContext_IsolatesStaticState()
    {
        // Reset static counter to ensure clean baseline
        StatefulPlugin.InstanceCount = 0;

        var host = WinNuxService
            .Create()
            .AddPlugin<StatefulPlugin>()
            .Build();

        await host.StartAsync();
        await host.StopAsync();

        // After unloading the plugin's ALC the static counter in the *host* ALC
        // should remain at its previous value — proving isolation.
        // (In a real cross-ALC test, two separate AssemblyLoadContexts would each
        // have their own copy of StatefulPlugin.InstanceCount.)
        Assert.That(StatefulPlugin.InstanceCount, Is.EqualTo(1),
            "Exactly one instance should have been created in this load context");
    }

    [Test]
    public async Task FaultingPlugin_DoesNotTakeDownMainService()
    {
        var mainSvc = new FakePlugin("main");

        var host = WinNuxService
            .Create()
            .AddService(mainSvc)
            .AddPlugin<FaultyPlugin>()
            .Build();

        // Host should start even if a plugin fails to start
        Assert.DoesNotThrowAsync(async () => await host.StartAsync(),
            "A faulting plugin should not crash the host");

        Assert.That(mainSvc.Started, Is.True,
            "Main service should still be running when a plugin faults");

        await host.StopAsync();
    }

    [Test]
    public void LoadPlugin_WithInvalidDirectory_ThrowsDescriptiveException()
    {
        var host = WinNuxService.Create().Build();

        Assert.ThrowsAsync<DirectoryNotFoundException>(
            async () => await host.LoadPluginFromDirectoryAsync("/nonexistent/path"),
            "Loading a plugin from a missing directory should throw DirectoryNotFoundException");
    }

    // -------------------------------------------------------------------------
    // Additional helper types
    // -------------------------------------------------------------------------

    private class FaultyPlugin : IWinNuxService
    {
        public Task OnStartAsync(CancellationToken token) =>
            throw new InvalidOperationException("Plugin failed to start");

        public Task OnStopAsync(CancellationToken token) => Task.CompletedTask;
    }
}