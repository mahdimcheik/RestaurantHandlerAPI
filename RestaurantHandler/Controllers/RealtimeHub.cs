using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Controllers;

/// <summary>
/// Canal temps réel unique (<c>/hubs/realtime</c>). Le client ne fait que se connecter : les
/// groupes sont attribués par le serveur à partir de l'identité, jamais demandés par le client.
/// Les événements partent de <see cref="Services.RealtimeService"/>.
/// </summary>
[Authorize]
public class RealtimeHub : Hub
{
    public const string Path = "/hubs/realtime";

    /// <summary>Tous les appareils du restaurant (KDS, back-office) : rôle Manager.</summary>
    public const string RestaurantGroup = "restaurant";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole(nameof(RoleEnum.Manager)) == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RestaurantGroup);
        }

        await base.OnConnectedAsync();
    }

    public Task<int> Ping() => Task.FromResult(1);
}
