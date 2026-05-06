using Microsoft.AspNetCore.SignalR;

namespace NotamWatcher.Api.Hubs;

/// <summary>
/// SignalR hub for real-time NOTAM delivery.
///
/// Group model: each watched route gets its own SignalR group keyed by RouteKey
/// (e.g. "KJFK-KLAX-KORD"). When the background fetcher finds new/updated NOTAMs
/// for an airport, it resolves which route groups contain that ICAO code and sends
/// only to those groups — no fan-out to uninterested clients.
///
/// Auth stub: clients must pass a non-empty X-Hub-Token header. In production this
/// would validate a JWT; here it gates against accidental open connections.
/// </summary>
public sealed class NotamHub : Hub
{
    private const string TokenHeader = "X-Hub-Token";

    public override async Task OnConnectedAsync()
    {
        var token = Context.GetHttpContext()?.Request.Headers[TokenHeader].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(token))
        {
            Context.Abort();
            return;
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Client calls this after connecting to subscribe to a specific route's NOTAM stream.
    /// The route key is the sorted, dash-joined ICAO codes (normalized server-side).
    /// </summary>
    public Task SubscribeToRoute(string routeKey) =>
        Groups.AddToGroupAsync(Context.ConnectionId, routeKey);

    /// <summary>Removes the client from a route group.</summary>
    public Task UnsubscribeFromRoute(string routeKey) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, routeKey);
}
