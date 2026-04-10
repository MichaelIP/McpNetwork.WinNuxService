using McpNetwork.WinNuxService;
using McpNetwork.WinNuxService.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sample.WebHost;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.Versioning;

class Program
{
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    static async Task Main(string[] args)
    {

        if (Debugger.IsAttached)
        {
            Console.WriteLine("Running in console mode");
        }

        var builder = WinNuxService
            .Create()

            .WithName("WinNuxService-WebDemo")
            .WithEnvironment("Demo")
            .WithVersion("1.0.0-beta.1")

            .AddProperty("Author", "McpNetwork")
            .AddProperty(string.Format("StartedAt-{0}", DateTimeOffset.Now), DateTimeOffset.Now.ToString("o"))

            .ConfigureLogging(logging =>
            {
                logging.AddConsole();
                logging.AddDebug();
            })

            .UseMiddleware<ProcessingTimeMiddleware>()

            .WithWebHost(
                configureBuilder: builder =>
                {
                    builder.Services.AddSignalR();
                },
                configureApp: app =>
                {
                    app.MapGet("/health", () => Results.Ok(new { status = "alive" }));
                    app.MapGet("/info", (WinNuxServiceInfo info) => Results.Ok(info));
                    app.MapGet("/index", () => Results.Content("<html><head><title>Done</title></head><body><h1>Welcome to WinNuxService Web Demo!</h1></body><html>", "text/html"));
                    app.MapHub<NotificationHub>("/notifications");
                }
            )
            .AddService<HeartbeatService>()

            .Build();

        await builder.RunAsync();
    }
}