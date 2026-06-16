using System.Reflection;
using System.Runtime.Loader;

namespace McpNetwork.WinNuxService.Plugins;

public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly ILogger? _logger;

    public PluginLoadContext(string pluginPath, ILogger? logger = null) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
        _logger = logger;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);

        if (path != null)
        {
            _logger?.LogDebug($"[PLUGIN LOAD] {assemblyName} -> {path}");
            return LoadFromAssemblyPath(path);
        }

        _logger?.LogDebug($"[PLUGIN FALLBACK] {assemblyName}");

        return null;
    }
}