namespace McpNetwork.WinNuxService.Interfaces;

/// <summary>
/// Optional interface for plugins that support named-instance configuration.
/// Implement this alongside <see cref="IWinNuxService"/> when your plugin
/// needs to be identified by a logical name and/or driven by an
/// <see cref="IConfiguration"/> section.
/// </summary>
public interface IConfigurablePlugin
{
    /// <summary>
    /// Called by <see cref="IPluginManager.ConfigurePlugin"/> after the plugin
    /// is loaded but before it is started.
    /// </summary>
    /// <param name="instanceName">
    /// A logical name that distinguishes this instance when the same DLL is
    /// loaded more than once (e.g. "Sensor-A", "Sensor-B").
    /// </param>
    /// <param name="configuration">
    /// The configuration section (or root) to read settings from.
    /// </param>
    void Configure(string instanceName, IConfiguration configuration);
}