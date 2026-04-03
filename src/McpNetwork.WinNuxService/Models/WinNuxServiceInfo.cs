namespace McpNetwork.WinNuxService.Models;

public class WinNuxServiceInfo
{
    public string ServiceName { get; init; } = "WinNuxService";
    public string Environment { get; init; } = "Production";
    public string Version { get; init; } = string.Empty;
    public IDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();

}