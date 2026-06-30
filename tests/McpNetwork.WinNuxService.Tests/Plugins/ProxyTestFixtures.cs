using McpNetwork.WinNuxService.Interfaces;
using McpNetwork.WinNuxService.Plugins;
using McpNetwork.WinNuxService.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace McpNetwork.WinNuxService.Tests.Plugins;

// ===========================================================================
// Cross-context fakes
//
// A real plugin loaded into its own AssemblyLoadContext ends up with a
// *different* runtime Type for IWinNuxService / IConfigurablePlugin than the
// host's copy, even though the source is identical — that's exactly why
// PluginInstanceProxy exists. Spinning up a second AssemblyLoadContext with a
// real compiled assembly just to reproduce that in a unit test is heavy and
// slow. These fakes simulate the same *symptom* cheaply: a type that exposes
// the right method names/signatures (duck typing) without actually
// implementing the host's IWinNuxService — exactly what `instance is
// IWinNuxService` sees for a real cross-context plugin.
// ===========================================================================

/// <summary>
/// Looks like an IWinNuxService (same method names/signatures) but does NOT
/// implement the real interface — simulating a plugin whose own copy of
/// IWinNuxService is a different Type than the host's.
/// </summary>
public class DuckTypedService
{
    public int StartCallCount { get; private set; }
    public int StopCallCount { get; private set; }
    public CancellationToken LastStartToken { get; private set; }
    public CancellationToken LastStopToken { get; private set; }

    public Task OnStartAsync(CancellationToken cancellationToken)
    {
        StartCallCount++;
        LastStartToken = cancellationToken;
        return Task.CompletedTask;
    }

    public Task OnStopAsync(CancellationToken cancellationToken)
    {
        StopCallCount++;
        LastStopToken = cancellationToken;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Same idea as <see cref="DuckTypedService"/> (duck-typed IWinNuxService), but this one
/// also implements the REAL <see cref="IConfigurablePlugin"/> — simulating a plugin that
/// supports named-instance configuration.
/// </summary>
public class DuckTypedConfigurableService : IConfigurablePlugin
{
    public int StartCallCount { get; private set; }
    public int StopCallCount { get; private set; }
    public string? ConfiguredInstanceName { get; private set; }
    public IConfiguration? ConfiguredConfiguration { get; private set; }

    public Task OnStartAsync(CancellationToken cancellationToken)
    {
        StartCallCount++;
        return Task.CompletedTask;
    }

    public Task OnStopAsync(CancellationToken cancellationToken)
    {
        StopCallCount++;
        return Task.CompletedTask;
    }

    public void Configure(string instanceName, IConfiguration configuration)
    {
        ConfiguredInstanceName = instanceName;
        ConfiguredConfiguration = configuration;
    }
}

/// <summary>Missing OnStopAsync entirely — used to verify the proxy fails fast and clearly.</summary>
public class IncompleteDuckTypedService
{
    public Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    // No OnStopAsync on purpose.
}

/// <summary>
/// Implements the REAL IWinNuxService directly — the "same-context" case (e.g. a plugin
/// type that, like the test mocks elsewhere in this project, lives in the same assembly).
/// </summary>
public class DirectWinNuxService : IWinNuxService
{
    public int StartCallCount { get; private set; }
    public int StopCallCount { get; private set; }

    public Task OnStartAsync(CancellationToken cancellationToken)
    {
        StartCallCount++;
        return Task.CompletedTask;
    }

    public Task OnStopAsync(CancellationToken cancellationToken)
    {
        StopCallCount++;
        return Task.CompletedTask;
    }
}

// ===========================================================================
// Testable PluginManager subclasses
// ===========================================================================

/// <summary>
/// Exposes PluginManager's protected CreateInstance directly, so unit tests can exercise
/// the same-context / cross-context fallback logic in isolation, without needing a real
/// plugin DLL.
/// </summary>
public class CreateInstanceTestablePluginManager : PluginManager
{
    public IWinNuxService InvokeCreateInstance(IServiceProvider services, Type serviceType) =>
        CreateInstance(services, serviceType);
}

/// <summary>
/// A PluginManager subclass that overrides only LoadAssembly (same pattern as
/// <see cref="MockablePluginManager"/>) so a given duck-typed fixture type stands in for a
/// real plugin assembly's IWinNuxService implementation. Unlike MockablePluginManager,
/// CreateInstance is left untouched, so tests exercise the REAL CreateInstance →
/// PluginInstanceProxy wrapping path end-to-end via the public LoadPlugin / StartPlugin /
/// StopPlugin / ConfigurePlugin API — the same way a genuine cross-AssemblyLoadContext
/// plugin would flow through the library.
/// </summary>
public class DuckTypingPluginManager : PluginManager
{
    private readonly Type _serviceType;

    public DuckTypingPluginManager(Type serviceType)
    {
        _serviceType = serviceType;
    }

    protected override (PluginLoadContext context, Assembly assembly, Type serviceType) LoadAssembly(string fullPath)
    {
        var assembly = typeof(DuckTypingPluginManager).Assembly;
        var context = new PluginLoadContext(fullPath); // created but never used to load
        return (context, assembly, _serviceType);
    }
}