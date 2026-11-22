using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantHandler.Models.Interfaces;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models;

/// <summary>
/// ROLE: A single refund given back to the student for ONE cancelled
/// <see cref="Reservation"/> (the buyer side of a cancellation). The paid
/// <see cref="Order"/> stays immutable (its <see cref="Order.TotalAmount"/> /
/// <see cref="Payment.Amount"/> never change); the money returned is recorded
/// here as an append-only line, the buyer-side mirror of the negative
/// <see cref="TeacherWalletTransaction"/> (type Refund) written on the seller side.
/// </summary>
public class Refund : BaseModel
{
    /// <summary>Gross amount returned to the student for the cancelled reservation (= <see cref="Reservation.Amount"/>).</summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    [ForeignKey(nameof(Reservation))]
    public Guid ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    [Required]
    [ForeignKey(nameof(Order))]
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    /// <summary>The settling payment that was refunded. Nullable to stay defensive if the link is missing.</summary>
    [ForeignKey(nameof(Payment))]
    public Guid? PaymentId { get; set; }
    public Payment? Payment { get; set; }

    /// <summary>Stripe refund id (re_...), kept for idempotency and reconciliation.</summary>
    public string? StripeRefundId { get; set; }

    /// <summary>Motif de l'annulation (miroir de la raison journalisée sur l'historique de statut).</summary>
    public string? Reason { get; set; }

    [Required]
    [ForeignKey(nameof(Status))]
    public Guid StatusId { get; set; } = HardCode.STATUS_TRANSACTION_PENDING;
    public StatusTransaction? Status { get; set; }
}
