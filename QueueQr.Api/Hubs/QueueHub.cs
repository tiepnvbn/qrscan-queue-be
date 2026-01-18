using Microsoft.AspNetCore.SignalR;

namespace QueueQr.Api.Hubs;

public sealed class QueueHub : Hub
{
    public Task JoinSite(string siteSlug)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"site:{siteSlug}");

    public Task JoinRoom(string siteSlug, string roomSlug)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"room:{siteSlug}:{roomSlug}");
}
