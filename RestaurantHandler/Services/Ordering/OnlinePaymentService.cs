using Microsoft.EntityFrameworkCore;
using RestaurantHandler.Contexts;
using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Integrations.SumUp;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Services;

/// <summary>
/// Paiement en ligne SumUp (ADR-0007, E05). La vérité vient toujours d'une relecture du checkout
/// chez SumUp : la notification ne fait que déclencher cette relecture, le retour navigateur
/// n'a aucun effet. Chaque relecture est sérialisée par commande et idempotente.
/// </summary>
public class OnlinePaymentService(
    MainContext db,
    SumUpClient sumup,
    OrderWorkflowService workflow,
    OrderPlacementService placement,
    RealtimeService realtime,
    TimeProvider clock,
    ILogger<OnlinePaymentService> logger
)
{
    public const string NotificationPath = "/payments/sumup/notifications";
    public const string PaymentAnomaly = "PaymentAnomaly";

    // ---------- Client ----------

    /// <summary>
    /// Nouvelle tentative de paiement : les tentatives en cours sont relues puis désactivées, un
    /// checkout est créé du montant calculé par le serveur. Renvoie la commande avec l'URL de paiement.
    /// </summary>
    public async Task<OrderDetails> StartCheckoutAsync(Guid orderId, CallerIdentity caller)
    {
        if (!EnvironmentVariables.SumUpEnabled)
            throw new BusinessConflictException("Le paiement en ligne est indisponible : choisissez le paiement au retrait.");

        var order = await OwnedAsync(orderId, caller);
        await CloseOpenAttemptsAsync(orderId);

        order = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == orderId);
        if (order.Status != OrderStatus.PendingPayment || order.PaymentChoice != PaymentChoice.Online)
            throw new BusinessConflictException("Cette commande n'attend pas de paiement en ligne.");

        var settings = await db.RestaurantSettings.AsNoTracking().FirstAsync();
        var validUntil = order.CreatedAt.AddMinutes(settings.OnlinePaymentExpiryMinutes);
        if (validUntil <= clock.GetUtcNow().AddMinutes(1))
            throw new BusinessConflictException("Le délai de paiement de cette commande est dépassé.");

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Method = PaymentMethod.SumupOnline,
            Status = PaymentStatus.Pending,
            AmountCents = order.TotalCents,
            CreatedAt = clock.GetUtcNow(),
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        try
        {
            var checkout = await sumup.CreateCheckoutAsync(
                payment.Id.ToString("N"),
                order.TotalCents,
                $"Commande n° {order.DailyNumber}",
                Url(EnvironmentVariables.SUMUP_NOTIFICATION_BASE_URL, NotificationPath),
                Url(EnvironmentVariables.API_FRONT_URL, $"/commandes/{order.Id}"),
                validUntil
            );
            payment.SumupCheckoutId = checkout.Id;
            payment.CheckoutUrl = checkout.HostedCheckoutUrl;
        }
        catch (Exception ex) when (ex is SumUpException or HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Création du checkout SumUp impossible pour la commande {OrderId}.", orderId);
            payment.Status = PaymentStatus.Failed;
            await db.SaveChangesAsync();
            throw new BusinessConflictException("Le paiement en ligne est momentanément indisponible : réessayez ou choisissez le paiement au retrait.");
        }

        await db.SaveChangesAsync();
        return await DetailsAsync(orderId, false);
    }

    /// <summary>US-E04-6 : les tentatives ouvertes sont closes, puis tout paiement en ligne reçu est remboursé.</summary>
    public async Task<OrderDetails> CancelByCustomerAsync(Guid orderId, CallerIdentity caller)
    {
        await OwnedAsync(orderId, caller);
        await CloseOpenAttemptsAsync(orderId);
        await workflow.CancelByCustomerAsync(orderId, caller);
        await RefundOnlinePaymentsAsync(orderId);
        return await DetailsAsync(orderId, false);
    }

    /// <summary>Paiement en ligne abandonné → paiement au retrait (limites d'US-E04-5).</summary>
    public async Task<OrderDetails> SwitchToPayOnSiteAsync(Guid orderId, CallerIdentity caller)
    {
        await OwnedAsync(orderId, caller);
        await placement.EnsurePayOnSiteAllowedAsync(caller.UserId);
        await CloseOpenAttemptsAsync(orderId);
        return await workflow.SwitchToPayOnSiteAsync(orderId, caller);
    }

    // ---------- Manager ----------

    /// <summary>US-E06-6 : annulation avec motif, remboursement automatique d'un paiement en ligne.</summary>
    public async Task<OrderDetails> CancelByManagerAsync(Guid orderId, string reason, CallerIdentity caller)
    {
        await CloseOpenAttemptsAsync(orderId);
        await workflow.CancelByManagerAsync(orderId, reason, caller);
        await RefundOnlinePaymentsAsync(orderId);
        return await DetailsAsync(orderId, true);
    }

    /// <summary>
    /// US-E05-4 : remboursement total décidé par le Manager (non-venue, remboursement précédent en
    /// échec…), seulement sur une commande close sans remise.
    /// </summary>
    public async Task<OrderDetails> RefundAsync(Guid orderId)
    {
        var status = await db.Orders.Where(o => o.Id == orderId).Select(o => (OrderStatus?)o.Status).FirstOrDefaultAsync()
            ?? throw new ResourceNotFoundException("Commande introuvable.");
        if (status is not (OrderStatus.Cancelled or OrderStatus.Refused or OrderStatus.NoShow))
            throw new BusinessConflictException("Seule une commande annulée, refusée ou non retirée peut être remboursée.");

        if (await RefundOnlinePaymentsAsync(orderId) == 0)
            throw new BusinessConflictException("Aucun paiement en ligne à rembourser sur cette commande.");
        return await DetailsAsync(orderId, true);
    }

    // ---------- SumUp ----------

    /// <summary>Notification SumUp : identifiant de checkout inconnu ignoré, sinon relecture.</summary>
    public async Task HandleNotificationAsync(string checkoutId)
    {
        var paymentId = await db.Payments
            .Where(p => p.SumupCheckoutId == checkoutId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync();
        if (paymentId is null)
        {
            logger.LogWarning("Notification SumUp pour un checkout inconnu {CheckoutId}.", checkoutId);
            return;
        }

        await ReconcileAsync(paymentId.Value);
    }

    /// <summary>
    /// Tâche Hangfire récurrente : relit les tentatives en attente (notification perdue) et annule
    /// les commandes non payées dans le délai.
    /// </summary>
    public async Task ReconcileAllAsync()
    {
        var now = clock.GetUtcNow();
        var pending = await db.Payments
            .Where(p => p.Status == PaymentStatus.Pending && p.SumupCheckoutId != null && p.CreatedAt < now.AddMinutes(-1))
            .Select(p => p.Id)
            .ToListAsync();
        foreach (var id in pending)
            await Guarded(() => ReconcileAsync(id), $"réconciliation du paiement {id}");

        var expiry = await db.RestaurantSettings.Select(s => s.OnlinePaymentExpiryMinutes).FirstOrDefaultAsync();
        var expired = await db.Orders
            // Une anomalie (argent encaissé, montant faux) attend la décision du Manager.
            .Where(o => o.Status == OrderStatus.PendingPayment && o.PaymentIssue == null && o.CreatedAt < now.AddMinutes(-expiry))
            .Select(o => o.Id)
            .ToListAsync();
        foreach (var id in expired)
            await Guarded(() => ExpireAsync(id), $"expiration de la commande {id}");
    }

    /// <summary>Relit le checkout d'une tentative et applique <see cref="OnlinePaymentPolicy"/>.</summary>
    public async Task ReconcileAsync(Guid paymentId)
    {
        var orderId = await db.Payments.Where(p => p.Id == paymentId).Select(p => p.OrderId).FirstAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await LockOrderAsync(orderId);

        var payment = await db.Payments.FirstAsync(p => p.Id == paymentId);
        if (payment.Status != PaymentStatus.Pending || payment.SumupCheckoutId is null)
            return;

        var checkout = await sumup.GetCheckoutAsync(payment.SumupCheckoutId);
        var paid = checkout.Transactions.FirstOrDefault(t => t.Status == SumUpClient.TransactionSuccessful);
        var order = await db.Orders.FirstAsync(o => o.Id == orderId);
        var outcome = OnlinePaymentPolicy.Decide(
            payment.Status,
            order.Status,
            order.TotalCents,
            new CheckoutState(checkout.Status, paid?.AmountCents, paid?.Currency)
        );

        var now = clock.GetUtcNow();
        if (outcome is PaymentOutcome.Confirm or PaymentOutcome.Anomaly or PaymentOutcome.RefundUnexpected)
        {
            payment.Status = PaymentStatus.Successful;
            payment.AmountCents = paid!.AmountCents;
            payment.Currency = paid.Currency;
            payment.SumupTransactionId = paid.Id;
            payment.SumupTransactionCode = paid.TransactionCode;
            payment.CompletedAt = now;
        }

        switch (outcome)
        {
            case PaymentOutcome.None:
                return;

            case PaymentOutcome.AttemptFailed:
                payment.Status = checkout.Status == SumUpClient.StatusFailed ? PaymentStatus.Failed : PaymentStatus.Cancelled;
                await db.SaveChangesAsync();
                break;

            case PaymentOutcome.Confirm:
                await db.SaveChangesAsync();
                await workflow.ConfirmPaidOnlineAsync(orderId);
                break;

            case PaymentOutcome.Anomaly:
                order.PaymentIssue = $"SumUp a encaissé {Money.Format(paid!.AmountCents, paid.Currency)} pour un total de {Money.Format(order.TotalCents)}.";
                await db.SaveChangesAsync();
                await AlertAsync(order);
                break;

            case PaymentOutcome.RefundUnexpected:
                await db.SaveChangesAsync();
                await RefundPaymentAsync(payment, order);
                break;
        }

        await transaction.CommitAsync();
    }

    // ---------- Interne ----------

    /// <summary>
    /// Clôt les tentatives encore en attente : relecture (un paiement a pu aboutir), puis
    /// désactivation ; un checkout déjà traité est relu une seconde fois.
    /// </summary>
    private async Task CloseOpenAttemptsAsync(Guid orderId)
    {
        var open = await db.Payments
            .Where(p => p.OrderId == orderId && p.Status == PaymentStatus.Pending && p.SumupCheckoutId != null)
            .Select(p => new { p.Id, p.SumupCheckoutId })
            .ToListAsync();

        foreach (var attempt in open)
        {
            await ReconcileAsync(attempt.Id);
            if (await db.Payments.AnyAsync(p => p.Id == attempt.Id && p.Status == PaymentStatus.Pending))
            {
                if (!await sumup.DeactivateCheckoutAsync(attempt.SumupCheckoutId!))
                {
                    await ReconcileAsync(attempt.Id);
                    continue;
                }

                var payment = await db.Payments.FirstAsync(p => p.Id == attempt.Id);
                payment.Status = PaymentStatus.Cancelled;
                await db.SaveChangesAsync();
            }
        }

        db.ChangeTracker.Clear();
    }

    private async Task ExpireAsync(Guid orderId)
    {
        await CloseOpenAttemptsAsync(orderId);
        var status = await db.Orders.Where(o => o.Id == orderId).Select(o => o.Status).FirstAsync();
        if (status == OrderStatus.PendingPayment)
            await workflow.ExpireUnpaidAsync(orderId);
    }

    /// <summary>Rembourse chaque paiement en ligne réussi non remboursé ; renvoie le nombre traité.</summary>
    private async Task<int> RefundOnlinePaymentsAsync(Guid orderId)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await LockOrderAsync(orderId);

        var order = await db.Orders.FirstAsync(o => o.Id == orderId);
        var payments = await db.Payments
            .Where(p => p.OrderId == orderId && p.Method == PaymentMethod.SumupOnline && p.Status == PaymentStatus.Successful)
            .ToListAsync();
        foreach (var payment in payments)
            await RefundPaymentAsync(payment, order);

        await transaction.CommitAsync();
        return payments.Count;
    }

    private async Task RefundPaymentAsync(Payment payment, Order order)
    {
        try
        {
            await sumup.RefundAsync(payment.SumupTransactionId ?? throw new SumUpException("transaction inconnue"));
            payment.Status = PaymentStatus.Refunded;
            payment.RefundedAt = clock.GetUtcNow();
            await db.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is SumUpException or HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Remboursement SumUp en échec pour le paiement {PaymentId}.", payment.Id);
            order.PaymentIssue = $"Remboursement de {Money.Format(payment.AmountCents)} en échec : à relancer.";
            await db.SaveChangesAsync();
            await AlertAsync(order);
        }
    }

    private Task AlertAsync(Order order) =>
        realtime.SendToRestaurantAsync(PaymentAnomaly, new { orderId = order.Id, order.DailyNumber, issue = order.PaymentIssue });

    /// <summary>Sérialise relectures, remboursements et annulations d'une même commande.</summary>
    private Task LockOrderAsync(Guid orderId) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({orderId.ToString()}))");

    private async Task<Order> OwnedAsync(Guid orderId, CallerIdentity caller) =>
        await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == caller.UserId)
        ?? throw new ResourceNotFoundException("Commande introuvable.");

    private async Task<OrderDetails> DetailsAsync(Guid orderId, bool forManager)
    {
        db.ChangeTracker.Clear();
        var order = await OrderQueryService.WithDetails(db.Orders.AsNoTrackingWithIdentityResolution()).FirstAsync(o => o.Id == orderId);
        return OrderMapping.ToDetails(order, forManager);
    }

    private async Task Guarded(Func<Task> work, string what)
    {
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Échec de la {What}.", what);
            db.ChangeTracker.Clear();
        }
    }

    private static string? Url(string? root, string path) =>
        string.IsNullOrWhiteSpace(root) ? null : root.TrimEnd('/') + path;
}
