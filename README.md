# McpNetwork.WinNuxService

**McpNetwork.WinNuxService** is a lightweight .NET library that simplifies building cross-platform services that run as:

* Windows Services
* Linux systemd services
* Console applications (for debugging)
* macOS background processes (via launchd)

Built on top of the **.NET Generic Host**, it provides a clean abstraction to build modular services and **plugin-based architectures** with full runtime plugin management.

Compatible with **.NET 10+**.

---

## Features

* Run the same application as Windows Service, Linux systemd service, or console app
* Built on Microsoft.Extensions.Hosting
* Full Dependency Injection
* Built-in Logging integration
* Multiple services in one host
* Plugin architecture with startup and runtime loading
* Dynamic plugin loading at runtime (load, start, stop, reload, unload)
* Dependency-isolated plugins via `AssemblyLoadContext`
* Optional plugin hot-reload without restarting the host
* Define and access service metadata at startup (`ServiceName`, `Environment`, `Version`, custom properties)

---

## Installation

```cmd
dotnet add package McpNetwork.WinNuxService
```

---

## Quick Start

```csharp
using McpNetwork.WinNuxService;

await WinNuxService
    .Create()
    .WithName("MyService")
    .WithEnvironment("Staging")
    .WithVersion("1.2.3")
    .AddProperty("GitCommit", "abc123def")
    .AddService<TestService>()
    .RunAsync();
```

Your application automatically runs correctly as:

* Windows Service
* Linux systemd service
* Console application

---

## Service Metadata and Build-Time Info

WinNuxService allows you to define **service metadata** at startup. This information is stored in a `WinNuxServiceInfo` object, which is **available via Dependency Injection** in all your services.

### Configuring Metadata

```csharp
var host = WinNuxService
    .Create()
    .WithName("MyService")                  // Sets the service name
    .WithEnvironment("Staging")             // Sets the environment
    .WithVersion("1.2.3")                   // Sets the version
    .AddProperty("GitCommit", "abc123def")  // Add custom key/value
    .AddService<TestService>()
    .Build();
```

* `WithName(string)` – sets the service name
* `WithEnvironment(string)` – sets the environment (Development, Staging, Production)
* `WithVersion(string)` – sets the service version
* `AddProperty(string key, string value)` – adds any custom property

### Accessing Metadata in Services

```csharp
public class TestService : IWinNuxService
{
    private readonly WinNuxServiceInfo _info;

    public TestService(WinNuxServiceInfo info)
    {
        _info = info;
    }

    public Task OnStartAsync(CancellationToken token)
    {
        Console.WriteLine($"Starting {_info.ServiceName} ({_info.Environment}) version {_info.Version}");
        foreach (var prop in _info.Properties)
        {
            Console.WriteLine($"Property: {prop.Key} = {prop.Value}");
        }

        return Task.CompletedTask;
    }

    public Task OnStopAsync(CancellationToken token)
    {
        Console.WriteLine($"Stopping {_info.ServiceName}");
        return Task.CompletedTask;
    }
}
```

### Running the Host

```csharp
await host.RunAsync();  // blocking run

// or start/stop programmatically
await host.StartAsync();
await host.StopAsync();
```

* `RunAsync()` – runs the service host (blocking)
* `StartAsync()` / `StopAsync()` – for finer control
* `ConfigureServices(Action<IServiceProvider>)` – post-build configuration, for setup that requires the fully constructed service provider

```csharp
var host = WinNuxService
    .Create()
    .AddService<MyService>()
    .Build();

host.ConfigureServices(services =>
{
    // Resolve and configure something after the container is built
    var config = services.GetRequiredService<IMyConfig>();
    config.Initialize();
});

await host.RunAsync();
```

---

## Creating a Service Module

Service modules implement `IWinNuxService`.

```csharp
public class TestService : IWinNuxService
{
    public Task OnStartAsync(CancellationToken token)
    {
        _ = RunLoop(token);
        return Task.CompletedTask;
    }

    public Task OnStopAsync(CancellationToken token)
    {
        Console.WriteLine("Service stopping");
        return Task.CompletedTask;
    }

    private async Task RunLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Console.WriteLine("Service running");
            await Task.Delay(5000, token);
        }
    }
}
```

---

## Running Multiple Services

