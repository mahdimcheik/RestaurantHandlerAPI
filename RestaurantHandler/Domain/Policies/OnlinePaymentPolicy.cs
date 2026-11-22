using RestaurantHandler.Models;

namespace RestaurantHandler.Domain.Policies;

public enum PaymentOutcome
{
    /// <summary>Rien à faire : paiement déjà traité, ou checkout encore en attente.</summary>
    None,

    /// <summary>Checkout échoué ou expiré : la tentative est close, la commande attend un nouvel essai.</summary>
    AttemptFailed,

    /// <summary>Paiement conforme sur une commande en attente : `Confirmée`, payée.</summary>
    Confirm,

    /// <summary>Paiement reçu mais montant ou devise différents : anomalie, aucune transition (R9).</summary>
    Anomaly,

    /// <summary>Paiement reçu alors que la commande n'en attend plus (annulée, déjà payée, basculée au retrait) : remboursement.</summary>
    RefundUnexpected,
}

/// <summary>Ce que SumUp dit du checkout, une fois relu (jamais la notification seule).</summary>
public sealed record CheckoutState(string Status, int? PaidAmountCents, string? PaidCurrency);

/// <summary>R9 et US-E05-2 : décision prise à chaque relecture ; rejouée, elle ne produit rien de plus.</summary>
public static class OnlinePaymentPolicy
{
    public static PaymentOutcome Decide(PaymentStatus attempt, OrderStatus order, int orderTotalCents, CheckoutState checkout)
    {
        if (attempt != PaymentStatus.Pending)
            return PaymentOutcome.None;

        return checkout.Status switch
        {
            "PAID" when checkout.PaidAmountCents is null => PaymentOutcome.None,
            "PAID" when order != OrderStatus.PendingPayment => PaymentOutcome.RefundUnexpected,
            "PAID" when checkout.PaidAmountCents != orderTotalCents || checkout.PaidCurrency != "EUR" => PaymentOutcome.Anomaly,
            "PAID" => PaymentOutcome.Confirm,
            "FAILED" or "EXPIRED" => PaymentOutcome.AttemptFailed,
            _ => PaymentOutcome.None,
        };
    }
}
