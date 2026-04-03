using McpNetwork.WinNuxService.Adapters;
using McpNetwork.WinNuxService.Interfaces;
using McpNetwork.WinNuxService.Models;
using McpNetwork.WinNuxService.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
        var builder = Host.CreateDefaultBuilder();

        // Assemble the runtime info
        var info = new WinNuxServiceInfo
        {
            ServiceName = serviceName ?? Assembly.GetEntryAssembly()?.GetName().Name ?? "WinNuxService",
            Environment = environment ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production",
            Version = version ?? "0.0.0",
            Properties = new Dictionary<string, string>(properties)
        };

        if (Debugger.IsAttached)
        {
            builder.UseConsoleLifetime();
        }

        builder.UseWindowsService();
        builder.UseSystemd();

        builder.ConfigureServices((ctx, services) =>
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
        });

        builder.ConfigureLogging(logging =>
        {
            _configureLogging?.Invoke(logging);
        });

        var host = builder.Build();

        return new WinNuxServiceHost(host, info);
    }

    public Task RunAsync()
    {
        return Build().RunAsync();
    }

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

}