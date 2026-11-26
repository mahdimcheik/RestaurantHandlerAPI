using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Services;

/// <summary>Événements temps réel des commandes ; la vérité reste l'API (relecture à la reconnexion).</summary>
public class OrderNotifier(RealtimeService realtime, NotificationDispatcher notifications)
{
    public const string OrderCreated = "OrderCreated";
    public const string OrderStatusChanged = "OrderStatusChanged";

    public async Task CreatedAsync(Order order)
    {
        var summary = OrderMapping.ToSummary(order);
        await realtime.SendToRestaurantAsync(OrderCreated, summary);
    }

    public async Task StatusChangedAsync(Order order, OrderStatus from, string? reason = null)
    {
        var summary = OrderMapping.ToSummary(order);
        await realtime.SendToRestaurantAsync(OrderStatusChanged, summary);
        await realtime.SendToUserAsync(order.CustomerId, OrderStatusChanged, summary);

        if (CustomerMessage(order, from, reason) is { } message)
        {
            await notifications.NotifyAsync(
                new NotificationRequest(
                    order.CustomerId,
                    order.Customer?.Email,
                    NotificationTypeEnum.Order,
                    message,
                    order.Status == OrderStatus.Ready ? "success" : "info",
                    order.Status == OrderStatus.Ready ? "bell" : "orders",
                    $"/commandes/{order.Id}"
                )
            );
        }
    }

    /// <summary>
    /// Message de suivi pour le client (US-E08-2), ou rien quand la transition ne le concerne
    /// pas : retour arrière du restaurant, commande remise au comptoir.
    /// </summary>
    public static string? CustomerMessage(Order order, OrderStatus from, string? reason)
    {
        var number = $"Commande n° {order.DailyNumber}";
        return order.Status switch
        {
            OrderStatus.Confirmed when from == OrderStatus.PendingPayment =>
                $"{number} confirmée, paiement reçu.",
            OrderStatus.InPreparation when from != OrderStatus.Ready =>
                $"{number} en préparation.",
            OrderStatus.Ready => $"{number} prête, vous pouvez venir la chercher.",
            OrderStatus.Cancelled => string.IsNullOrWhiteSpace(reason)
                ? $"{number} annulée."
                : $"{number} annulée : {reason.Trim()}",
            OrderStatus.NoShow => $"{number} non retirée.",
            _ => null,
        };
    }
}
