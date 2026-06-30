using McpNetwork.WinNuxService.Interfaces;
using System.Reflection;

namespace McpNetwork.WinNuxService.Plugins;

/// <summary>
/// Wraps a plugin instance whose concrete type was loaded into its own
/// <see cref="PluginLoadContext"/>. Because that load context carries its own
/// copy of <see cref="IWinNuxService"/> / <see cref="IConfigurablePlugin"/>,
/// the plugin's type is technically a *different* runtime type than the
/// host's interfaces, even though the source is identical — so a direct cast
/// (<c>(IWinNuxService)instance</c>) throws <see cref="InvalidCastException"/>.
/// This proxy implements the host's interfaces and forwards every call to the
/// underlying instance via reflection, so the rest of the library (and host
/// code following the documented <c>loaded.Instance is IConfigurablePlugin</c>
/// pattern) keeps working unchanged.
/// </summary>
internal sealed class PluginInstanceProxy : IWinNuxService, IConfigurablePlugin
{
    private readonly object _instance;
    private readonly MethodInfo _onStart;
    private readonly MethodInfo _onStop;
    private readonly MethodInfo? _configure;

    public PluginInstanceProxy(object instance, Type instanceType)
    {
        _instance = instance;

        _onStart = instanceType.GetMethod(nameof(IWinNuxService.OnStartAsync))
            ?? throw new InvalidOperationException(
                $"'{instanceType.FullName}' does not expose an OnStartAsync method matching IWinNuxService.");

        _onStop = instanceType.GetMethod(nameof(IWinNuxService.OnStopAsync))
            ?? throw new InvalidOperationException(
                $"'{instanceType.FullName}' does not expose an OnStopAsync method matching IWinNuxService.");

        // IConfigurablePlugin is optional — only wire it up if the plugin's own
        // (cross-context) copy of the interface shows up by name.
        var configurablePluginFullName = typeof(IConfigurablePlugin).FullName;
        var implementsConfigurable = instanceType
            .GetInterfaces()
            .Any(i => i.FullName == configurablePluginFullName);

        if (implementsConfigurable)
            _configure = instanceType.GetMethod(nameof(IConfigurablePlugin.Configure));
    }

    /// <summary>
    /// True if the underlying plugin instance actually implements its own
    /// (cross-context) copy of <see cref="IConfigurablePlugin"/>.
    /// </summary>
    public bool SupportsConfiguration => _configure is not null;

    /// <summary>
    /// The underlying plugin instance being proxied. Internal — exists mainly so tests
    /// (covered by InternalsVisibleTo) can assert that calls genuinely reached the real
    /// plugin object, without resorting to private-field reflection.
    /// </summary>
    internal object Instance => _instance;

    public Task OnStartAsync(CancellationToken cancellationToken) =>
        (Task)_onStart.Invoke(_instance, [cancellationToken])!;

    public Task OnStopAsync(CancellationToken cancellationToken) =>
        (Task)_onStop.Invoke(_instance, [cancellationToken])!;

    public void Configure(string instanceName, IConfiguration configuration) =>
        _configure?.Invoke(_instance, [instanceName, configuration]);
}