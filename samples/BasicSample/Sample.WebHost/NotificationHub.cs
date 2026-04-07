using Microsoft.AspNetCore.SignalR;

namespace Sample.WebHost;

public class NotificationHub : Hub
{
    public async Task SendMessage(string message) => await Clients.All.SendAsync("ReceiveMessage", message);
}