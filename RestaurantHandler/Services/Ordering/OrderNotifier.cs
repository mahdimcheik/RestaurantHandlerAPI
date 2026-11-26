using RestaurantHandler.Models;

namespace RestaurantHandler.Services;

/// <summary>Événements temps réel des commandes ; la vérité reste l'API (relecture à la reconnexion).</summary>
public class OrderNotifier(RealtimeService realtime)
{
    public const string OrderCreated = "OrderCreated";
    public const string OrderStatusChanged = "OrderStatusChanged";

    public async Task CreatedAsync(Order order)
    {
        var summary = OrderMapping.ToSummary(order);
        await realtime.SendToRestaurantAsync(OrderCreated, summary);
    }

    public async Task StatusChangedAsync(Order order)
    {
        var summary = OrderMapping.ToSummary(order);
        await realtime.SendToRestaurantAsync(OrderStatusChanged, summary);
        await realtime.SendToUserAsync(order.CustomerId, OrderStatusChanged, summary);
    }
}
