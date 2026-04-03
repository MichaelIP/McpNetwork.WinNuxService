using McpNetwork.WinNuxService.Plugins;

namespace McpNetwork.WinNuxService.Interfaces;

public interface IPluginManager
{
    /// <summary>
    /// Returns a snapshot of all currently loaded plugins.
    /// </summary>
    IReadOnlyList<LoadedPlugin> Plugins { get; }

    /// <summary>
    /// Loads a plugin from the given DLL path and registers it.
    /// Does NOT start it — call StartPlugin explicitly.
    /// </summary>
    LoadedPlugin LoadPlugin(string path, IServiceProvider services);

    /// <summary>
    /// Starts a previously loaded plugin.
    /// </summary>
    Task StartPlugin(LoadedPlugin plugin);

    /// <summary>
    /// Stops a running plugin and cancels its work, but keeps it registered.
    /// </summary>
    Task StopPlugin(LoadedPlugin plugin);

    /// <summary>
    /// Unloads a plugin fully: stops it, removes it from the list, and unloads its AssemblyLoadContext.
    /// </summary>
    Task UnloadPlugin(LoadedPlugin plugin);

    /// <summary>
    /// Reloads a plugin in-place: stops it, unloads it, reloads the DLL from the same path, starts it.
    /// Returns the new LoadedPlugin instance replacing the old one.
    /// </summary>
    Task<LoadedPlugin> ReloadPlugin(LoadedPlugin plugin, IServiceProvider services);
}