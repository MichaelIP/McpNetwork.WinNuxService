using System;
using System.Reflection;
using System.Runtime.Loader;

namespace McpNetwork.WinNuxService.Plugins;

internal class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath)
        : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);

        if (path != null)
        {
            Console.WriteLine($"[PLUGIN LOAD] {assemblyName} -> {path}");
            return LoadFromAssemblyPath(path);
        }

        Console.WriteLine($"[PLUGIN FALLBACK] {assemblyName}");

        return null;
    }
}