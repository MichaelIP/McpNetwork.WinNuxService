using McpNetwork.WinNuxService.Interfaces;
using McpNetwork.WinNuxService.Models;
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

    /// <inheritdoc />
    public void ConfigurePlugin(LoadedPlugin plugin, string instanceName, IConfiguration configuration)
    {
        EnsureNotUnloaded(plugin);

        plugin.InstanceName = instanceName;

        if (plugin.Instance is IConfigurablePlugin configurable)
            configurable.Configure(instanceName, configuration);

        _logger?.LogInformation("Plugin '{Name}' configured as instance '{InstanceName}'", plugin.Name, instanceName);
    }

    /// <summary>
    /// Registers any services declared by <see cref="IWinNuxPlugin"/> implementations
    /// found in the plugin DLL into <paramref name="services"/>.
    /// Must be called during the builder phase, before <c>Build()</c>, via
    /// <see cref="WinNuxServiceBuilder.RegisterPluginServices"/>.
    /// </summary>
    internal void RegisterPluginServices(string path, HostBuilderContext context, IServiceCollection services)
    {
        var fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Plugin DLL not found: {fullPath}");

        _logger?.LogInformation("Registering plugin services from {Path}", fullPath);

        // Short-lived collectible context — used only to call ConfigureServices,
        // then immediately unloaded before the real runtime PluginLoadContext is created.
        var tempContext = new PluginLoadContext(fullPath);
        try
        {
            var assembly = tempContext.LoadFromAssemblyPath(fullPath);

            // Compare by full name, not type identity: the plugin assembly was loaded into
            // its own AssemblyLoadContext and carries its own copy of IWinNuxPlugin, so
            // typeof(IWinNuxPlugin).IsAssignableFrom(t) would always return false here.
            var winNuxPluginFullName = typeof(IWinNuxPlugin).FullName;

            var pluginTypes = assembly
                .GetTypes()
                .Where(t =>
                    t.GetInterfaces().Any(i => i.FullName == winNuxPluginFullName) &&
                    !t.IsAbstract &&
                    !t.IsInterface);

            foreach (var type in pluginTypes)
            {
                // Can't cast across load contexts either — invoke via reflection.
                var plugin = Activator.CreateInstance(type)!;
                var method = type.GetMethod(nameof(IWinNuxPlugin.ConfigureServices));
                method?.Invoke(plugin, new object[] { context, services });
                _logger?.LogInformation("Registered services from plugin type '{Type}'", type.Name);
            }
        }
        finally
        {
            tempContext.Unload();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    /// <summary>
    /// Loads the assembly from disk and resolves the IWinNuxService implementation type.
    /// Override in tests to skip real DLL loading entirely.
    /// </summary>
    protected virtual (PluginLoadContext context, Assembly assembly, Type serviceType) LoadAssembly(string fullPath)
    {
        var context = new PluginLoadContext(fullPath, _logger);
        var assembly = context.LoadFromAssemblyPath(fullPath);

        // Compare by full name, not type identity: the plugin assembly is loaded into
        // its own PluginLoadContext and carries its own copy of IWinNuxService, so
        // typeof(IWinNuxService).IsAssignableFrom(t) is always false here — it would
        // throw "no type found" even for a perfectly valid plugin.
        var winNuxServiceFullName = typeof(IWinNuxService).FullName;

        var serviceType = assembly
            .GetTypes()
            .FirstOrDefault(t =>
                t.GetInterfaces().Any(i => i.FullName == winNuxServiceFullName) &&
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
    /// <remarks>
    /// <paramref name="serviceType"/> usually comes from a separate
    /// <see cref="PluginLoadContext"/> and therefore carries its own copy of
    /// <see cref="IWinNuxService"/> — a direct <c>(IWinNuxService)</c> cast would throw
    /// <see cref="InvalidCastException"/> even though the instance genuinely implements
    /// the (source-identical) interface. When the instance isn't already assignable —
    /// i.e. it's a real cross-context plugin rather than a same-context test mock — it's
    /// wrapped in a <see cref="PluginInstanceProxy"/> that forwards calls by reflection.
    /// </remarks>
    protected virtual IWinNuxService CreateInstance(IServiceProvider services, Type serviceType)
    {
        var instance = ActivatorUtilities.CreateInstance(services, serviceType);

        if (instance is IWinNuxService direct)
            return direct;

        return new PluginInstanceProxy(instance, serviceType);
    }

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