using McpNetwork.WinNuxService.Interfaces;
using System.Reflection;
using System.Threading;

namespace McpNetwork.WinNuxService.Plugins;

public class LoadedPlugin
{
    public string Path { get; init; }

    internal PluginLoadContext Context { get; init; }

    internal Assembly Assembly { get; init; }
    internal IWinNuxService Instance { get; set; }
    internal CancellationTokenSource Cancellation { get; set; } = new();
}