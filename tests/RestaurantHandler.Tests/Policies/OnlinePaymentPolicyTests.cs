using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Models;

namespace RestaurantHandler.Tests.Policies;

public class OnlinePaymentPolicyTests
{
    private static PaymentOutcome Decide(string status, int? paid = 1900, string currency = "EUR",
        OrderStatus order = OrderStatus.PendingPayment, PaymentStatus attempt = PaymentStatus.Pending) =>
        OnlinePaymentPolicy.Decide(attempt, order, 1900, new CheckoutState(status, paid, currency));

    [Fact]
    public void Paiement_conforme_confirme_la_commande() => Assert.Equal(PaymentOutcome.Confirm, Decide("PAID"));

    [Theory]
    [InlineData(1800, "EUR")]
    [InlineData(2000, "EUR")]
    [InlineData(1900, "GBP")]
    public void Montant_ou_devise_differents_anomalie(int paid, string currency) =>
        Assert.Equal(PaymentOutcome.Anomaly, Decide("PAID", paid, currency));

    [Fact]
    public void Notification_rejouee_apres_traitement_sans_effet()
    {
        Assert.Equal(PaymentOutcome.None, Decide("PAID", attempt: PaymentStatus.Successful));
        Assert.Equal(PaymentOutcome.None, Decide("PAID", attempt: PaymentStatus.Refunded));
    }

    [Theory]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.InPreparation)]
    public void Paiement_recu_sur_une_commande_qui_ne_l_attend_plus_rembourse(OrderStatus order) =>
        Assert.Equal(PaymentOutcome.RefundUnexpected, Decide("PAID", order: order));

    [Theory]
    [InlineData("FAILED")]
    [InlineData("EXPIRED")]
    public void Echec_ou_expiration_close_la_tentative(string status) =>
        Assert.Equal(PaymentOutcome.AttemptFailed, Decide(status));

    [Fact]
    public void En_attente_ou_paye_sans_transaction_reussie_ne_fait_rien()
    {
        Assert.Equal(PaymentOutcome.None, Decide("PENDING"));
        Assert.Equal(PaymentOutcome.None, Decide("PAID", paid: null));
    }
}
