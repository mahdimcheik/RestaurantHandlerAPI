using Microsoft.EntityFrameworkCore;
using RestaurantHandler.Contexts;
using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Services;

/// <summary>Statistiques E09 : charge les commandes comptées d'une période, les calculs sont dans <see cref="StatsPolicy"/>.</summary>
public class StatsService(MainContext db, TimeProvider clock)
{
    public async Task<DashboardStats> DashboardAsync(DateOnly? date)
    {
        var range = StatsPolicy.ResolvePeriod(StatsPeriod.Day, date, null, Today());
        var orders = await CountedAsync(range);
        var byStatus = await db.Orders.AsNoTracking()
            .Where(o => o.ServiceDate == range.First)
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);
        return StatsPolicy.Dashboard(range.First, orders, byStatus);
    }

    public async Task<ProductSalesReport> ProductsAsync(StatsPeriodQuery query)
    {
        var range = StatsPolicy.ResolvePeriod(query.Period, query.From, query.To, Today());
        return StatsPolicy.Products(range, await CountedAsync(range));
    }

    public async Task<PeakHoursReport> PeakHoursAsync(PeakHoursQuery query)
    {
        var range = StatsPolicy.ResolvePeriod(query.Period, query.From, query.To, Today());
        return StatsPolicy.PeakHours(range, await CountedAsync(range), query.BucketMinutes);
    }

    private DateOnly Today() => DateOnly.FromDateTime(RestaurantClock.ToLocal(clock.GetUtcNow()));

    /// <summary>Commandes comptées dont l'instant de retrait tombe dans la plage (index M10).</summary>
    private Task<List<Order>> CountedAsync(StatsRange range) =>
        db.Orders.AsNoTrackingWithIdentityResolution()
            .Include(o => o.Items).ThenInclude(i => i.Modifications)
            .Include(o => o.Payments)
            .Where(o =>
                (o.Status == OrderStatus.Completed && o.CompletedAt >= range.FromUtc && o.CompletedAt < range.ToUtc)
                || (o.Status == OrderStatus.Ready && o.PaidAt != null && o.ReadyAt >= range.FromUtc && o.ReadyAt < range.ToUtc))
            .AsSplitQuery()
            .ToListAsync();
}
