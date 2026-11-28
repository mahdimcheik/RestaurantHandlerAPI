using Microsoft.EntityFrameworkCore;
using RestaurantHandler.Contexts;
using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Services;

/// <summary>
/// Lectures de commandes. Un Client ne voit que les siennes : toute autre lecture renvoie 404,
/// pour ne pas révéler l'existence de la commande.
/// </summary>
public class OrderQueryService(MainContext db, TimeProvider clock)
{
    public static IQueryable<Order> WithDetails(IQueryable<Order> query) =>
        query
            .Include(o => o.Customer)
            .Include(o => o.Items).ThenInclude(i => i.Modifications)
            .Include(o => o.Items).ThenInclude(i => i.MenuSlot)
            .Include(o => o.Payments)
            .Include(o => o.StatusHistory).ThenInclude(h => h.ChangedByUser);

    public async Task<OrderDetails> GetAsync(Guid id, CallerIdentity caller)
    {
        var order = await WithDetails(db.Orders.AsNoTrackingWithIdentityResolution())
            .FirstOrDefaultAsync(o => o.Id == id && (caller.IsManager || o.CustomerId == caller.UserId))
            ?? throw new ResourceNotFoundException("Commande introuvable.");
        return OrderMapping.ToDetails(order, caller.IsManager);
    }

    /// <summary>Justificatif d'une commande payée, au propriétaire ou à un Manager.</summary>
    public async Task<OrderReceipt> ReceiptAsync(Guid id, CallerIdentity caller)
    {
        var order = await GetAsync(id, caller);
        if (order.PaidAt is not { } paidAt)
            throw new BusinessConflictException("Cette commande n'est pas payée : pas de justificatif.");

        var settings = await db.RestaurantSettings.AsNoTracking().FirstAsync();
        return new OrderReceipt
        {
            TradeName = settings.TradeName,
            LegalName = settings.LegalName,
            Address = settings.Address,
            Siret = settings.Siret,
            VatNumber = settings.VatNumber,
            OrderNumber = order.OrderNumber,
            DailyNumber = order.DailyNumber,
            PaidAt = paidAt,
            Status = order.Status,
            StatusChangedAt = order.History.LastOrDefault(h => h.Status == order.Status)?.ChangedAt,
            Items = order.Items,
            Vat = order.Vat,
            TotalCents = order.TotalCents,
            Payments = order.Payments.Where(p => p.Status is PaymentStatus.Successful or PaymentStatus.Refunded).ToList(),
        };
    }

    public Task<(List<OrderSummary> Items, int Total)> ListMineAsync(CallerIdentity caller, int page, int pageSize) =>
        ListForCustomerAsync(caller.UserId, page, pageSize);

    /// <summary>Commandes d'un client, les plus récentes d'abord (fiche client côté Manager).</summary>
    public async Task<(List<OrderSummary> Items, int Total)> ListForCustomerAsync(Guid customerId, int page, int pageSize)
    {
        pageSize = Math.Clamp(pageSize, 1, 50);
        page = Math.Max(page, 1);
        var query = db.Orders.AsNoTracking().Include(o => o.Customer).Where(o => o.CustomerId == customerId);

        var total = await query.CountAsync();
        var orders = await query.OrderByDescending(o => o.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (orders.Select(OrderMapping.ToSummary).ToList(), total);
    }

    /// <summary>Commandes non terminées du client (bandeau « commande en cours »).</summary>
    public async Task<List<OrderSummary>> CurrentAsync(CallerIdentity caller)
    {
        var orders = await db.Orders.AsNoTracking().Include(o => o.Customer)
            .Where(o => o.CustomerId == caller.UserId && OrderTransitionPolicy.ActiveStatuses.Contains(o.Status))
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
        return orders.Select(OrderMapping.ToSummary).ToList();
    }

    public async Task<List<OrderSummary>> ListForManagerAsync(OrderFilter filter)
    {
        var date = filter.Date ?? DateOnly.FromDateTime(RestaurantClock.ToLocal(clock.GetUtcNow()));
        var query = db.Orders.AsNoTracking().Include(o => o.Customer).Where(o => o.ServiceDate == date);
        if (filter.Status is { } status)
            query = query.Where(o => o.Status == status);
        if (filter.IsPaid is { } paid)
            query = query.Where(o => (o.PaidAt != null) == paid);
        if (filter.Number is { } number)
            query = query.Where(o => o.DailyNumber == number);

        var orders = await query.OrderBy(o => o.PickupAt).ThenBy(o => o.DailyNumber).ToListAsync();
        return orders.Select(OrderMapping.ToSummary).ToList();
    }

    /// <summary>
    /// File du KDS : `Confirmée` et `En préparation`, par heure de retrait puis de validation. Une
    /// commande programmée n'apparaît qu'à partir de « heure de retrait − délai ASAP » (US-E07-1).
    /// </summary>
    public async Task<List<OrderDetails>> KitchenAsync()
    {
        var now = clock.GetUtcNow();
        var prep = await db.RestaurantSettings.AsNoTracking().Select(s => s.AsapDelayMinutes).FirstOrDefaultAsync();
        var visibleUntil = now.AddMinutes(prep);

        var orders = await WithDetails(db.Orders.AsNoTrackingWithIdentityResolution())
            .Where(o => (o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.InPreparation)
                && (o.PickupMode == PickupMode.Asap || o.PickupAt <= visibleUntil))
            .OrderBy(o => o.PickupAt).ThenBy(o => o.ConfirmedAt)
            .ToListAsync();
        return orders.Select(o => OrderMapping.ToDetails(o, true)).ToList();
    }
}
