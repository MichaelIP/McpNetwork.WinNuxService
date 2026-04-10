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
* Plugin architecture with startup and runtime loading
* Dynamic plugin loading at runtime (load, start, stop, reload, unload)
* Dependency-isolated plugins via `AssemblyLoadContext`
* Optional plugin hot-reload without restarting the host* Define and access service metadata at startup (`ServiceName`, `Environment`, `Version`, custom properties)
* **Embedded HTTP server and SignalR support via `WithWebHost()`**

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
    .WithName("MyService")                   // Sets the service name
    .WithEnvironment("Staging")              // Sets the environment
    .WithVersion("1.2.3")                    // Sets the version
    .AddProperty("GitCommit", "abc123def")   // Add custom key/value
    .AddService<TestService>()
    .Build();
```

* `WithName(string)` – sets the service name
* `WithEnvironment(string)` – sets the environment (Development, Staging, Production)
* `WithVersion(string)` – sets the service version
* `AddProperty(string key, string value)` – adds any custom property

### Accessing Metadata in Services

```csharp
public class TestService : WinNuxServiceBase
{
    private readonly WinNuxServiceInfo _info;

    public TestService(WinNuxServiceInfo info)
    {
        _info = info;
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        Console.WriteLine($"Starting {_info.ServiceName} ({_info.Environment}) v{_info.Version}");
        foreach (var prop in _info.Properties)
            Console.WriteLine($"  {prop.Key} = {prop.Value}");

        await Task.Delay(Timeout.Infinite, token);
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
* `StartAsync()` – starts the host and **waits until all services have fully completed `OnStartAsync`** before returning
* `StopAsync()` – stops the host, calling `OnStopAsync` on all services
* `ConfigureServices(Action<IServiceProvider>)` – post-build configuration, for setup that requires the fully constructed service provider

---

## Creating a Service

### Recommended: inherit `WinNuxServiceBase`

`WinNuxServiceBase` is the recommended way to implement a service. It handles all cancellation
boilerplate for you — including safe shutdown regardless of host type (plain, web, Windows Service,
systemd). You only need to implement `ExecuteAsync`.

```csharp
public class HeartbeatService : WinNuxServiceBase
{
    private readonly ILogger<HeartbeatService> _logger;

    public HeartbeatService(ILogger<HeartbeatService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            _logger.LogInformation("Heartbeat at {Time}", DateTime.Now);
            await Task.Delay(TimeSpan.FromSeconds(5), token);
        }
    }
}
```

There is no need to manage `CancellationTokenSource`, `try/catch` for `OperationCanceledException`,
or override `OnStartAsync` / `OnStopAsync`. The base class takes care of all of it.

Unexpected exceptions thrown from `ExecuteAsync` are surfaced via the `OnUnhandledException` hook,
which rethrows by default. Override it to log and swallow instead:

```csharp
protected override void OnUnhandledException(Exception ex)
{
    _logger.LogCritical(ex, "Unhandled error in {Service}", GetType().Name);
    // swallow — host keeps running
}
```

### Advanced: override `OnStartAsync` / `OnStopAsync`

Two cases legitimately require overriding the lifecycle methods.

**Case 1 — You need to acquire or release external resources (connections, file handles, etc.):**

```csharp
public class DatabasePollerService : WinNuxServiceBase
{
    private SqlConnection? _connection;

    public override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _connection = new SqlConnection("...");
        await _connection.OpenAsync(cancellationToken);
        await base.OnStartAsync(cancellationToken); // always call base
    }

    public override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        await base.OnStopAsync(cancellationToken);  // always call base first
        await _connection!.DisposeAsync();
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            // poll database...
            await Task.Delay(TimeSpan.FromSeconds(10), token);
        }
    }
}
```

> **Important:** always call `base.OnStartAsync()` and `base.OnStopAsync()`. Omitting either will
> break the internal cancellation lifecycle.

**Case 2 — You need custom exception handling per service:**

```csharp
public class ResilientService : WinNuxServiceBase
{
    private readonly ILogger<ResilientService> _logger;

    public ResilientService(ILogger<ResilientService> logger) => _logger = logger;

    protected override void OnUnhandledException(Exception ex)
    {
        _logger.LogCritical(ex, "ResilientService crashed — host stays alive");
        // swallow intentionally
    }

    protected override async Task ExecuteAsync(CancellationToken token) { ... }
}
```

### Low-level: implement `IWinNuxService` directly

For maximum control, you can implement the interface directly. This is rarely needed.

```csharp
public class TestService : IWinNuxService
{
    public Task OnStartAsync(CancellationToken token)
    {
        // start your work here
        return Task.CompletedTask;
    }

    public Task OnStopAsync(CancellationToken token)
    {
        // stop your work here
        return Task.CompletedTask;
    }
}
```

> **Warning:** when implementing `IWinNuxService` directly, you are responsible for safe
> cancellation. The token passed to `OnStartAsync` is controlled by the host and its lifecycle
> varies depending on the host type (plain vs web). Use a linked
> `CancellationTokenSource` owned by your service to avoid shutdown hangs.

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

## Embedded HTTP Server and SignalR

WinNuxService supports embedding an **ASP.NET Core HTTP server** and/or a **SignalR hub** directly
inside the host process — alongside your background services — via the `WithWebHost()` method.

This is useful for exposing health endpoints, REST APIs, or real-time communication without running
a separate web process.

### How it works

`WithWebHost()` accepts two optional delegates:

* `configureBuilder` — runs during the **builder phase**: register ASP.NET Core services such as
  `AddSignalR()`, `AddControllers()`, etc.
* `configureApp` — runs during the **app phase**: map routes, hubs, and middleware with `MapGet()`,
  `MapHub()`, etc.

Your background services registered with `AddService<T>()` continue to run alongside the HTTP layer
in the same process. All existing features — DI, logging, metadata, plugins — work unchanged.

### Health and info endpoints

```csharp
await WinNuxService
    .Create()
    .WithName("MyApiService")
    .WithVersion("2.0.0")
    .WithWebHost(
        configureApp: app =>
        {
            app.MapGet("/health", () => Results.Ok(new { status = "alive" }));
            app.MapGet("/info", (WinNuxServiceInfo info) => Results.Ok(info));
        }
    )
    .AddService<HeartbeatService>()
    .RunAsync();
```

### With SignalR

```csharp
await WinNuxService
    .Create()
    .WithName("MyRealtimeService")
    .WithWebHost(
        configureBuilder: builder =>
        {
            builder.Services.AddSignalR();
        },
        configureApp: app =>
        {
            app.MapHub<NotificationHub>("/notifications");
            app.MapGet("/health", () => "OK");
        }
    )
    .AddService<HeartbeatService>()
    .RunAsync();
```

The hub itself is a standard ASP.NET Core Hub — no WinNuxService-specific code required:

```csharp
public class NotificationHub : Hub
{
    public async Task SendMessage(string message) =>
        await Clients.All.SendAsync("ReceiveMessage", message);
}
```

### Full example with SignalR and background services

```csharp
// Program.cs
await WinNuxService
    .Create()
    .WithName("WinNuxService-WebDemo")
    .WithEnvironment("Demo")
    .WithVersion("1.0.0")
    .ConfigureLogging(logging =>
    {
        logging.AddConsole();
        logging.AddDebug();
    })
    .WithWebHost(
        configureBuilder: builder =>
        {
            builder.Services.AddSignalR();
        },
        configureApp: app =>
        {
            app.MapGet("/health", () => Results.Ok(new { status = "alive" }));
            app.MapGet("/info", (WinNuxServiceInfo info) => Results.Ok(info));
            app.MapHub<NotificationHub>("/notifications");
        }
    )
    .AddService<HeartbeatService>()
    .RunAsync();
```

```csharp
// HeartbeatService.cs
public class HeartbeatService : WinNuxServiceBase
{
    private readonly WinNuxServiceInfo _info;
    private readonly ILogger<HeartbeatService> _logger;

    public HeartbeatService(WinNuxServiceInfo info, ILogger<HeartbeatService> logger)
    {
        _info = info;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            _logger.LogInformation("Heartbeat from {Name} at {Time}", _info.ServiceName, DateTime.Now);
            await Task.Delay(TimeSpan.FromSeconds(5), token);
        }
    }
}
```

### Middleware

When using `WithWebHost()`, the full ASP.NET Core middleware pipeline is available. Middlewares are
registered inside `configureApp` using the standard `UseMiddleware<T>()` or `Use()` API, and must
be added **before** route mappings so they wrap all incoming requests.

**Typed middleware** — implement a class with an `InvokeAsync` method:

```csharp
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _expectedKey;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _expectedKey = config["ApiKey"] ?? throw new InvalidOperationException("ApiKey not configured");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("X-Api-Key", out var key) || key != _expectedKey)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Unauthorized");
            return;
        }

        await _next(context);
    }
}
```

Register it via `UseMiddleware<T>()` in `configureBuilder` / `configureApp`:

```csharp
.WithWebHost(
    configureBuilder: builder =>
    {
        builder.Services.AddSignalR();
    },
    configureApp: app =>
    {
        // Middlewares first — they wrap everything below
        app.UseMiddleware<ApiKeyMiddleware>();

        // Then routes and hubs
        app.MapGet("/health", () => Results.Ok(new { status = "alive" }));
        app.MapHub<NotificationHub>("/notifications");
    }
)
```

**Inline middleware** — for simple, one-off cases:

```csharp
configureApp: app =>
{
    app.Use(async (context, next) =>
    {
        context.Response.Headers["X-Powered-By"] = "WinNuxService";
        await next(context);
    });

    app.MapGet("/health", () => "OK");
}
```

**Fluent shortcut** — `UseMiddleware<T>()` is also available directly on the builder for a cleaner
registration style alongside `AddService<T>()`:

```csharp
await WinNuxService
    .Create()
    .WithName("MyApiService")
    .UseMiddleware<RequestLoggingMiddleware>()   // registered here
    .UseMiddleware<ApiKeyMiddleware>()            // in order
    .WithWebHost(
        configureApp: app =>
        {
            app.MapGet("/health", () => "OK");
            app.MapHub<NotificationHub>("/notifications");
        }
    )
    .AddService<HeartbeatService>()
    .RunAsync();
```

> **Middleware order matters.** The pipeline executes top to bottom. A typical sensible order is:
> exception handling → request logging → authentication → authorization → endpoints.

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
public class HeartbeatService : WinNuxServiceBase
{
    private readonly IMessenger _messenger;

    public HeartbeatService(IMessenger messenger) { _messenger = messenger; }

    protected override async Task ExecuteAsync(CancellationToken token)
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
public class TimeService : WinNuxServiceBase
{
    private readonly IMessenger _messenger;

    public TimeService(IMessenger messenger) { _messenger = messenger; }

    protected override async Task ExecuteAsync(CancellationToken token)
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

WinNuxService detects the runtime environment automatically. The same binary runs as a console
application when launched interactively, and as a native service when started by the OS service
manager — no code change required.

### Deploying as a Windows Service

**1. Publish the application**

```cmd
dotnet publish -c Release -r win-x64 --self-contained true -o C:\Services\MyService
```

**2. Create the Windows Service**

Open a command prompt as Administrator:

```cmd
sc create MyService binPath="C:\Services\MyService\MyService.exe" start=auto
sc description MyService "My WinNuxService background service"
```

**3. Start the service**

```cmd
sc start MyService
```

**4. Manage the service**

```cmd
sc stop MyService       # graceful stop
sc delete MyService     # uninstall
sc query MyService      # check status
```

You can also use the **Services** panel (`services.msc`) or PowerShell:

```powershell
Start-Service MyService
Stop-Service  MyService
Get-Service   MyService
```

> **Logging on Windows:** when running as a Windows Service, console output is suppressed. Use
> `AddEventLog()` to write to the Windows Event Log, or configure a file-based logger such as
> Serilog with a rolling file sink.

---

### Deploying as a Linux systemd Service

**1. Publish the application**

```bash
dotnet publish -c Release -r linux-x64 --self-contained true -o /opt/myservice
chmod +x /opt/myservice/MyService
```

**2. Create the systemd unit file**

Create `/etc/systemd/system/myservice.service`:

```ini
[Unit]
Description=My WinNuxService background service
After=network.target

[Service]
Type=notify
ExecStart=/opt/myservice/MyService
WorkingDirectory=/opt/myservice
Restart=on-failure
RestartSec=5
User=myservice
Group=myservice

# Logging — captured by journald automatically
StandardOutput=journal
StandardError=journal
SyslogIdentifier=myservice

# Environment
Environment=DOTNET_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

> **`Type=notify`** tells systemd to wait for the process to signal readiness before considering
> the service started. WinNuxService emits this signal automatically via `UseSystemd()`.

**3. Create a dedicated user** (recommended)

```bash
useradd -r -s /bin/false myservice
chown -R myservice:myservice /opt/myservice
```

**4. Enable and start the service**

```bash
systemctl daemon-reload
systemctl enable myservice    # start automatically on boot
systemctl start myservice
```

**5. Manage and inspect the service**

```bash
systemctl status myservice    # current status
systemctl stop myservice      # graceful stop
systemctl restart myservice   # stop then start
journalctl -u myservice -f    # follow live logs
journalctl -u myservice --since "1 hour ago"  # recent logs
```

> **Logging on Linux:** `journald` captures everything written to stdout/stderr, so
> `logging.AddConsole()` is sufficient. No file sink required unless you need log rotation outside
> of journald.

---

### macOS (launchd)

On macOS the application runs as a console process or can be registered with launchd using a
`.plist` file. For most use cases, running it directly or via a process supervisor such as
[supervisord](http://supervisord.org) is simpler.

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

**Hybrid Service + API**

Examples:

* background worker exposing a `/health` endpoint
* real-time telemetry pushed over SignalR
* internal REST API alongside scheduled jobs

---

## Requirements

* .NET 10 or later
* Windows / Linux / macOS

---

## License

McpNetwork.WinNuxService is licensed under the MIT License. See [LICENSE](LICENSE) for more information.