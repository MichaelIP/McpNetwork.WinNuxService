using McpNetwork.WinNuxService.Adapters;
using McpNetwork.WinNuxService.Interfaces;
using McpNetwork.WinNuxService.Models;
using McpNetwork.WinNuxService.Plugins;
using System.Diagnostics;
using System.Reflection;

namespace McpNetwork.WinNuxService;

public class WinNuxServiceBuilder
{
    private readonly List<Type> _workers = new();
    private readonly List<Type> _plugins = new();
    private readonly List<Assembly> _pluginAssemblies = new();
    private readonly List<PluginLoadContext> _pluginContexts = new();

    private Action<ILoggingBuilder>? _configureLogging;
    private Action<HostBuilderContext, IServiceCollection>? _configureServices;

    private WebHostOptions? _webHostOptions;
    private string version = string.Empty;
    private string serviceName = string.Empty;
    private string environment = string.Empty;
    private readonly Dictionary<string, string> properties = new();

    public WinNuxServiceBuilder WithName(string name)
    {
        this.serviceName = name;
        return this;
    }

    public WinNuxServiceBuilder WithEnvironment(string environment)
    {
        this.environment = environment;
        return this;
    }

    public WinNuxServiceBuilder WithVersion(string version)
    {
        this.version = version;
        return this;
    }

    /// <summary>
    /// Enables an embedded HTTP/SignalR server inside the host.
    /// </summary>
    /// <param name="configureBuilder">Register ASP.NET Core services (AddSignalR, AddControllers…)</param>
    /// <param name="configureApp">Map routes, hubs, middleware (MapHub, MapGet…)</param>
    public WinNuxServiceBuilder WithWebHost(
        Action<WebApplicationBuilder>? configureBuilder = null,
        Action<WebApplication>? configureApp = null)
    {
        _webHostOptions = new WebHostOptions
        {
            ConfigureBuilder = configureBuilder,
            ConfigureApp = configureApp
        };
        return this;
    }

    public WinNuxServiceBuilder AddProperty(string key, string value)
    {
        properties[key] = value;
        return this;
    }

    public WinNuxServiceBuilder AddService<T>()
        where T : class, IWinNuxService
    {
        _workers.Add(typeof(T));
        return this;
    }

    public WinNuxServiceBuilder LoadPlugin<T>()
        where T : IWinNuxPlugin
    {
        _plugins.Add(typeof(T));
        return this;
    }

