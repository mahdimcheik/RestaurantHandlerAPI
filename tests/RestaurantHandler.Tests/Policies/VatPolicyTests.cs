using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Models;

namespace RestaurantHandler.Tests.Policies;

public class VatPolicyTests
{
    private static OrderItem Line(int unit, decimal rate, int qty = 1) =>
        new() { NameSnapshot = "x", UnitPriceCents = unit, VatRate = rate, Quantity = qty };

    [Fact]
    public void Ventile_par_taux_sur_le_total_R1()
    {
        var breakdown = VatPolicy.Breakdown([Line(950, 10), Line(250, 5.5m, 2), Line(1290, 10)]);

        var ten = breakdown.Single(b => b.Rate == 10);
        Assert.Equal(2240, ten.GrossCents);
        Assert.Equal(2036, ten.NetCents); // 2240 × 100 / 110 = 2036,36
        Assert.Equal(204, ten.VatCents);

        var reduced = breakdown.Single(b => b.Rate == 5.5m);
        Assert.Equal(500, reduced.GrossCents);
        Assert.Equal(474, reduced.NetCents); // 500 × 100 / 105,5 = 473,93
        Assert.Equal(26, reduced.VatCents);
    }

    [Fact]
    public void Arrondi_sur_le_total_et_non_ligne_par_ligne()
    {
        // 3 lignes à 0,05 € à 20 % : par ligne HT = 4 c (×3 = 12) ; sur le total HT = round(12,5) = 13.
        var breakdown = VatPolicy.Breakdown([Line(5, 20), Line(5, 20), Line(5, 20)]);

        Assert.Equal(13, breakdown.Single().NetCents);
        Assert.Equal(2, breakdown.Single().VatCents);
    }
}
