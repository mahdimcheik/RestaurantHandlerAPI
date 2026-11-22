using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Models;

namespace RestaurantHandler.Tests.Policies;

public class StatsPolicyTests
{
    private static readonly Guid Classic = Guid.NewGuid();
    private static readonly Guid Cola = Guid.NewGuid();
    private static readonly Guid MenuId = Guid.NewGuid();
    private static readonly Guid Cheddar = Guid.NewGuid();

    // Mercredi 25/11/2026, heure de Paris = UTC+1.
    private static DateTimeOffset Paris(int hour, int minute = 0, int day = 25) => new(2026, 11, day, hour - 1, minute, 0, TimeSpan.Zero);

    private static readonly StatsRange Day = StatsPolicy.ResolvePeriod(StatsPeriod.Day, new DateOnly(2026, 11, 25), null, default);

    private static OrderItem ProductLine(Guid id, string name, int unit, int qty = 1, int cheddars = 0)
    {
        var line = new OrderItem { ProductId = id, NameSnapshot = name, UnitPriceCents = unit, Quantity = qty, VatRate = 10 };
        if (cheddars > 0)
            line.Modifications.Add(new OrderItemModification
            {
                Type = ModificationType.Supplement, IngredientId = Cheddar, LabelSnapshot = "+ Cheddar", Quantity = cheddars, UnitPriceCents = 80,
            });
        return line;
    }

    private static OrderItem MenuLine(int qty, int platCheddars = 0)
    {
        var menu = new OrderItem { Id = Guid.NewGuid(), MenuId = MenuId, NameSnapshot = "Menu Classic", UnitPriceCents = 1290, Quantity = qty, VatRate = 10 };
        foreach (var component in new[] { ProductLine(Classic, "Classic", 0, cheddars: platCheddars), ProductLine(Cola, "Cola", 50) })
        {
            component.ParentItemId = menu.Id;
            menu.Children.Add(component);
        }
        return menu;
    }

    private static Order Counted(DateTimeOffset at, OrderChannel channel = OrderChannel.Counter, PaymentMethod method = PaymentMethod.OnSiteCash,
        OrderStatus status = OrderStatus.Completed, DateTimeOffset? confirmedAt = null, params OrderItem[] lines)
    {
        var order = new Order { Channel = channel, Status = status, ReadyAt = at, ConfirmedAt = confirmedAt };
        if (status == OrderStatus.Completed)
            order.CompletedAt = at;
        foreach (var line in lines)
        {
            order.Items.Add(line);
            foreach (var child in line.Children)
                order.Items.Add(child);
        }
        order.TotalCents = order.Items.Where(i => i.ParentItemId is null).Sum(OrderPricing.LineTotal);
        order.PaidAt = at;
        order.Payments.Add(new Payment { Method = method, Status = PaymentStatus.Successful, AmountCents = order.TotalCents });
        return order;
    }

    [Theory]
    [InlineData(OrderStatus.Completed, false, true)]
    [InlineData(OrderStatus.Ready, true, true)]
    [InlineData(OrderStatus.Ready, false, false)]
    [InlineData(OrderStatus.Cancelled, true, false)]
    [InlineData(OrderStatus.Refused, false, false)]
    [InlineData(OrderStatus.NoShow, true, false)]
    [InlineData(OrderStatus.InPreparation, true, false)]
    public void Commande_comptee_retiree_ou_prete_payee(OrderStatus status, bool paid, bool expected) =>
        Assert.Equal(expected, StatsPolicy.IsCounted(status, paid ? DateTimeOffset.UtcNow : null));

    [Fact]
    public void Une_journee_va_de_minuit_a_minuit_heure_de_Paris()
    {
        Assert.Equal(new DateTimeOffset(2026, 11, 24, 23, 0, 0, TimeSpan.Zero), Day.FromUtc);
        Assert.Equal(new DateTimeOffset(2026, 11, 25, 23, 0, 0, TimeSpan.Zero), Day.ToUtc);
    }

