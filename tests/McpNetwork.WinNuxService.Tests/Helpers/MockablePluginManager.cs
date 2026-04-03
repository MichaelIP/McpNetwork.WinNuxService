using McpNetwork.WinNuxService.Interfaces;
using McpNetwork.WinNuxService.Plugins;
using System.Reflection;

namespace McpNetwork.WinNuxService.Tests.Helpers;

/// <summary>
/// A PluginManager subclass that overrides LoadAssembly so tests can inject
/// a mock IWinNuxService without needing a valid .NET assembly on disk.
///
/// The DLL path must still exist as a file (use CreateDummyDll) because
/// LoadPlugin checks File.Exists before calling LoadAssembly.
/// </summary>
public class MockablePluginManager : PluginManager
{
    private readonly IWinNuxService _mockInstance;

    public MockablePluginManager(IWinNuxService mockInstance)
    {
        _mockInstance = mockInstance;
    }

    protected override (PluginLoadContext context, Assembly assembly, Type serviceType) LoadAssembly(string fullPath)
    {
        // Use the test assembly itself as a stand-in — it is a valid loaded
        // assembly so GetName() works, and we supply the serviceType directly.
        var assembly = typeof(MockablePluginManager).Assembly;
        var context = new PluginLoadContext(fullPath);   // created but never used to load
        var serviceType = typeof(MockService);

        return (context, assembly, serviceType);
    }

    protected override IWinNuxService CreateInstance(IServiceProvider services, Type serviceType)
        => _mockInstance;

    /// <summary>
    /// Creates a zero-byte temp file that satisfies File.Exists.
    /// </summary>
    public static string CreateDummyDll(string? name = null)
    {
        var path = Path.Combine(Path.GetTempPath(), name ?? $"TestPlugin_{Guid.NewGuid():N}.dll");
        File.WriteAllBytes(path, []);
        return path;
    }

    // Stand-in type so LoadAssembly can return a valid non-abstract IWinNuxService type
    private class MockService : IWinNuxService
    {
        public Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}