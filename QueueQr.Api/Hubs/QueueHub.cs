using Microsoft.AspNetCore.SignalR;

namespace QueueQr.Api.Hubs;

public sealed class QueueHub : Hub
{
    public override Task OnConnectedAsync()
    {
        Console.WriteLine($"[SignalR Hub] Client connected: {Context.ConnectionId}");
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        Console.WriteLine($"[SignalR Hub] Client disconnected: {Context.ConnectionId}");
        return base.OnDisconnectedAsync(exception);
    }

    public Task JoinSite(string siteSlug)
    {
        Console.WriteLine($"[SignalR Hub] {Context.ConnectionId} joining site:{siteSlug}");
        return Groups.AddToGroupAsync(Context.ConnectionId, $"site:{siteSlug}");
    }

    public Task JoinRoom(string siteSlug, string roomSlug)
    {
        Console.WriteLine($"[SignalR Hub] {Context.ConnectionId} joining room:{siteSlug}:{roomSlug}");
        return Groups.AddToGroupAsync(Context.ConnectionId, $"room:{siteSlug}:{roomSlug}");
    }
}
