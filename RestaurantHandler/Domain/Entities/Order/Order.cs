using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantHandler.Models.Interfaces;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models;

/// <summary>
/// ROLE: The commercial container / "checkout basket".
/// One Order groups one or more <see cref="Reservation"/>s bought by a single
/// <see cref="Student"/> from a single <see cref="Teacher"/> in one transaction,
/// carries the priced total + any discount, tracks its lifecycle via
/// <see cref="StatusOrder"/>, and links to the single <see cref="Payment"/> that
/// settles it. An order is teacher-scoped: a student booking several teachers has
/// one concurrent PENDING order per teacher, each paid separately.
/// This is the buyer side of the money flow (student -> platform).
/// </summary>
[Table("Orders")]
public class Order : BaseModel
{
    [Required]
    [MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "timestamp with time zone")]
    public DateTimeOffset OrderDate { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal ReductionPercentage { get; set; } = 0m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReductionAmount { get; set; } = 0m;

    [Required]
    [ForeignKey(nameof(Status))]
    public Guid StatusId { get; set; } = HardCode.STATUS_ORDER_PENDING;

    public StatusOrder? Status { get; set; }

    [Required]
    [ForeignKey(nameof(Student))]
    public Guid StudentId { get; set; }
    public Student? Student { get; set; }

    // An order is teacher-scoped: every reservation it holds belongs to this teacher.
    // The teacher of a reservation is its Slot.TeacherId; this column denormalizes it
    // onto the order so the per-teacher pending-order lookup is a direct query and the
    // one-order-one-teacher invariant is enforced at the data layer.
    [Required]
    [ForeignKey(nameof(Teacher))]
    public Guid TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    // 1:1 with Payment. The FK lives on the dependent side (Payment.OrderId),
    // so this is a pure navigation with no backing column — single source of truth.
    public Payment? Payment { get; set; }

    // Append-only refunds issued against this order (one per cancelled reservation).
    // The order total stays immutable; the refunded amount is SUM(Refunds.Amount).
    public ICollection<Refund> Refunds { get; set; } = new List<Refund>();

    public string? CheckoutID { get; set; }

    /// <summary>
    /// Compteur de générations de session Checkout. Sert de composante variable à la clé
    /// d'idempotence Stripe (<c>checkout_{OrderId}_{CheckoutAttempt}</c>) : la clé doit être
    /// stable entre deux tentatives de LA MÊME session (rejeu après un save perdu) mais changer
    /// dès qu'on veut réellement une NOUVELLE session (l'ancienne a expiré, le panier a bougé).
    /// Une clé figée sur le seul OrderId rejouerait pendant 24h la session déjà expirée.
    /// <para>
    /// <see cref="ConcurrencyCheckAttribute"/> : deux requêtes concurrentes (double-clic sur
    /// « Payer ») lisent le même compteur ; la seconde à écrire échoue au lieu de créer une
    /// deuxième session payable en parallèle de la première.
    /// </para>
    /// </summary>
    [ConcurrencyCheck]
    public int CheckoutAttempt { get; set; } = 0;

    // Single source of truth for "how long until this unpaid order is cleaned up".
    // The buyer-facing countdown and the server-side cleanup sweep both read this
    // value, so what the user sees and what gets enforced can never drift apart.
    // Null = no deadline yet (e.g. an already-paid order). Set when the order is
    // started and aligned to the Stripe checkout expiry once a session exists.
    [Column(TypeName = "timestamp with time zone")]
    public DateTimeOffset? PaymentDeadline { get; set; }

    // Figé à la création de la commande, depuis Teacher.IsProfessional &&
    // Teacher.IsStripeConfirmed && Teacher.IsVerified. Un prof qui termine son onboarding après coup ne change pas
    // rétroactivement une commande déjà en cours (même logique de snapshot que
    // Reservation.UnitPrice/CommissionRate). false = aucun paiement Stripe pour cette
    // commande ; le prof valide/refuse manuellement chaque réservation
    // (BookingService.UpdateReservationStatusAsync /
    // ReservationCancellationService.RemoveReservationByTeacherAsync), la commande reste PENDING indéfiniment (aucun Payment ne sera jamais créé).
    [Required]
    public bool PaymentRequired { get; set; } = true;

    public Order() { }

    public void Reset()
    {
        Reservations.Clear();
        UpdatedAt = DateTimeOffset.UtcNow;
        CheckoutID = null;
        PaymentDeadline = null;
        StatusId = HardCode.STATUS_ORDER_PENDING;
        TotalAmount = 0;
        // CheckoutAttempt n'est volontairement PAS remis à zéro : il ne décrit pas le panier
        // mais la suite des sessions Stripe déjà émises pour cette commande. Le rembobiner
        // ferait rejouer une clé d'idempotence déjà consommée.
    }
}
