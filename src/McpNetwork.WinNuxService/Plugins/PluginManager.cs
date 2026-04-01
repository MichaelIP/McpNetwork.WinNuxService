using McpNetwork.WinNuxService.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace McpNetwork.WinNuxService.Plugins;

public class PluginManager
{
    private readonly List<LoadedPlugin> _plugins = [];

    public IReadOnlyCollection<LoadedPlugin> Plugins => _plugins;

    public LoadedPlugin LoadPlugin(string path, IServiceProvider services)
    {
        var fullPath = Path.GetFullPath(path);

        var context = new PluginLoadContext(fullPath);

        var assembly = context.LoadFromAssemblyPath(fullPath);

        var serviceType = assembly
            .GetTypes()
            .First(t => typeof(IWinNuxService).IsAssignableFrom(t));

        var instance = (IWinNuxService)ActivatorUtilities.CreateInstance(services, serviceType);

        var plugin = new LoadedPlugin
        {
            Path = fullPath,
            Context = context,
            Assembly = assembly,
            Instance = instance
        };

        _plugins.Add(plugin);

        return plugin;
    }

    public async Task<LoadedPlugin> ReloadPlugin(LoadedPlugin plugin, IServiceProvider services)
    {
        // Stop the running plugin tasks
        await StopPlugin(plugin);

        // Clear instance reference
        plugin.Instance = null;

        // Unload the old assembly context
        plugin.Context.Unload();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Load new plugin
        var newPlugin = LoadPlugin(plugin.Path, services);

        // Start it immediately
        await StartPlugin(newPlugin);

        return newPlugin;
    }

    public Task StartPlugin(LoadedPlugin plugin)
    {
        return plugin.Instance.OnStartAsync(plugin.Cancellation.Token);
    }

    public async Task StopPlugin(LoadedPlugin plugin)
    {
        // Cancel the repeating loop
        plugin.Cancellation.Cancel();

        try
        {
            await plugin.Instance.OnStopAsync(plugin.Cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // expected
        }

        // Dispose CTS and prepare for next run
        plugin.Cancellation.Dispose();
        plugin.Cancellation = new CancellationTokenSource();
    }

}