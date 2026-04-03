using McpNetwork.WinNuxService.Interfaces;
using McpNetwork.WinNuxService.Models;
using McpNetwork.WinNuxService.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;

namespace McpNetwork.WinNuxService.Tests;

[TestFixture]
public class PluginManagerTests
{
    // Each test gets its own mock service instance and a fresh manager
    private IWinNuxService _mockService = null!;
    private MockablePluginManager _manager = null!;
    private IServiceProvider _services = null!;
    private string _dllPath = null!;

    [SetUp]
    public void SetUp()
    {
        _mockService = Substitute.For<IWinNuxService>();
        _mockService.OnStartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _mockService.OnStopAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        _manager = new MockablePluginManager(_mockService);
        _services = new ServiceCollection().BuildServiceProvider();
        _dllPath = MockablePluginManager.CreateDummyDll();
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_dllPath))
            File.Delete(_dllPath);
    }

    // -------------------------------------------------------------------------
    // LoadPlugin
    // -------------------------------------------------------------------------

    [Test]
    public void LoadPlugin_AddsPluginToList()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);

        Assert.That(_manager.Plugins, Has.Count.EqualTo(1));
        Assert.That(_manager.Plugins, Contains.Item(plugin));
    }

    [Test]
    public void LoadPlugin_SetsCorrectPath()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);

        Assert.That(plugin.Path, Is.EqualTo(Path.GetFullPath(_dllPath)));
    }

    [Test]
    public void LoadPlugin_SetsInitialStateToLoaded()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);

        Assert.That(plugin.State, Is.EqualTo(PluginState.Loaded));
    }

    [Test]
    public void LoadPlugin_SetsPluginName()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);

        // Name falls back to filename without extension when assembly name is null
        Assert.That(plugin.Name, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void LoadPlugin_MissingFile_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() =>
            _manager.LoadPlugin("nonexistent/plugin.dll", _services));
    }

    [Test]
    public void LoadPlugin_MultiplePlugins_AllAppearInList()
    {
        var path2 = MockablePluginManager.CreateDummyDll();

        try
        {
            _manager.LoadPlugin(_dllPath, _services);
            _manager.LoadPlugin(path2, _services);

            Assert.That(_manager.Plugins, Has.Count.EqualTo(2));
        }
        finally
        {
            File.Delete(path2);
        }
    }

    // -------------------------------------------------------------------------
    // StartPlugin
    // -------------------------------------------------------------------------

    [Test]
    public async Task StartPlugin_SetsStateToRunning()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);

        await _manager.StartPlugin(plugin);

        Assert.That(plugin.State, Is.EqualTo(PluginState.Running));
    }

    [Test]
    public async Task StartPlugin_CallsOnStartAsync()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);

        await _manager.StartPlugin(plugin);

        await _mockService.Received(1).OnStartAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public void StartPlugin_OnUnloadedPlugin_ThrowsInvalidOperationException()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);
        plugin.State = PluginState.Unloaded;

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _manager.StartPlugin(plugin));
    }

    // -------------------------------------------------------------------------
    // StopPlugin
    // -------------------------------------------------------------------------

    [Test]
    public async Task StopPlugin_SetsStateToStopped()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);
        await _manager.StartPlugin(plugin);

        await _manager.StopPlugin(plugin);

        Assert.That(plugin.State, Is.EqualTo(PluginState.Stopped));
    }

    [Test]
    public async Task StopPlugin_CallsOnStopAsync()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);
        await _manager.StartPlugin(plugin);

        await _manager.StopPlugin(plugin);

        await _mockService.Received(1).OnStopAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task StopPlugin_PluginCanBeRestartedAfterStop()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);
        await _manager.StartPlugin(plugin);
        await _manager.StopPlugin(plugin);

        // Should not throw, and state goes back to Running
        await _manager.StartPlugin(plugin);

        Assert.That(plugin.State, Is.EqualTo(PluginState.Running));
    }

    [Test]
    public async Task StopPlugin_WhenAlreadyUnloaded_DoesNotThrow()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);
        plugin.State = PluginState.Unloaded;

        // StopPlugin is a no-op on unloaded plugins
        Assert.DoesNotThrowAsync(() => _manager.StopPlugin(plugin));

        await Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // UnloadPlugin
    // -------------------------------------------------------------------------

    [Test]
    public async Task UnloadPlugin_RemovesPluginFromList()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);

        await _manager.UnloadPlugin(plugin);

        Assert.That(_manager.Plugins, Is.Empty);
    }

    [Test]
    public async Task UnloadPlugin_SetsStateToUnloaded()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);

        await _manager.UnloadPlugin(plugin);

        Assert.That(plugin.State, Is.EqualTo(PluginState.Unloaded));
    }

    [Test]
    public async Task UnloadPlugin_WhenRunning_StopsPluginFirst()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);
        await _manager.StartPlugin(plugin);

        await _manager.UnloadPlugin(plugin);

        await _mockService.Received(1).OnStopAsync(Arg.Any<CancellationToken>());
        Assert.That(plugin.State, Is.EqualTo(PluginState.Unloaded));
    }

    [Test]
    public async Task UnloadPlugin_ClearsInstanceReference()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);

        await _manager.UnloadPlugin(plugin);

        Assert.That(plugin.Instance, Is.Null);
    }

    [Test]
    public async Task UnloadPlugin_ThenCallAgain_ThrowsInvalidOperationException()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);
        await _manager.UnloadPlugin(plugin);

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _manager.UnloadPlugin(plugin));
    }

    // -------------------------------------------------------------------------
    // ReloadPlugin
    // -------------------------------------------------------------------------

    [Test]
    public async Task ReloadPlugin_ReturnsNewPluginInstance()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);
        await _manager.StartPlugin(plugin);

        var reloaded = await _manager.ReloadPlugin(plugin, _services);

        Assert.That(reloaded, Is.Not.SameAs(plugin));
    }

    [Test]
    public async Task ReloadPlugin_NewPluginIsRunning()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);
        await _manager.StartPlugin(plugin);

        var reloaded = await _manager.ReloadPlugin(plugin, _services);

        Assert.That(reloaded.State, Is.EqualTo(PluginState.Running));
    }

    [Test]
    public async Task ReloadPlugin_OldPluginIsRemovedFromList()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);
        await _manager.StartPlugin(plugin);

        await _manager.ReloadPlugin(plugin, _services);

        Assert.That(_manager.Plugins, Does.Not.Contain(plugin));
    }

    [Test]
    public async Task ReloadPlugin_ListContainsExactlyOnePlugin()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);
        await _manager.StartPlugin(plugin);

        await _manager.ReloadPlugin(plugin, _services);

        Assert.That(_manager.Plugins, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ReloadPlugin_NewPluginHasSamePath()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);
        await _manager.StartPlugin(plugin);

        var reloaded = await _manager.ReloadPlugin(plugin, _services);

        Assert.That(reloaded.Path, Is.EqualTo(plugin.Path));
    }

    [Test]
    public void ReloadPlugin_OnUnloadedPlugin_ThrowsInvalidOperationException()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);
        plugin.State = PluginState.Unloaded;

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _manager.ReloadPlugin(plugin, _services));
    }

    // -------------------------------------------------------------------------
    // Plugins list snapshot
    // -------------------------------------------------------------------------

    [Test]
    public async Task Plugins_IsSnapshot_NotLiveReference()
    {
        var plugin = _manager.LoadPlugin(_dllPath, _services);

        // Capture list before unload
        var snapshot = _manager.Plugins;

        await _manager.UnloadPlugin(plugin);

        // The snapshot still contains the plugin; the live list does not
        Assert.That(snapshot, Contains.Item(plugin));
        Assert.That(_manager.Plugins, Is.Empty);
    }

    // -------------------------------------------------------------------------
    // Concurrency
    // -------------------------------------------------------------------------

    [Test]
    public async Task LoadPlugin_ConcurrentCalls_AllPluginsRegistered()
    {
        const int count = 10;

        var paths = Enumerable.Range(0, count)
            .Select(_ => MockablePluginManager.CreateDummyDll())
            .ToList();

        try
        {
            var tasks = paths.Select(p =>
                Task.Run(() => _manager.LoadPlugin(p, _services)));

            await Task.WhenAll(tasks);

            Assert.That(_manager.Plugins, Has.Count.EqualTo(count));
        }
        finally
        {
            foreach (var p in paths)
                File.Delete(p);
        }
    }
}