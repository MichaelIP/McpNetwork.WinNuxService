using McpNetwork.WinNuxService.Interfaces;
using McpNetwork.WinNuxService.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace McpNetwork.WinNuxService.Plugins;

public class PluginManager : IPluginManager
{
    private readonly List<LoadedPlugin> _plugins = [];
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<PluginManager>? _logger;

    public PluginManager(ILogger<PluginManager>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<LoadedPlugin> Plugins
    {
        get
        {
            _lock.Wait();
            try { return _plugins.ToList(); }
            finally { _lock.Release(); }
        }
    }

    /// <inheritdoc />
    public LoadedPlugin LoadPlugin(string path, IServiceProvider services)
    {
        var fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Plugin DLL not found: {fullPath}");

        _logger?.LogInformation("Loading plugin from {Path}", fullPath);

        var (context, assembly, serviceType) = LoadAssembly(fullPath);
        var instance = CreateInstance(services, serviceType);

        var plugin = new LoadedPlugin
        {
            Path = fullPath,
            Name = assembly.GetName().Name ?? Path.GetFileNameWithoutExtension(fullPath),
            Context = context,
            Assembly = assembly,
            Instance = instance,
            State = PluginState.Loaded
        };

        _lock.Wait();
        try { _plugins.Add(plugin); }
        finally { _lock.Release(); }

        _logger?.LogInformation("Plugin '{Name}' loaded (type: {Type})", plugin.Name, serviceType.Name);

        return plugin;
    }

    /// <summary>
    /// Loads the assembly from disk and resolves the IWinNuxService implementation type.
    /// Override in tests to skip real DLL loading entirely.
    /// </summary>
    protected virtual (PluginLoadContext context, Assembly assembly, Type serviceType) LoadAssembly(string fullPath)
    {
        var context = new PluginLoadContext(fullPath);
        var assembly = context.LoadFromAssemblyPath(fullPath);

        var serviceType = assembly
            .GetTypes()
            .FirstOrDefault(t =>
                typeof(IWinNuxService).IsAssignableFrom(t) &&
                !t.IsAbstract &&
                !t.IsInterface)
            ?? throw new InvalidOperationException(
                $"No public non-abstract type implementing IWinNuxService found in '{fullPath}'.");

        return (context, assembly, serviceType);
    }

    /// <summary>
    /// Factory method that creates a plugin service instance.
    /// Override in tests to inject mocks without loading a real assembly.
    /// </summary>
    protected virtual IWinNuxService CreateInstance(IServiceProvider services, Type serviceType)
        => (IWinNuxService)ActivatorUtilities.CreateInstance(services, serviceType);

    /// <inheritdoc />
    public async Task StartPlugin(LoadedPlugin plugin)
    {
        EnsureNotUnloaded(plugin);

        _logger?.LogInformation("Starting plugin '{Name}'", plugin.Name);

        plugin.State = PluginState.Running;

        await plugin.Instance!.OnStartAsync(plugin.Cancellation.Token);
    }

    /// <inheritdoc />
    public async Task StopPlugin(LoadedPlugin plugin)
    {
        if (plugin.State == PluginState.Unloaded)
            return;

        _logger?.LogInformation("Stopping plugin '{Name}'", plugin.Name);

        plugin.Cancellation.Cancel();

        try
        {
            await plugin.Instance!.OnStopAsync(plugin.Cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // expected — the token was cancelled
        }

        plugin.Cancellation.Dispose();
        plugin.Cancellation = new CancellationTokenSource();

        plugin.State = PluginState.Stopped;

        _logger?.LogInformation("Plugin '{Name}' stopped", plugin.Name);
    }

    /// <inheritdoc />
    public async Task UnloadPlugin(LoadedPlugin plugin)
    {
        EnsureNotUnloaded(plugin);

        _logger?.LogInformation("Unloading plugin '{Name}'", plugin.Name);

        if (plugin.State == PluginState.Running)
            await StopPlugin(plugin);

        plugin.Instance = null;

        plugin.Context.Unload();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        plugin.State = PluginState.Unloaded;

        _lock.Wait();
        try { _plugins.Remove(plugin); }
        finally { _lock.Release(); }

        _logger?.LogInformation("Plugin '{Name}' unloaded and removed", plugin.Name);
    }

    /// <inheritdoc />
    public async Task<LoadedPlugin> ReloadPlugin(LoadedPlugin plugin, IServiceProvider services)
    {
        EnsureNotUnloaded(plugin);

        var path = plugin.Path;
        var name = plugin.Name;

        _logger?.LogInformation("Reloading plugin '{Name}' from {Path}", name, path);

        // Stop if running, then fully unload (removes from list)
        await UnloadPlugin(plugin);

        // Load fresh from the same path (re-adds to list)
        var newPlugin = LoadPlugin(path, services);

        // Start the new instance immediately
        await StartPlugin(newPlugin);

        _logger?.LogInformation("Plugin '{Name}' reloaded successfully", name);

        return newPlugin;
    }

    private static void EnsureNotUnloaded(LoadedPlugin plugin)
    {
        if (plugin.State == PluginState.Unloaded)
            throw new InvalidOperationException(
                $"Plugin '{plugin.Name}' has already been unloaded and cannot be used.");
    }
}