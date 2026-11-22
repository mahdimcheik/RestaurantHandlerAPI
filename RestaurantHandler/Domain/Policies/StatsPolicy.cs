using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Domain.Policies;

/// <summary>Jours de Paris inclus et instants UTC correspondants (fin exclue).</summary>
public sealed record StatsRange(DateOnly First, DateOnly Last, DateTimeOffset FromUtc, DateTimeOffset ToUtc);

/// <summary>
/// Indicateurs de pilotage E09 (pas de la comptabilité : la caisse tierce fait foi). Une commande
/// est **comptée** si elle est `Retirée`, ou `Prête` et payée ; elle l'est à son instant de
/// retrait (`CompletedAt`, à défaut `ReadyAt`). Les commandes reçues doivent avoir leurs lignes
/// (avec <c>Children</c> et modifications) et leurs paiements chargés.
/// </summary>
public static class StatsPolicy
{
    public const int MaxCustomDays = 366;

    public static readonly OrderStatus[] CountedStatuses = [OrderStatus.Completed, OrderStatus.Ready];

    public static bool IsCounted(OrderStatus status, DateTimeOffset? paidAt) =>
        status == OrderStatus.Completed || (status == OrderStatus.Ready && paidAt is not null);

    public static DateTimeOffset? ReferenceInstant(Order o) => o.CompletedAt ?? o.ReadyAt;

    public static StatsRange ResolvePeriod(StatsPeriod period, DateOnly? from, DateOnly? to, DateOnly today)
    {
        var anchor = from ?? today;
        var (first, last) = period switch
        {
            StatsPeriod.Day => (anchor, anchor),
            StatsPeriod.Week => WeekOf(anchor),
            StatsPeriod.Month => (new DateOnly(anchor.Year, anchor.Month, 1),
                new DateOnly(anchor.Year, anchor.Month, DateTime.DaysInMonth(anchor.Year, anchor.Month))),
            StatsPeriod.Custom => CustomRange(from, to),
            _ => throw new BusinessRuleException("Période inconnue."),
        };
        return new StatsRange(first, last, DayStartUtc(first), DayStartUtc(last.AddDays(1)));
    }

    public static DashboardStats Dashboard(DateOnly date, IEnumerable<Order> orders, IReadOnlyDictionary<OrderStatus, int> byStatus)
    {
        var counted = orders.Where(o => IsCounted(o.Status, o.PaidAt)).ToList();
        var revenue = counted.Sum(o => o.TotalCents);
        var successful = counted.SelectMany(o => o.Payments).Where(p => p.Status == PaymentStatus.Successful).ToList();
        return new DashboardStats
        {
            Date = date,
            RevenueCents = revenue,
            Orders = counted.Count,
            AverageBasketCents = counted.Count == 0 ? 0 : revenue / counted.Count,
            Online = Channel(counted, OrderChannel.Online),
            Counter = Channel(counted, OrderChannel.Counter),
            PaidOnlineCents = successful.Where(p => p.Method == PaymentMethod.SumupOnline).Sum(p => p.AmountCents),
            PaidOnSiteCents = successful.Where(p => p.Method != PaymentMethod.SumupOnline).Sum(p => p.AmountCents),
            Vat = VatPolicy.Breakdown(counted.SelectMany(o => o.Items.Where(i => i.ParentItemId is null)))
                .Select(v => new VatLineDto { Rate = v.Rate, GrossCents = v.GrossCents, NetCents = v.NetCents, VatCents = v.VatCents })
                .ToList(),
            ByStatus = Enum.GetValues<OrderStatus>().ToDictionary(s => s, s => byStatus.GetValueOrDefault(s)),
        };
    }