    public WinNuxServiceBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configure)
    {
        _configureServices += configure;
        return this;
    }

    public WinNuxServiceBuilder ConfigureLogging(Action<ILoggingBuilder> configure)
    {
        _configureLogging += configure;
        return this;
    }

    public WinNuxServiceBuilder LoadExternalPlugin(string pluginDllPath)
    {
        var fullPath = Path.GetFullPath(pluginDllPath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Plugin not found: {fullPath}");

        var loadContext = new PluginLoadContext(fullPath);
        _pluginContexts.Add(loadContext);

        var assembly = loadContext.LoadFromAssemblyPath(fullPath);
        _pluginAssemblies.Add(assembly);

        return this;
    }

    public WinNuxServiceHost Build()
    {
        if (_webHostOptions is not null)
            return BuildWithWeb();

        return BuildPlain();   // existing path, untouched
    }

    public Task RunAsync()
    {
        return Build().RunAsync();
    }

    //private WinNuxServiceHost BuildPlain()
    //{
    //    var builder = Host.CreateDefaultBuilder();

    //    var info = BuildServiceMetadata();

    //    if (Debugger.IsAttached)
    //    {
    //        builder.UseConsoleLifetime();
    //    }

    //    builder.UseWindowsService();
    //    builder.UseSystemd();

    //    builder.ConfigureServices((ctx, services) =>
    //    {
    //        services.AddSingleton<PluginManager>();
    //        services.AddSingleton<IPluginManager>(sp => sp.GetRequiredService<PluginManager>());
    //        services.AddSingleton(info);

    //        // Register workers
    //        foreach (var worker in _workers)
    //        {
    //            services.AddSingleton(worker);

    //            var adapterType = typeof(ServiceAdapter<>).MakeGenericType(worker);
    //            services.AddSingleton(typeof(IHostedService), adapterType);
    //        }

    //        // Load plugins
    //        foreach (var pluginType in _plugins)
    //        {
    //            var plugin = (IWinNuxPlugin)Activator.CreateInstance(pluginType)!;
    //            plugin.ConfigureServices(ctx, services);
    //        }

    //        // External plugin assemblies
    //        foreach (var assembly in _pluginAssemblies)
    //        {
    //            LoadPluginsFromAssembly(assembly, ctx, services);
    //        }

    //        _configureServices?.Invoke(ctx, services);
    //    });

    //    builder.ConfigureLogging(logging =>
    //    {
    //        _configureLogging?.Invoke(logging);
    //    });

    //    var host = builder.Build();

    //    return new WinNuxServiceHost(host, info);
    //}

    //private WinNuxServiceHost BuildWithWeb()
    //{
    //    // 1. Create a WebApplicationBuilder instead of the generic host builder
    //    var builder = WebApplication.CreateBuilder();

    //    // 2. Forward your existing service metadata into DI
    //    //var info = new WinNuxServiceInfo(_name, _environment, _version, _properties);
    //    var info = BuildServiceMetadata();

    //    builder.Services.AddSingleton(info);

    //    // 3. Register all IWinNuxService workers as hosted services
    //    foreach (var type in _workers)
    //        builder.Services.AddSingleton(typeof(IWinNuxService), type);

    //    builder.Services.AddHostedService<WinNuxHostedServiceRunner>();

    //    // 4. Apply caller-supplied DI registrations
    //    _configureServices?.Invoke(new HostBuilderContext(new Dictionary<object, object>()), builder.Services);

    //    // 5. Apply caller-supplied logging
    //    if (_configureLogging is not null)
    //        builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
    //    // (or just expose builder.Logging directly — see note below)

    //    // 6. Let the caller configure ASP.NET Core services (SignalR, controllers…)
    //    _webHostOptions!.ConfigureBuilder?.Invoke(builder);

    //    // 7. Build the WebApplication
    //    var app = builder.Build();

    //    // 8. Let the caller map routes, hubs, middleware
    //    _webHostOptions.ConfigureApp?.Invoke(app);

    //    // 9. Wrap in your existing host abstraction
    //    return new WinNuxServiceHost(app, info);
    //}

    private static void LoadPluginsFromAssembly(Assembly assembly, HostBuilderContext ctx, IServiceCollection services)
    {
        var pluginTypes = assembly
            .GetTypes()
            .Where(t =>
                typeof(IWinNuxPlugin).IsAssignableFrom(t) &&
                !t.IsAbstract &&
                !t.IsInterface);

        foreach (var type in pluginTypes)
        {
            var plugin = (IWinNuxPlugin)Activator.CreateInstance(type)!;

            plugin.ConfigureServices(ctx, services);
        }
    }

    private WinNuxServiceInfo BuildServiceMetadata()
    {
        // Assemble the runtime info
        return new WinNuxServiceInfo
        {
            ServiceName = !string.IsNullOrEmpty(serviceName)
                ? serviceName
                : Assembly.GetEntryAssembly()?.GetName().Name ?? "WinNuxService",
            Environment = !string.IsNullOrEmpty(environment)
                ? environment
                : System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production",
            Version = !string.IsNullOrEmpty(version) ? version : "0.0.0",
            Properties = new Dictionary<string, string>(properties)
        };
    }

    // ------------------------------------------------------------------ //
    //  SHARED helpers — single source of truth                           //
    // ------------------------------------------------------------------ //

    private void ConfigureCommonServices(HostBuilderContext ctx, IServiceCollection services, WinNuxServiceInfo info)
    {
        services.AddSingleton<PluginManager>();
        services.AddSingleton<IPluginManager>(sp => sp.GetRequiredService<PluginManager>());
        services.AddSingleton(info);

        // Register workers
        foreach (var worker in _workers)
        {
            services.AddSingleton(worker);
            var adapterType = typeof(ServiceAdapter<>).MakeGenericType(worker);
            services.AddSingleton(typeof(IHostedService), adapterType);
        }

        // Load plugins
        foreach (var pluginType in _plugins)
        {
            var plugin = (IWinNuxPlugin)Activator.CreateInstance(pluginType)!;
            plugin.ConfigureServices(ctx, services);
        }

        // External plugin assemblies
        foreach (var assembly in _pluginAssemblies)
        {
            LoadPluginsFromAssembly(assembly, ctx, services);
        }

        _configureServices?.Invoke(ctx, services);
    }

    private void ConfigureCommonLogging(ILoggingBuilder logging)
    {
        _configureLogging?.Invoke(logging);
    }

    // ------------------------------------------------------------------ //
    //  BuildPlain — unchanged behavior, now delegates to helpers          //
    // ------------------------------------------------------------------ //

    private WinNuxServiceHost BuildPlain()
    {
        var builder = Host.CreateDefaultBuilder();
        var info = BuildServiceMetadata();

        if (Debugger.IsAttached)
            builder.UseConsoleLifetime();

        builder.UseWindowsService();
        builder.UseSystemd();

        builder.ConfigureServices((ctx, services) => ConfigureCommonServices(ctx, services, info));
        builder.ConfigureLogging(ConfigureCommonLogging);

        var host = builder.Build();
        return new WinNuxServiceHost(host, info);
    }

    // ------------------------------------------------------------------ //
    //  BuildWithWeb — same foundation + ASP.NET Core layer on top         //
    // ------------------------------------------------------------------ //

    private WinNuxServiceHost BuildWithWeb()
    {
        var builder = WebApplication.CreateBuilder();
        var info = BuildServiceMetadata();

        // Same lifecycle behavior as BuildPlain
        if (Debugger.IsAttached)
            builder.Host.UseConsoleLifetime();

        builder.Host.UseWindowsService();
        builder.Host.UseSystemd();

        // Same core services — plugins, workers, metadata
        builder.Host.ConfigureServices((ctx, services) => ConfigureCommonServices(ctx, services, info));
        builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
        builder.Host.ConfigureLogging(ConfigureCommonLogging);

        // ASP.NET Core specific — what BuildPlain never had
        _webHostOptions!.ConfigureBuilder?.Invoke(builder);

        var app = builder.Build();

        _webHostOptions.ConfigureApp?.Invoke(app);

        return new WinNuxServiceHost(app, info);
    }

}