```csharp
await WinNuxService
    .Create()
    .AddService<ServiceA>()
    .AddService<ServiceB>()
    .AddService<ServiceC>()
    .RunAsync();
```

All services run **inside the same host process**.

---

## Dependency Injection

Standard .NET DI works out of the box.

```csharp
.ConfigureServices((ctx, services) =>
{
    services.AddSingleton<IDatabase, Database>();
})
```

Services receive dependencies through constructor injection.

---

## Logging

```csharp
.ConfigureLogging(logging =>
{
    logging.AddConsole();
})
```

Compatible with:

* Serilog
* NLog
* Application Insights
* any `Microsoft.Extensions.Logging` provider

---

## Plugin Architecture

Plugins extend your service at runtime without modifying or redeploying the host. Each plugin is a separate assembly that implements `IWinNuxService`, loaded in its own isolated `AssemblyLoadContext`.

### Directory Structure

```
MyService/
│
├── MyService.exe
│
└── plugins/
    ├── PluginA/
    │   ├── PluginA.dll
    │   └── (PluginA dependencies)
    │
    └── PluginB/
        ├── PluginB.dll
        └── (PluginB dependencies)
```

### Plugin Isolation

Each plugin runs in its own `AssemblyLoadContext`, which means:

* Full dependency isolation — plugins can use different versions of the same library
* Safe unloading — the CLR can collect the plugin's types when unloaded
* No version conflicts between plugins or with the host

---

## Loading Plugins at Startup

To load plugins from external assemblies before the host starts, use `LoadExternalPlugin` on the builder:

```csharp
var host = WinNuxService
    .Create()
    .WithName("MyService")
    .AddService<CoreService>()
    .LoadExternalPlugin("plugins/PluginA/PluginA.dll")
    .LoadExternalPlugin("plugins/PluginB/PluginB.dll")
    .Build();

await host.RunAsync();
```

The builder scans each assembly for types implementing `IWinNuxPlugin` and calls `ConfigureServices` on them, allowing plugins to register their own DI services before the host starts.

---

## Runtime Plugin Management

After the host is started, you can load, start, stop, reload, and unload plugins at any time via `host.Plugins`, which exposes the `IPluginManager` interface.

### IPluginManager API

| Method | Description |
|--------|-------------|
| `Plugins` | Returns the list of currently loaded plugins |
| `LoadPlugin(path, services)` | Loads a plugin DLL — does **not** start it |
| `StartPlugin(plugin)` | Starts a loaded plugin |
| `StopPlugin(plugin)` | Stops a running plugin, keeps it registered |
| `UnloadPlugin(plugin)` | Stops, removes, and unloads a plugin entirely |
| `ReloadPlugin(plugin, services)` | Stops, unloads, reloads, and restarts a plugin in-place |

### LoadedPlugin Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Assembly name of the plugin |
| `Path` | `string` | Full path to the DLL on disk |
| `State` | `PluginState` | Current state: `Loaded`, `Running`, `Stopped`, or `Unloaded` |

### Loading and Starting a Plugin at Runtime

```csharp
var host = WinNuxService
    .Create()
    .WithName("MyService")
    .AddService<CoreService>()
    .Build();

await host.StartAsync();

// Later — load a plugin dynamically
var plugin = host.Plugins.LoadPlugin("plugins/PluginA/PluginA.dll", host.Services);
await host.Plugins.StartPlugin(plugin);

Console.WriteLine($"Plugin '{plugin.Name}' is now {plugin.State}");
// → Plugin 'PluginA' is now Running
```

### Stopping a Plugin

Stops the plugin and cancels its work, but keeps it registered so it can be started again.

```csharp
await host.Plugins.StopPlugin(plugin);
// plugin.State == PluginState.Stopped

// Can be restarted later
await host.Plugins.StartPlugin(plugin);
```

### Unloading a Plugin

Fully removes the plugin: stops it, unloads its `AssemblyLoadContext`, and removes it from the list.

```csharp
await host.Plugins.UnloadPlugin(plugin);
// plugin.State == PluginState.Unloaded
// plugin is no longer in host.Plugins.Plugins
```

### Reloading a Plugin (Hot Reload)

Reloads the plugin in-place from the same DLL path — useful for live updates in production.

