using Microsoft.EntityFrameworkCore;
using RestaurantHandler.Contexts;
using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Services;

/// <summary>
/// Transitions de commande (machine à états), réservées au restaurant et au système. Chaque
/// transition est vérifiée par <see cref="OrderTransitionPolicy"/>, historisée et diffusée.
/// </summary>
public class OrderWorkflowService(
    MainContext db,
    OrderNotifier notifier,
    TimeProvider clock
)
{
    // ---------- Manager ----------

    public Task<OrderDetails> StartAsync(Guid orderId, CallerIdentity caller) =>
        TransitionAsync(
            orderId,
            OrderStatus.InPreparation,
            OrderActor.Manager,
            caller.UserId,
            null
        );

    public Task<OrderDetails> ReadyAsync(Guid orderId, CallerIdentity caller) =>
        TransitionAsync(orderId, OrderStatus.Ready, OrderActor.Manager, caller.UserId, null);

    /// <summary>Annule le dernier geste cuisine (≤ 10 s) : En préparation → Confirmée, Prête → En préparation.</summary>
    public async Task<OrderDetails> UndoAsync(Guid orderId, CallerIdentity caller)
    {
        var status =
            await db
                .Orders.Where(o => o.Id == orderId)
                .Select(o => (OrderStatus?)o.Status)
                .FirstOrDefaultAsync()
            ?? throw new ResourceNotFoundException("Commande introuvable.");
        var previous = status switch
        {
            OrderStatus.InPreparation => OrderStatus.Confirmed,
            OrderStatus.Ready => OrderStatus.InPreparation,
            _ => throw new BusinessConflictException(
                "Aucun geste cuisine à annuler sur cette commande."
            ),
        };
        return await TransitionAsync(
            orderId,
            previous,
            OrderActor.Manager,
            caller.UserId,
            "Annulation du geste"
        );
    }

    public Task<OrderDetails> CompleteAsync(Guid orderId, CallerIdentity caller) =>
        TransitionAsync(orderId, OrderStatus.Completed, OrderActor.Manager, caller.UserId, null);

    public Task<OrderDetails> NoShowAsync(Guid orderId, CallerIdentity caller) =>
        TransitionAsync(orderId, OrderStatus.NoShow, OrderActor.Manager, caller.UserId, null);

    public Task<OrderDetails> CancelByManagerAsync(
        Guid orderId,
        string reason,
        CallerIdentity caller
    ) => TransitionAsync(orderId, OrderStatus.Cancelled, OrderActor.Manager, caller.UserId, reason);

    // ---------- Système ----------

    /// <summary>Paiement en ligne confirmé par SumUp (après relecture) : `Confirmée` et payée.</summary>
    public Task<OrderDetails> ConfirmPaidOnlineAsync(Guid orderId) =>
        TransitionAsync(
            orderId,
            OrderStatus.Confirmed,
            OrderActor.System,
            null,
            null,
            apply: o => o.PaidAt = clock.GetUtcNow()
        );

    /// <summary>Aucun paiement réussi dans le délai : annulation automatique (US-E05-2).</summary>
    public Task<OrderDetails> ExpireUnpaidAsync(Guid orderId) =>
        TransitionAsync(
            orderId,
            OrderStatus.Cancelled,
            OrderActor.System,
            null,
            "Paiement en ligne non reçu dans le délai."
        );

    // ---------- Interne ----------

    private async Task<OrderDetails> TransitionAsync(
        Guid orderId,
        OrderStatus to,
        OrderActor actor,
        Guid? authorId,
        string? reason,
        Action<Order>? apply = null
    )
    {
        var order =
            await db
                .Orders.Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new ResourceNotFoundException("Commande introuvable.");

        var now = clock.GetUtcNow();
        var statusSince =
            await db
                .OrderStatusHistory.Where(h => h.OrderId == orderId && h.Status == order.Status)
                .MaxAsync(h => (DateTimeOffset?)h.ChangedAt)
            ?? order.CreatedAt;
        var grace = await db
            .RestaurantSettings.Select(s => s.PickupGraceMinutes)
            .FirstOrDefaultAsync();

        var ctx = new TransitionContext(
            now,
            order.PaidAt is not null,
            order.PickupAt,
            grace,
            statusSince,
            reason
        );
        if (OrderTransitionPolicy.Check(order.Status, to, actor, ctx) is { } error)
            throw new BusinessConflictException(error);

        var from = order.Status;
        order.Status = to;
        order.UpdatedAt = now;
        switch (to)
        {
            case OrderStatus.Confirmed when from != OrderStatus.InPreparation:
                order.ConfirmedAt = now;
                break;
            case OrderStatus.Ready:
                order.ReadyAt = now;
                break;
            case OrderStatus.InPreparation when from == OrderStatus.Ready:
                order.ReadyAt = null;
                break;
            case OrderStatus.Completed:
                order.CompletedAt = now;
                break;
        }

        apply?.Invoke(order);

        db.OrderStatusHistory.Add(
            new OrderStatusHistory
            {
                OrderId = order.Id,
                Status = to,
                ChangedByUserId = authorId,
                ChangedAt = now,
                Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            }
        );

        await SaveAsync();
        await notifier.StatusChangedAsync(order, from, reason);
        return await LoadAsync(orderId);
    }

    private async Task SaveAsync()
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessConflictException(
                "La commande vient d'être modifiée par quelqu'un d'autre : rechargez puis réessayez."
            );
        }
    }

    private async Task<OrderDetails> LoadAsync(Guid id)
    {
        var order = await OrderQueryService
            .WithDetails(db.Orders.AsNoTrackingWithIdentityResolution())
            .FirstAsync(o => o.Id == id);
        return OrderMapping.ToDetails(order, true);
    }
}
