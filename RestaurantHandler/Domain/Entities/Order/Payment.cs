using RestaurantHandler.Models.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantHandler.Models;

/// <summary>
/// ROLE: A single settlement of an <see cref="Order"/> (the money actually paid in).
/// Records how much was paid (<see cref="Amount"/>), through which
/// <see cref="PaymentMethod"/>, and the external provider reference
/// (<see cref="TransactionRef"/>, e.g. the Stripe PaymentIntent id).
/// 1:1 with Order. Buyer side.
/// </summary>
public class Payment : BaseModel
{
    [Required]
    public decimal Amount { get; set; }

    /// <summary>
    /// Processing fee Stripe deducted from this charge (in the order currency, e.g. euros).
    /// Comes from the charge's BalanceTransaction. Nullable: the BalanceTransaction is not
    /// always settled the instant the checkout completes.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? StripeFee { get; set; }

    /// <summary>
    /// Net amount that actually landed in the platform balance (gross - <see cref="StripeFee"/>).
    /// This is the real money to split between the platform and the teacher(s).
    /// Comes from the charge's BalanceTransaction. Nullable for the same reason as <see cref="StripeFee"/>.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? NetAmount { get; set; }

    /// <summary>
    /// Stripe BalanceTransaction id (txn_...) backing the fee/net figures, kept for reconciliation.
    /// </summary>
    public string? BalanceTransactionId { get; set; }

    [Required]
    public Guid MethodId { get; set; }
    public PaymentMethod? Method { get; set; }
    [Required]
    [ForeignKey(nameof(Order))]
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
    public string? TransactionRef { get; set; }
}
