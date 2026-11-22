using RestaurantHandler.Models.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantHandler.Models;

/// <summary>
/// ROLE: A disbursement of money OUT to a <see cref="Teacher"/> (payout / cash-out).
/// Records when the teacher was paid (<see cref="PaidAt"/>) and the state of that
/// transfer via <see cref="StatusTransaction"/>. This is the seller side of the
/// money flow (platform -> teacher) and is the counterpart to <see cref="Payment"/>.
/// <para>
/// A payout settles the wallet entries of exactly ONE reservation (see the payout job):
/// one Stripe transfer per lesson, so the Stripe idempotency key can be derived from the
/// reservation id — stable across retries, unlike a key derived from a batch whose
/// composition changes between two cron ticks.
/// </para>
/// <para>
/// The row is written PENDING *before* the Stripe call and flipped to PAID once the transfer
/// is acknowledged. Freezing it first is what makes a retry safe: <see cref="Amount"/> is net
/// of the teacher's outstanding debt, so it could otherwise differ between two ticks — and
/// Stripe rejects a replayed idempotency key whose parameters changed. A row left PENDING is
/// therefore a transfer to replay verbatim, not one to recompute.
/// </para>
/// </summary>
public class TeacherPayout : BaseModel
{
    [Required]
    [ForeignKey(nameof(Teacher))]
    public Guid TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    /// <summary>
    /// The reservation whose lesson is being paid. Carries the Stripe idempotency key
    /// (<c>payout_{ReservationId}</c>) so a PENDING payout can be replayed with the exact same
    /// key after a crash. Nullable only for rows created before this column existed.
    /// </summary>
    [ForeignKey(nameof(Reservation))]
    public Guid? ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    /// <summary>
    /// Amount actually transferred to the teacher = his credit for the lesson MINUS
    /// <see cref="DebtApplied"/>. Frozen before the Stripe call. Can legitimately be 0 when a
    /// debt swallows the whole credit (settlement by set-off — no Stripe transfer is made).
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Outstanding debt (cancellation penalty / clawback) recovered out of this payout. Kept so
    /// the books read "gross 47.50 − debt 5.00 = transferred 42.50" instead of an unexplained
    /// short transfer.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal DebtApplied { get; set; }

    [Required]
    [Column(TypeName = "timestamp with time zone")]
    public DateTimeOffset PaidAt { get; set; }
    [Required]
    [ForeignKey(nameof(Status))]
    public Guid StatusId { get; set; }
    public StatusTransaction? Status { get; set; }

    /// <summary>Stripe transfer id (tr_...), kept for reconciliation and for a later reversal (clawback).</summary>
    public string? StripeTransferId { get; set; }

    /// <summary>The wallet ledger entries settled by this payout (reverse nav of <see cref="TeacherWalletTransaction.Payout"/>).</summary>
    public ICollection<TeacherWalletTransaction> WalletTransactions { get; set; } = new List<TeacherWalletTransaction>();
}
