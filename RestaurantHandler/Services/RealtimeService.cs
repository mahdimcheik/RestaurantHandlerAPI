using Microsoft.AspNetCore.SignalR;
using RestaurantHandler.Controllers;

namespace RestaurantHandler.Services;

/// <summary>
/// Émission des événements temps réel. Le temps réel notifie, la vérité reste l'API : un client
/// se resynchronise par une lecture REST à chaque (re)connexion.
/// </summary>
public class RealtimeService(IHubContext<RealtimeHub> hub, ILogger<RealtimeService> logger)
{
    /// <summary>Toutes les connexions d'un utilisateur (identifié par le claim NameIdentifier).</summary>
    public Task SendToUserAsync(Guid userId, string type, object payload) =>
        SafeSend(hub.Clients.User(userId.ToString()), type, payload);

    /// <summary>Appareils du restaurant (Managers connectés).</summary>
    public Task SendToRestaurantAsync(string type, object payload) =>
        SafeSend(hub.Clients.Group(RealtimeHub.RestaurantGroup), type, payload);

    public Task SendToAllAsync(string type, object payload) =>
        SafeSend(hub.Clients.All, type, payload);

    private async Task SafeSend(IClientProxy clients, string type, object payload)
    {
        try
        {
            await clients.SendAsync(type, payload);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Échec d'émission temps réel {Type}.", type);
        }
    }
}
