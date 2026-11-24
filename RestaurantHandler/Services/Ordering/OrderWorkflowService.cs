using Hangfire;
using Microsoft.EntityFrameworkCore;
using RestaurantHandler.Contexts;
using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Services;

/// <summary>
/// Transitions de commande (machine à états), encaissements déclarés et non-venues. Chaque
/// transition est vérifiée par <see cref="OrderTransitionPolicy"/>, historisée et diffusée.
/// </summary>
public class OrderWorkflowService(
    MainContext db,
    OrderNotifier notifier,
    IBackgroundJobClient jobs,
    TimeProvider clock
)
{
    // ---------- Client ----------

    /// <summary>US-E04-6 : avant la préparation uniquement. Remboursement en ligne : J3.</summary>
    public Task<OrderDetails> CancelByCustomerAsync(Guid orderId, CallerIdentity caller) =>
        TransitionAsync(
            orderId,
            OrderStatus.Cancelled,
            OrderActor.Customer,
            caller.UserId,
            null,
            ownerId: caller.UserId
        );

    /// <summary>Paiement en ligne abandonné : la commande passe « au retrait » et attend le Manager.</summary>
    public async Task<OrderDetails> SwitchToPayOnSiteAsync(Guid orderId, CallerIdentity caller)
    {
        var details = await TransitionAsync(
            orderId,
            OrderStatus.AwaitingApproval,
            OrderActor.Customer,
            caller.UserId,
            null,
            ownerId: caller.UserId,
            apply: o => o.PaymentChoice = PaymentChoice.OnSite
        );
        var timeout = await db
            .RestaurantSettings.Select(x => x.ApprovalTimeoutMinutes)
            .FirstOrDefaultAsync();
        jobs.Schedule<OrderWorkflowService>(
            x => x.AutoRefuseAsync(orderId),
            TimeSpan.FromMinutes(timeout)
        );
        return details;
    }

    // ---------- Manager ----------

    public Task<OrderDetails> AcceptAsync(Guid orderId, CallerIdentity caller) =>
        TransitionAsync(orderId, OrderStatus.Confirmed, OrderActor.Manager, caller.UserId, null);

    public Task<OrderDetails> RefuseAsync(Guid orderId, string reason, CallerIdentity caller) =>
        TransitionAsync(orderId, OrderStatus.Refused, OrderActor.Manager, caller.UserId, reason);

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

    /// <summary>
    /// Encaissement sur place déclaré (la caisse certifiée tierce fait foi, ADR-0006). Plusieurs
    /// déclarations possibles (paiement mixte) ; la commande est payée quand elles couvrent le total.
    /// </summary>
    public async Task<OrderDetails> DeclarePaymentAsync(
        Guid orderId,
        PaymentDeclare dto,
        CallerIdentity caller
    )
    {
        var order =
            await db.Orders.Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new ResourceNotFoundException("Commande introuvable.");

        if (order.PaymentChoice == PaymentChoice.Online)
            throw new BusinessConflictException("Cette commande se paie en ligne.");
        if (order.Status is OrderStatus.Refused or OrderStatus.Cancelled or OrderStatus.NoShow)
            throw new BusinessConflictException(
                "Cette commande est close : aucun encaissement possible."
            );
        if (order.PaidAt is not null)
            throw new BusinessConflictException("Cette commande est déjà payée.");

        var remaining = order.TotalCents - OrderMapping.PaidCents(order);
        var amount = dto.AmountCents ?? remaining;
        if (amount > remaining)
            throw new BusinessRuleException(
                $"Montant supérieur au reste à payer ({Money.Format(remaining)})."
            );

        var now = clock.GetUtcNow();
        order.Payments.Add(
            new Payment
            {
                Method = dto.Method,
                Status = PaymentStatus.Successful,
                AmountCents = amount,
                DeclaredByUserId = caller.UserId,
                CreatedAt = now,
                CompletedAt = now,
            }
        );
        if (amount == remaining)
            order.PaidAt = now;
        order.UpdatedAt = now;

        await SaveAsync();
        await notifier.UpdatedAsync(order);
        return await LoadAsync(orderId);
    }

    /// <summary>US-E04-5 : le Manager lève le blocage du paiement au retrait.</summary>
    public async Task ResetNoShowsAsync(Guid userId, CallerIdentity caller)
    {
        var user =
            await db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new ResourceNotFoundException("Utilisateur introuvable.");
        db.NoShowResets.Add(
            new NoShowReset
            {
                UserId = user.Id,
                PreviousCount = user.NoShowCount,
                ResetByUserId = caller.UserId,
                ResetAt = clock.GetUtcNow(),
            }
        );
        user.NoShowCount = 0;
        await db.SaveChangesAsync();
    }

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

    /// <summary>Tâche Hangfire : refuse une commande restée « À valider » au-delà du délai (US-E06-3).</summary>
    public async Task AutoRefuseAsync(Guid orderId)
    {
        var status = await db
            .Orders.Where(o => o.Id == orderId)
            .Select(o => (OrderStatus?)o.Status)
            .FirstOrDefaultAsync();
        if (status != OrderStatus.AwaitingApproval)
            return;

        try
        {
            await TransitionAsync(
                orderId,
                OrderStatus.Refused,
                OrderActor.System,
                null,
                "Greenadine n'a pas pu valider votre commande à temps."
            );
        }
        catch (BusinessConflictException)
        {
            // Un Manager a statué entre-temps.
        }
    }

    // ---------- Interne ----------

    private async Task<OrderDetails> TransitionAsync(
        Guid orderId,
        OrderStatus to,
        OrderActor actor,
        Guid? authorId,
        string? reason,
        Guid? ownerId = null,
        Action<Order>? apply = null
    )
    {
        var order =
            await db
                .Orders.Include(o => o.Customer)
                .FirstOrDefaultAsync(o =>
                    o.Id == orderId && (ownerId == null || o.CustomerId == ownerId)
                )
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
            case OrderStatus.NoShow when order.Customer is not null:
                order.Customer.NoShowCount++;
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
        await notifier.StatusChangedAsync(order);
        return await LoadAsync(orderId, actor != OrderActor.Customer);
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

    private async Task<OrderDetails> LoadAsync(Guid id, bool forManager = true)
    {
        var order = await OrderQueryService
            .WithDetails(db.Orders.AsNoTrackingWithIdentityResolution())
            .FirstAsync(o => o.Id == id);
        return OrderMapping.ToDetails(order, forManager);
    }
}