```csharp
// Replace the DLL on disk, then:
var newPlugin = await host.Plugins.ReloadPlugin(plugin, host.Services);

// newPlugin is the fresh instance, fully started
Console.WriteLine($"Plugin '{newPlugin.Name}' reloaded — state: {newPlugin.State}");
// → Plugin 'PluginA' reloaded — state: Running
```

Reload sequence:
1. Stop the running plugin (cancels its work)
2. Unload its `AssemblyLoadContext` (releases memory and type locks)
3. Force GC collection (ensures the old context is collected before reloading)
4. Load the new assembly from the same path
5. Start the new plugin instance

### Listing Loaded Plugins

```csharp
foreach (var plugin in host.Plugins.Plugins)
{
    Console.WriteLine($"{plugin.Name} — {plugin.State} — {plugin.Path}");
}
```

### Injecting IPluginManager into Your Services

`IPluginManager` is registered in the DI container, so your own services can receive it through constructor injection:

```csharp
public class ManagementService : IWinNuxService
{
    private readonly IPluginManager _pluginManager;
    private readonly IServiceProvider _services;

    public ManagementService(IPluginManager pluginManager, IServiceProvider services)
    {
        _pluginManager = pluginManager;
        _services = services;
    }

    public async Task OnStartAsync(CancellationToken token)
    {
        // Load a plugin from within a service
        var plugin = _pluginManager.LoadPlugin("plugins/MyPlugin.dll", _services);
        await _pluginManager.StartPlugin(plugin);
    }

    public Task OnStopAsync(CancellationToken token) => Task.CompletedTask;
}
```

---

## Minimal Example (~60 lines)

### Program.cs

```csharp
using McpNetwork.WinNuxService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

await WinNuxService
    .Create()
    .WithName("HeartbeatHost")
    .WithVersion("1.0.0")
    .AddService<HeartbeatService>()
    .AddService<TimeService>()
    .ConfigureServices((ctx, services) =>
    {
        services.AddSingleton<IMessenger, ConsoleMessenger>();
    })
    .ConfigureLogging(logging => logging.AddConsole())
    .RunAsync();

public interface IMessenger { void Send(string message); }

public class ConsoleMessenger : IMessenger
{
    public void Send(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
}
```

### HeartbeatService.cs

```csharp
public class HeartbeatService : IWinNuxService
{
    private readonly IMessenger _messenger;

    public HeartbeatService(IMessenger messenger) { _messenger = messenger; }

    public Task OnStartAsync(CancellationToken token)
    {
        _ = RunLoop(token);
        return Task.CompletedTask;
    }

    public Task OnStopAsync(CancellationToken token)
    {
        _messenger.Send("HeartbeatService stopping");
        return Task.CompletedTask;
    }

    private async Task RunLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            _messenger.Send("HeartbeatService alive");
            await Task.Delay(3000, token);
        }
    }
}
```

### TimeService.cs

```csharp
public class TimeService : IWinNuxService
{
    private readonly IMessenger _messenger;

    public TimeService(IMessenger messenger) { _messenger = messenger; }

    public Task OnStartAsync(CancellationToken token)
    {
        _ = RunLoop(token);
        return Task.CompletedTask;
    }

    public Task OnStopAsync(CancellationToken token)
    {
        _messenger.Send("TimeService stopping");
        return Task.CompletedTask;
    }

    private async Task RunLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            _messenger.Send($"Current time: {DateTime.Now}");
            await Task.Delay(5000, token);
        }
    }
}
```

---

## Platform Support

| Platform | Support           |
| -------- | ----------------- |
| Windows  | Windows Service   |
| Linux    | systemd           |
| macOS    | Console / launchd |

---

## When Should You Use WinNuxService?

**Background Processing Server**

Examples:

* queue consumers
* batch processing
* scheduled jobs

**IoT Gateway**

Examples:

* device communication
* telemetry processing
* protocol plugins loaded and swapped at runtime

**Plugin-Based Enterprise Services**

Examples:

* dynamically extend server capabilities without redeploying
* hot-reload plugins after updating their DLL on disk
* isolate external dependencies per plugin

---

## Requirements

* .NET 10 or later
* Windows / Linux / macOS

---

## License

McpNetwork.WinNuxService is licensed under the MIT License. See [LICENSE](LICENSE) for more information.