    [Fact]
    public void Le_jour_du_passage_a_l_heure_d_ete_dure_23_heures()
    {
        var range = StatsPolicy.ResolvePeriod(StatsPeriod.Day, new DateOnly(2026, 3, 29), null, default);
        Assert.Equal(new DateTimeOffset(2026, 3, 28, 23, 0, 0, TimeSpan.Zero), range.FromUtc);
        Assert.Equal(TimeSpan.FromHours(23), range.ToUtc - range.FromUtc);
    }

    [Fact]
    public void Semaine_du_lundi_au_dimanche_mois_entier_et_jour_par_defaut()
    {
        var week = StatsPolicy.ResolvePeriod(StatsPeriod.Week, new DateOnly(2026, 11, 29), null, default);
        Assert.Equal((new DateOnly(2026, 11, 23), new DateOnly(2026, 11, 29)), (week.First, week.Last));

        var month = StatsPolicy.ResolvePeriod(StatsPeriod.Month, new DateOnly(2027, 2, 10), null, default);
        Assert.Equal((new DateOnly(2027, 2, 1), new DateOnly(2027, 2, 28)), (month.First, month.Last));

        var today = StatsPolicy.ResolvePeriod(StatsPeriod.Day, null, null, new DateOnly(2026, 12, 1));
        Assert.Equal(new DateOnly(2026, 12, 1), today.Last);
    }

    [Fact]
    public void Plage_libre_exige_des_bornes_ordonnees_et_limitees()
    {
        var d = new DateOnly(2026, 1, 1);
        Assert.Throws<BusinessRuleException>(() => StatsPolicy.ResolvePeriod(StatsPeriod.Custom, d, null, d));
        Assert.Throws<BusinessRuleException>(() => StatsPolicy.ResolvePeriod(StatsPeriod.Custom, d, d.AddDays(-1), d));
        Assert.Throws<BusinessRuleException>(() => StatsPolicy.ResolvePeriod(StatsPeriod.Custom, d, d.AddDays(366), d));

        var ok = StatsPolicy.ResolvePeriod(StatsPeriod.Custom, d, d.AddDays(365), d);
        Assert.Equal(d.AddDays(365), ok.Last);
    }

    [Fact]
    public void Tableau_du_jour_CA_panier_moyen_et_repartitions()
    {
        var cancelled = Counted(Paris(13), lines: [ProductLine(Classic, "Classic", 950)]);
        cancelled.Status = OrderStatus.Cancelled;
        var orders = new[]
        {
            Counted(Paris(12), OrderChannel.Online, PaymentMethod.SumupOnline, lines: [ProductLine(Classic, "Classic", 950)]),
            Counted(Paris(12, 10), lines: [MenuLine(1)]),
            Counted(Paris(13), status: OrderStatus.Ready, method: PaymentMethod.OnSiteCard, lines: [ProductLine(Cola, "Cola", 250, 2)]),
            cancelled,
        };

        var stats = StatsPolicy.Dashboard(Day.First, orders, new Dictionary<OrderStatus, int> { [OrderStatus.Cancelled] = 1, [OrderStatus.Completed] = 2 });

        Assert.Equal(950 + 1340 + 500, stats.RevenueCents);
        Assert.Equal(3, stats.Orders);
        Assert.Equal(2790 / 3, stats.AverageBasketCents);
        Assert.Equal((1, 950), (stats.Online.Orders, stats.Online.RevenueCents));
        Assert.Equal((2, 1840), (stats.Counter.Orders, stats.Counter.RevenueCents));
        Assert.Equal(950, stats.PaidOnlineCents);
        Assert.Equal(1840, stats.PaidOnSiteCents);
        Assert.Equal(2790, Assert.Single(stats.Vat).GrossCents);
        Assert.Equal(1, stats.ByStatus[OrderStatus.Cancelled]);
        Assert.Equal(0, stats.ByStatus[OrderStatus.NoShow]);
        Assert.Equal(Enum.GetValues<OrderStatus>().Length, stats.ByStatus.Count);
    }

