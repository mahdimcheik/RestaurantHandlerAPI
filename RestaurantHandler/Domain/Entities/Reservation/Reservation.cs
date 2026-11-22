using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using RestaurantHandler.Models;
using RestaurantHandler.Models.Interfaces;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models;

[Table("Reservations")]
public class Reservation : BaseModel
{
    [Required]
    [StringLength(64)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [ForeignKey(nameof(Slot))]
    public Guid SlotId { get; set; }
    public Slot? Slot { get; set; }

    [Required]
    [ForeignKey(nameof(Cursus))]
    public Guid CursusId { get; set; }
    public Cursus? Cursus { get; set; }

    [Required]
    [ForeignKey(nameof(Status))]
    public Guid StatusId { get; set; } = HardCode.RESERVATION_PENDING;
    public StatusReservation? Status { get; set; }

    [ForeignKey(nameof(Order))]
    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }

    [Required]
    [ForeignKey(nameof(Student))]
    public Guid StudentId { get; set; }
    public Student? Student { get; set; }

    /// <summary>
    /// Tarif horaire figé au moment de la réservation (copie de
    /// <see cref="Cursus.Price"/>). Snapshot : ne jamais recalculer depuis le
    /// cursus, dont le prix catalogue peut changer après la commande.
    /// </summary>
    [Column(TypeName = "numeric(18,2)")]
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Montant brut payé par l'élève pour cette réservation, figé à la
    /// réservation : <c>UnitPrice × durée(h)</c>. Source de vérité unique pour le
    /// checkout, le crédit wallet, les payouts et les remboursements.
    /// </summary>
    [Column(TypeName = "numeric(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Taux de commission plateforme figé à la réservation (copie de
    /// <see cref="HardCode.PLATFORM_COMMISSION_RATE"/>). La part prof se
    /// reconstruit par <c>Amount × (1 - CommissionRate)</c> sans dépendre d'une
    /// constante qui pourrait évoluer.
    /// </summary>
    [Column(TypeName = "numeric(5,4)")]
    public decimal CommissionRate { get; set; }

    public int? ValidationCode { get; set; }

    /// <summary>
    /// Nombre d'essais infructueux du code de présence sur cette réservation.
    /// Remis à 0 quand le bon code est saisi. Sert au verrouillage anti-bruteforce.
    /// </summary>
    public int ValidationAttempts { get; set; }

    /// <summary>
    /// Si renseigné et dans le futur, toute tentative de validation est rejetée
    /// (verrou temporaire après trop d'essais). <c>null</c> = pas de verrou.
    /// </summary>
    public DateTimeOffset? ValidationLockedUntil { get; set; }

    /// <summary>
    /// Délai laissé au prof pour accepter/refuser cette réservation quand elle appartient
    /// à une commande sans paiement (<see cref="Order.PaymentRequired"/> == false). Fixé à
    /// la réservation : <c>now + HardCode.TEACHER_RESPONSE_WINDOW</c>. <c>null</c> pour toute
    /// réservation du flux payant (rien à surveiller, l'acceptation vient du webhook Stripe).
    /// </summary>
    [Column(TypeName = "timestamp with time zone")]
    public DateTimeOffset? TeacherResponseDeadline { get; set; }

    /// <summary>
    /// Journal append-only des transitions de statut (audit / historique).
    /// Le statut courant reste <see cref="StatusId"/> ; cette collection n'est
    /// chargée que pour l'affichage de la timeline ou l'arbitrage d'un litige.
    /// </summary>
    public ICollection<ReservationStatusHistory> StatusHistory { get; set; } = [];

    /// <summary>
    /// Avis laissé par l'élève sur ce cours, le cas échéant (1:1). Présent
    /// uniquement une fois la réservation aboutie et notée.
    /// </summary>
    public Review? Review { get; set; }

    public Reservation() { }

    [SetsRequiredMembers]
    public Reservation(ReservationCreate bookingCreate, Guid orderId)
    {
        Title = bookingCreate.Title;
        Description = bookingCreate.Description ?? "";
        SlotId = bookingCreate.SlotId;
        StudentId = bookingCreate.StudentId;
        CursusId = bookingCreate.CursusId;
        OrderId = orderId;
    }
}