    public static ProductSalesReport Products(StatsRange range, IEnumerable<Order> orders)
    {
        var products = new Dictionary<Guid, ProductSales>();
        var menus = new Dictionary<Guid, MenuSales>();
        var supplements = new Dictionary<Guid, SupplementSales>();

        // Les plus récentes en dernier : le nom affiché est le dernier snapshot.
        foreach (var order in orders.Where(o => IsCounted(o.Status, o.PaidAt)).OrderBy(ReferenceInstant))
        {
            foreach (var line in order.Items.Where(i => i.ParentItemId is null))
            {
                if (line.MenuId is { } menuId)
                {
                    var m = menus.TryGetValue(menuId, out var found) ? found : menus[menuId] = new MenuSales { MenuId = menuId };
                    m.Name = line.NameSnapshot;
                    m.Quantity += line.Quantity;
                    m.RevenueCents += OrderPricing.LineTotal(line);
                    foreach (var component in line.Children)
                    {
                        if (component.ProductId is { } componentId)
                            Product(componentId, component.NameSnapshot).QuantityInMenus += component.Quantity * line.Quantity;
                        AddSupplements(component, component.Quantity * line.Quantity);
                    }
                }
                else if (line.ProductId is { } productId)
                {
                    var p = Product(productId, line.NameSnapshot);
                    p.QuantityAlone += line.Quantity;
                    p.RevenueCents += OrderPricing.LineTotal(line);
                    AddSupplements(line, line.Quantity);
                }
            }
        }

        return new ProductSalesReport
        {
            From = range.First,
            To = range.Last,
            Products = products.Values
                .OrderByDescending(p => p.QuantityAlone + p.QuantityInMenus).ThenByDescending(p => p.RevenueCents).ThenBy(p => p.Name)
                .ToList(),
            Menus = menus.Values.OrderByDescending(m => m.Quantity).ThenByDescending(m => m.RevenueCents).ThenBy(m => m.Name).ToList(),
            Supplements = supplements.Values.OrderByDescending(s => s.Quantity).ThenByDescending(s => s.RevenueCents).ThenBy(s => s.Name).ToList(),
        };

        ProductSales Product(Guid id, string name)
        {
            var p = products.TryGetValue(id, out var found) ? found : products[id] = new ProductSales { ProductId = id };
            p.Name = name;
            return p;
        }

        void AddSupplements(OrderItem item, int multiplier)
        {
            foreach (var mod in item.Modifications.Where(m => m.Type == ModificationType.Supplement && m.IngredientId is not null))
            {
                var ingredientId = mod.IngredientId!.Value;
                var s = supplements.TryGetValue(ingredientId, out var found)
                    ? found
                    : supplements[ingredientId] = new SupplementSales { IngredientId = ingredientId };
                s.Name = mod.LabelSnapshot;
                s.Quantity += mod.Quantity * multiplier;
                s.RevenueCents += mod.UnitPriceCents * mod.Quantity * multiplier;
            }
        }
    }

    public static PeakHoursReport PeakHours(StatsRange range, IEnumerable<Order> orders, int bucketMinutes)
    {
        if (bucketMinutes is not (15 or 30))
            throw new BusinessRuleException("Tranche de 15 ou 30 minutes.");

        var buckets = orders
            .Where(o => IsCounted(o.Status, o.PaidAt) && ReferenceInstant(o) is not null)
            .Select(o =>
            {
                var local = RestaurantClock.ToLocal(ReferenceInstant(o)!.Value);
                var minutes = local.Hour * 60 + local.Minute;
                return (Order: o, local.DayOfWeek, Start: new TimeOnly(0, 0).AddMinutes(minutes - minutes % bucketMinutes));
            })
            .GroupBy(x => (x.DayOfWeek, x.Start))
            .Select(g =>
            {
                var prep = g
                    .Where(x => x.Order.ConfirmedAt is not null && x.Order.ReadyAt is not null)
                    .Select(x => (x.Order.ReadyAt!.Value - x.Order.ConfirmedAt!.Value).TotalMinutes)
                    .ToList();
                return new PeakHoursBucket
                {
                    DayOfWeek = g.Key.DayOfWeek,
                    Start = g.Key.Start,
                    Orders = g.Count(),
                    RevenueCents = g.Sum(x => x.Order.TotalCents),
                    AveragePreparationMinutes = prep.Count == 0 ? null : Math.Round(prep.Average(), 1),
                };
            })
            .OrderBy(b => ((int)b.DayOfWeek + 6) % 7)
            .ThenBy(b => b.Start)
            .ToList();

        return new PeakHoursReport { From = range.First, To = range.Last, BucketMinutes = bucketMinutes, Buckets = buckets };
    }

    private static ChannelStats Channel(List<Order> counted, OrderChannel channel)
    {
        var orders = counted.Where(o => o.Channel == channel).ToList();
        return new ChannelStats { Orders = orders.Count, RevenueCents = orders.Sum(o => o.TotalCents) };
    }

    private static (DateOnly, DateOnly) WeekOf(DateOnly day)
    {
        var monday = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        return (monday, monday.AddDays(6));
    }

    private static (DateOnly, DateOnly) CustomRange(DateOnly? from, DateOnly? to)
    {
        if (from is not { } first || to is not { } last)
            throw new BusinessRuleException("Plage libre : dates de début et de fin obligatoires.");
        if (last < first)
            throw new BusinessRuleException("La date de fin précède la date de début.");
        if (last.DayNumber - first.DayNumber + 1 > MaxCustomDays)
            throw new BusinessRuleException($"Plage limitée à {MaxCustomDays} jours.");
        return (first, last);
    }

    private static DateTimeOffset DayStartUtc(DateOnly day) => RestaurantClock.ToUtc(day.ToDateTime(TimeOnly.MinValue));
}