    [Fact]
    public void Tableau_sans_commande_panier_moyen_nul()
    {
        var stats = StatsPolicy.Dashboard(Day.First, [], new Dictionary<OrderStatus, int>());
        Assert.Equal((0, 0, 0), (stats.RevenueCents, stats.Orders, stats.AverageBasketCents));
    }

    [Fact]
    public void Ventes_distinguent_seul_et_menu_et_comptent_les_supplements()
    {
        var orders = new[]
        {
            Counted(Paris(12), lines: [ProductLine(Classic, "Classic", 950, qty: 2, cheddars: 1)]),
            Counted(Paris(12, 30), lines: [MenuLine(3, platCheddars: 2)]),
            Counted(Paris(13), status: OrderStatus.NoShow, lines: [ProductLine(Classic, "Classic", 950, qty: 5)]),
        };

        var report = StatsPolicy.Products(Day, orders);

        var classic = report.Products.Single(p => p.ProductId == Classic);
        Assert.Equal((2, 3, 2 * (950 + 80)), (classic.QuantityAlone, classic.QuantityInMenus, classic.RevenueCents));
        var cola = report.Products.Single(p => p.ProductId == Cola);
        Assert.Equal((0, 3, 0), (cola.QuantityAlone, cola.QuantityInMenus, cola.RevenueCents));
        Assert.Equal(Classic, report.Products[0].ProductId);

        var menu = Assert.Single(report.Menus);
        Assert.Equal((3, 3 * (1290 + 160 + 50)), (menu.Quantity, menu.RevenueCents));

        var cheddar = Assert.Single(report.Supplements);
        Assert.Equal((2 + 6, (2 + 6) * 80), (cheddar.Quantity, cheddar.RevenueCents));
    }

    [Fact]
    public void Le_nom_affiche_est_le_dernier_snapshot()
    {
        var orders = new[]
        {
            Counted(Paris(14), lines: [ProductLine(Classic, "Classic XL", 1500)]),
            Counted(Paris(12), lines: [ProductLine(Classic, "Classic", 950)]),
        };
        Assert.Equal("Classic XL", StatsPolicy.Products(Day, orders).Products.Single().Name);
    }

    [Theory]
    [InlineData(15, 12, 15)]
    [InlineData(30, 12, 0)]
    public void Heures_de_pointe_par_tranche_locale_avec_temps_de_preparation(int bucket, int expectedHour, int expectedMinute)
    {
        var orders = new[]
        {
            Counted(Paris(12, 20), confirmedAt: Paris(12, 8), lines: [ProductLine(Classic, "Classic", 950)]),
            Counted(Paris(12, 25), confirmedAt: Paris(12, 5), lines: [ProductLine(Cola, "Cola", 250)]),
            Counted(Paris(19, 0), lines: [ProductLine(Cola, "Cola", 250)]),
        };

        var report = StatsPolicy.PeakHours(Day, orders, bucket);

        var lunch = report.Buckets[0];
        Assert.Equal(DayOfWeek.Wednesday, lunch.DayOfWeek);
        Assert.Equal(new TimeOnly(expectedHour, expectedMinute), lunch.Start);
        Assert.Equal((2, 1200), (lunch.Orders, lunch.RevenueCents));
        Assert.Equal(16, lunch.AveragePreparationMinutes);
        Assert.Null(report.Buckets[1].AveragePreparationMinutes);
        Assert.Equal(new TimeOnly(19, 0), report.Buckets[1].Start);
    }

    [Fact]
    public void Heures_de_pointe_lundi_d_abord_et_tranche_validee()
    {
        var orders = new[]
        {
            Counted(Paris(12, day: 29), lines: [ProductLine(Cola, "Cola", 250)]), // dimanche
            Counted(Paris(12, day: 23), lines: [ProductLine(Cola, "Cola", 250)]), // lundi
        };
        var report = StatsPolicy.PeakHours(Day, orders, 30);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Sunday], report.Buckets.Select(b => b.DayOfWeek));

        Assert.Throws<BusinessRuleException>(() => StatsPolicy.PeakHours(Day, orders, 20));
    }
}
