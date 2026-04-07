namespace McpNetwork.WinNuxService.Models;

public sealed class WebHostOptions
{
    public Action<WebApplicationBuilder>? ConfigureBuilder { get; set; }
    public Action<WebApplication>? ConfigureApp { get; set; }
}