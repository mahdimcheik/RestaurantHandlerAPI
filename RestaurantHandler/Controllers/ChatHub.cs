using RestaurantHandler.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RestaurantHandler.Controllers;

/// <summary>
/// Canal temps réel des notifications : les événements partent du serveur
/// (<see cref="SignalRNotificationsService"/>), le client ne fait que se connecter.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly ConnectionManager _connectionManager;

    public ChatHub(ConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public override async Task OnConnectedAsync()
    {
        var userName =
            Context.User?.FindFirst("Email")?.Value
            ?? Context.User?.FindFirst("Name")?.Value
            ?? Context.User?.Identity?.Name
            ?? "Anonymous";

        _connectionManager.AddConnection(Context.ConnectionId, userName);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _connectionManager.RemoveConnection(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public Task<int> Ping()
    {
        return Task.FromResult(1);
    }
}
