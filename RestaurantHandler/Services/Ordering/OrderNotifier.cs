using RestaurantHandler.Models;

namespace RestaurantHandler.Services;

/// <summary>Événements temps réel des commandes ; la vérité reste l'API (relecture à la reconnexion).</summary>
public class OrderNotifier(RealtimeService realtime)
{
    public const string OrderCreated = "OrderCreated";
    public const string OrderAwaitingApproval = "OrderAwaitingApproval";
    public const string OrderStatusChanged = "OrderStatusChanged";
    public const string OrderUpdated = "OrderUpdated";

    public async Task CreatedAsync(Order order)
    {
        var summary = OrderMapping.ToSummary(order);
        await realtime.SendToRestaurantAsync(
            order.Status == OrderStatus.AwaitingApproval ? OrderAwaitingApproval : OrderCreated,
            summary
        );
    }

    public async Task StatusChangedAsync(Order order)
    {
        var summary = OrderMapping.ToSummary(order);
        await realtime.SendToRestaurantAsync(OrderStatusChanged, summary);
        if (order.CustomerId is { } customerId)
            await realtime.SendToUserAsync(customerId, OrderStatusChanged, summary);
    }

    /// <summary>Paiement déclaré : seul le back-office affiche « payée / à encaisser ».</summary>
    public Task UpdatedAsync(Order order) =>
        realtime.SendToRestaurantAsync(OrderUpdated, OrderMapping.ToSummary(order));
}
