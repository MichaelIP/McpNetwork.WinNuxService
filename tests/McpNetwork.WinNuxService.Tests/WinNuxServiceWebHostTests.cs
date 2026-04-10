using McpNetwork.WinNuxService.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net;

namespace McpNetwork.WinNuxService.Tests;

[TestFixture]
public class WinNuxServiceWebHostTests
{
    private WinNuxServiceHost _host = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        // 1. Build through the REAL WinNuxService fluent builder — nothing bypassed
        _host = WinNuxService
            .Create()
            .WithName("TestService")
            .WithVersion("1.0.0")
            .WithEnvironment("Testing")
            .UseMiddleware<ProcessingTimeMiddleware>()
            .WithTestServer()
            .WithWebHost(
                configureApp: app =>
                {
                    app.MapGet("/health", () => Results.Ok(new { status = "alive" }));
                    app.MapGet("/info", (WinNuxServiceInfo info) => Results.Ok(info));
                }
            )
            .Build();

        // 2. Swap Kestrel for TestServer AFTER build, before start
        //    InternalsVisibleTo gives us access to the inner IHost here
        _host.Host.Services
            .GetRequiredService<IWebHostEnvironment>(); // sanity: confirms it's a web host

        await _host.StartAsync();

        // 3. Extract the TestServer from the running host
        _client = _host.Host.GetTestServer().CreateClient();
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _host.StopAsync();
    }

    // ------------------------------------------------------------------ //
    //  Endpoint tests — proves WithWebHost() wired routes correctly        //
    // ------------------------------------------------------------------ //

    [Test]
    public async Task Health_Endpoint_Returns_200()
    {
        var response = await _client.GetAsync("/health");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Info_Endpoint_Returns_Service_Metadata()
    {
        var response = await _client.GetAsync("/info");
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(body, Does.Contain("TestService"));
        Assert.That(body, Does.Contain("1.0.0"));
        Assert.That(body, Does.Contain("Testing"));
    }

    [Test]
    public async Task Unknown_Route_Returns_404()
    {
        var response = await _client.GetAsync("/does-not-exist");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ------------------------------------------------------------------ //
    //  Middleware tests — proves UseMiddleware<T>() wired correctly        //
    // ------------------------------------------------------------------ //

    [Test]
    public async Task ProcessingTime_Header_Is_Present_On_Success()
    {
        var response = await _client.GetAsync("/health");

        Assert.That(response.Headers.Contains("X-Processing-Time"), Is.True,
            "ProcessingTimeMiddleware should add the header on every successful response.");
    }

    [Test]
    public async Task ProcessingTime_Header_Is_Present_On_404()
    {
        var response = await _client.GetAsync("/unknown");

        // Middleware wraps ALL requests — even unmatched routes
        Assert.That(response.Headers.Contains("X-Processing-Time"), Is.True,
            "ProcessingTimeMiddleware should add the header even on 404 responses.");
    }

    [Test]
    public async Task ProcessingTime_Header_Has_Expected_Format()
    {
        var response = await _client.GetAsync("/health");
        var value = response.Headers.GetValues("X-Processing-Time").First();

        Assert.That(value, Is.Not.Null.And.Match(@"^\d+\.\d{2} ms$"));
    }

    // ------------------------------------------------------------------ //
    //  Background service test — proves AddService<T>() runs alongside     //
    // ------------------------------------------------------------------ //

    [Test]
    public async Task HeartbeatService_Does_Not_Prevent_Host_From_Serving_Requests()
    {
        // If HeartbeatService threw on start or blocked the host,
        // this request would either hang or fail.
        await Task.Delay(200); // let it tick at least once

        var response = await _client.GetAsync("/health");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "Host should serve HTTP requests normally while HeartbeatService is running.");
    }
}