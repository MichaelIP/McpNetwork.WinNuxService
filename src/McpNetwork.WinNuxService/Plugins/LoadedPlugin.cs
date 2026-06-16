using McpNetwork.WinNuxService.Interfaces;
using McpNetwork.WinNuxService.Models;
using System.Reflection;

namespace McpNetwork.WinNuxService.Plugins;

public class LoadedPlugin
{
    /// <summary>
    /// Full path to the plugin DLL on disk.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Friendly name derived from the assembly name.
    /// </summary>
    public string Name { get; internal set; } = string.Empty;

    /// <summary>
    /// Logical instance name assigned by <see cref="IPluginManager.ConfigurePlugin"/>.
    /// Null until ConfigurePlugin is called. Useful when the same DLL is loaded
    /// more than once and each instance needs a distinct identity.
    /// </summary>
    public string? InstanceName { get; internal set; }

    /// <summary>
    /// Current lifecycle state of the plugin.
    /// </summary>
    public PluginState State { get; internal set; } = PluginState.Loaded;

    internal PluginLoadContext Context { get; init; } = null!;
    internal Assembly Assembly { get; init; } = null!;
    internal IWinNuxService? Instance { get; set; }
    internal CancellationTokenSource Cancellation { get; set; } = new();
}
