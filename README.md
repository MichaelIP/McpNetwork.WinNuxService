# McpNetwork.WinNuxService

**McpNetwork.WinNuxService** is a lightweight .NET library that simplifies building cross-platform services that run as:

* Windows Services
* Linux systemd services
* Console applications (for debugging)
* macOS background processes (via launchd)

Built on top of the **.NET Generic Host**, it provides a clean abstraction to build modular services and **plugin-based architectures**.

Compatible with **.NET 10+**.

---

## Features

* Run the same application as Windows Service, Linux systemd service, or console app
* Built on Microsoft.Extensions.Hosting
* Full Dependency Injection
* Built-in Logging integration
* Multiple services in one host
* Plugin architecture
* Dynamic plugin loading
* Optional plugin hot-reload
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
    .WithName("MyService")                    // Sets the service name
    .WithEnvironment("Staging")               // Sets the environment
    .WithVersion("1.2.3")                     // Sets the version
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

## Plugin Architecture

Plugins can be loaded dynamically from **external assemblies**.

Example directory structure:

```
MyService/
│
├── MyService.exe
│
└── plugins/
    ├── PluginA/
    │   ├── PluginA.dll
    │   └── dependencies
    │
    └── PluginB/
        ├── PluginB.dll
        └── dependencies
```

Each plugin is loaded using its own **AssemblyLoadContext**, allowing:

* dependency isolation
* different dependency versions
* safe unloading
* runtime reloading

---

## Plugin Reloading

Plugins can be reloaded **without restarting the main host**.

Reload sequence:

1. Stop plugin
2. Cancel running tasks
3. Unload AssemblyLoadContext
4. Load new assembly
5. Restart plugin

This enables **live updates in production environments**.

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
* protocol plugins

**Plugin-Based Enterprise Services**

Examples:

* dynamically extend server capabilities
* load new modules without redeploying
* isolate external dependencies

---

## Requirements

* .NET 10 or later
* Windows / Linux / macOS

---

## License

McpNetwork.WinNuxService is licensed under the MIT License. See [LICENSE](LICENSE) for more information.
