using RestaurantHandler.Models;

namespace RestaurantHandler.Domain.Policies;

public sealed record VatBreakdown(decimal Rate, int GrossCents, int NetCents, int VatCents);

/// <summary>
/// R1 : prix TTC ; HT et TVA déduits **par taux sur le total de la commande**, arrondis au centime
/// le plus proche. Une ligne menu est au taux du menu (question 3 en attente).
/// </summary>
public static class VatPolicy
{
    public static IReadOnlyList<VatBreakdown> Breakdown(IEnumerable<OrderItem> topLevelItems) =>
        topLevelItems
            .GroupBy(i => i.VatRate)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var gross = g.Sum(OrderPricing.LineTotal);
                var net = (int)Math.Round(gross * 100m / (100m + g.Key), MidpointRounding.AwayFromZero);
                return new VatBreakdown(g.Key, gross, net, gross - net);
            })
            .ToList();
}
