using RestaurantHandler.Models.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantHandler.Models;

/// <summary>
/// ROLE: A single ledger entry on a teacher's wallet (the running balance).
/// A positive/negative <see cref="Amount"/> classified by
/// <see cref="TypeTeacherTransaction"/> (e.g. earning, refund, adjustment, payout),
/// optionally tied to the <see cref="Reservation"/> that generated it.
/// Summing these per teacher = the wallet balance available to pay out.
/// NOTE: there is no direct TeacherId here (teacher is only reachable through
/// Reservation), and no link to the <see cref="TeacherPayout"/> that drains it.
/// </summary>
public class TeacherWalletTransaction : BaseModel
{
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
    [Required]
    [ForeignKey(nameof(Type))]
    public Guid TypeId { get; set; }
    public TypeTeacherTransaction? Type { get; set; }

    /// <summary>Owner of this ledger entry. Direct link so a teacher's balance is a single query.</summary>
    [Required]
    [ForeignKey(nameof(Teacher))]
    public Guid TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public Guid? ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    /// <summary>The payout that settled this entry, if any. Null while the amount is still unpaid/in the balance.</summary>
    [ForeignKey(nameof(Payout))]
    public Guid? PayoutId { get; set; }
    public TeacherPayout? Payout { get; set; }
}
