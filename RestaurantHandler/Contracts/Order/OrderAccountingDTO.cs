using RestaurantHandler.Models.Interfaces;
using Stripe;
using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models;
/// <summary>
/// Détail comptable d'UNE réservation dans l'état comptable d'une commande.
/// Vue admin.
/// </summary>
public class AccountingLine
{
    [Required]
    public Guid ReservationId { get; set; }

    /// <summary>Montant brut payé par l'élève pour cette réservation (figé).</summary>
    [Required]
    public decimal Amount { get; set; }

    /// <summary>Taux de commission plateforme figé à la réservation.</summary>
    [Required]
    public decimal CommissionRate { get; set; }

    /// <summary>Part prof brute = <c>Amount × (1 - CommissionRate)</c>.</summary>
    [Required]
    public decimal TeacherShare { get; set; }

    /// <summary>Vrai si la réservation a été remboursée (part prof annulée).</summary>
    public bool Refunded { get; set; }

    /// <summary>Vrai si la part prof a déjà été effectivement versée (payout).</summary>
    public bool PaidOut { get; set; }
}
/// <summary>
/// État comptable consolidé d'une commande (vue admin uniquement) : ce qui a été
/// encaissé, remboursé à l'élève, payé au prof (dû + versé) et le gain plateforme.
/// Entièrement dérivé du ledger (Payment / TeacherWalletTransaction / Refund) ;
/// aucune valeur n'est stockée.
/// </summary>
public class OrderAccounting
{
    [Required]
    public Guid OrderId { get; set; }
    [Required]
    public string OrderNumber { get; set; } = string.Empty;
    public string? StatusName { get; set; }

    /// <summary>Vrai si la commande a été payée (un <c>Payment</c> existe).</summary>
    public bool IsPaid { get; set; }

    /// <summary>Brut encaissé auprès de l'élève (= <c>Payment.Amount</c>).</summary>
    [Required]
    public decimal GrossPaid { get; set; }

    /// <summary>Frais Stripe prélevés sur l'encaissement. Null tant que non settled.</summary>
    public decimal? StripeFee { get; set; }

    /// <summary>Vrai si les frais Stripe ne sont pas encore connus (backfill en attente).</summary>
    public bool StripeFeePending { get; set; }

    /// <summary>Net réellement arrivé sur le solde plateforme (brut − frais Stripe).</summary>
    [Required]
    public decimal NetCollected { get; set; }

    /// <summary>Total remboursé à l'élève (= somme des <c>Refund</c>).</summary>
    [Required]
    public decimal RefundedAmount { get; set; }

    /// <summary>Part prof créditée mais pas encore versée (<c>PayoutId == null</c>).</summary>
    [Required]
    public decimal TeacherOwed { get; set; }

    /// <summary>Part prof déjà effectivement versée (<c>PayoutId != null</c>).</summary>
    [Required]
    public decimal TeacherPaidOut { get; set; }

    /// <summary>Total dû + versé au prof pour la commande.</summary>
    [Required]
    public decimal TeacherTotal { get; set; }

    /// <summary>Gain plateforme = <c>NetCollected − RefundedAmount − TeacherTotal</c>.</summary>
    [Required]
    public decimal PlatformGain { get; set; }

    /// <summary>Vrai si le gain est provisoire (frais Stripe pas encore settled).</summary>
    public bool PlatformGainProvisional { get; set; }

    /// <summary>Détail ligne à ligne par réservation.</summary>
    public List<AccountingLine> Lines { get; set; } = [];
}
