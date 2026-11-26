using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Models;
using static RestaurantHandler.Models.OrderStatus;

namespace RestaurantHandler.Tests.Policies;

public class OrderTransitionPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    private static TransitionContext Ctx(bool paid = false, string? reason = null, int sinceSeconds = 60, int pickupMinutesAgo = 0) =>
        new(Now, paid, Now.AddMinutes(-pickupMinutesAgo), 30, Now.AddSeconds(-sinceSeconds), reason);

    private static string? Check(OrderStatus from, OrderStatus to, OrderActor actor, TransitionContext? ctx = null) =>
        OrderTransitionPolicy.Check(from, to, actor, ctx ?? Ctx());

    [Theory]
    [InlineData(PendingPayment, Confirmed, OrderActor.System)]
    [InlineData(PendingPayment, Cancelled, OrderActor.System)]
    [InlineData(Confirmed, InPreparation, OrderActor.Manager)]
    [InlineData(InPreparation, Ready, OrderActor.Manager)]
    public void Transitions_listees_autorisees(OrderStatus from, OrderStatus to, OrderActor actor) =>
        Assert.Null(Check(from, to, actor));

    [Theory]
    [InlineData(PendingPayment, Confirmed, OrderActor.Manager)]
    [InlineData(Confirmed, InPreparation, OrderActor.System)]
    [InlineData(Confirmed, Cancelled, OrderActor.System)]
    [InlineData(InPreparation, Cancelled, OrderActor.System)]
    [InlineData(Ready, Cancelled, OrderActor.System)]
    [InlineData(Confirmed, Ready, OrderActor.Manager)]
    [InlineData(Ready, Confirmed, OrderActor.Manager)]
    [InlineData(PendingPayment, InPreparation, OrderActor.Manager)]
    public void Transitions_non_listees_ou_mauvais_acteur_refusees(OrderStatus from, OrderStatus to, OrderActor actor) =>
        Assert.NotNull(Check(from, to, actor));

    [Theory]
    [InlineData(Completed)]
    [InlineData(NoShow)]
    [InlineData(Cancelled)]
    public void Etats_finaux_immuables(OrderStatus final)
    {
        foreach (var to in Enum.GetValues<OrderStatus>())
            foreach (var actor in Enum.GetValues<OrderActor>())
                Assert.NotNull(Check(final, to, actor, Ctx(paid: true, reason: "x")));
    }

    [Theory]
    [InlineData(PendingPayment, Cancelled)]
    [InlineData(Confirmed, Cancelled)]
    [InlineData(InPreparation, Cancelled)]
    [InlineData(Ready, Cancelled)]
    public void Annulation_Manager_exige_un_motif(OrderStatus from, OrderStatus to)
    {
        Assert.NotNull(Check(from, to, OrderActor.Manager));
        Assert.Null(Check(from, to, OrderActor.Manager, Ctx(reason: "Rupture découverte")));
    }

    [Fact]
    public void Seuls_le_restaurant_et_le_systeme_agissent_sur_une_commande() =>
        // ADR-0013 : le client n'annule plus lui-même, il appelle le restaurant.
        Assert.Equal([OrderActor.Manager, OrderActor.System], Enum.GetValues<OrderActor>());

    [Fact]
    public void Retiree_exige_une_commande_payee()
    {
        Assert.NotNull(Check(Ready, Completed, OrderActor.Manager, Ctx(paid: false)));
        Assert.Null(Check(Ready, Completed, OrderActor.Manager, Ctx(paid: true)));
    }

    [Fact]
    public void Non_venu_seulement_apres_retrait_plus_delai_de_grace()
    {
        Assert.NotNull(Check(Ready, NoShow, OrderActor.Manager, Ctx(pickupMinutesAgo: 29)));
        Assert.Null(Check(Ready, NoShow, OrderActor.Manager, Ctx(pickupMinutesAgo: 30)));
    }

    [Theory]
    [InlineData(InPreparation, Confirmed)]
    [InlineData(Ready, InPreparation)]
    public void Annulation_de_geste_cuisine_dans_les_10_secondes(OrderStatus from, OrderStatus to)
    {
        Assert.Null(Check(from, to, OrderActor.Manager, Ctx(sinceSeconds: 10)));
        Assert.NotNull(Check(from, to, OrderActor.Manager, Ctx(sinceSeconds: 11)));
    }
}
