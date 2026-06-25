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
    /// Current lifecycle state of the plugin.
    /// </summary>
    public PluginState State { get; internal set; } = PluginState.Loaded;

    /// <summary>
    /// Logical instance name assigned via <see cref="IPluginManager.ConfigurePlugin"/>.
    /// Empty until ConfigurePlugin is called.
    /// </summary>
    public string InstanceName { get; internal set; } = string.Empty;

    internal PluginLoadContext Context { get; init; } = null!;
    internal Assembly Assembly { get; init; } = null!;
    public IWinNuxService? Instance { get; internal set; }
    internal CancellationTokenSource Cancellation { get; set; } = new();
}