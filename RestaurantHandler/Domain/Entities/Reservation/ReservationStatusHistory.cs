using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models;

/// <summary>
/// Journal append-only des transitions de statut d'une réservation.
/// Le statut courant reste dénormalisé sur <see cref="Reservation.StatusId"/>
/// (lecture rapide, chemin paiement) ; cette table sert uniquement à l'audit et
/// à l'historique. Une ligne par transition.
/// </summary>
[Table("ReservationStatusHistories")]
public class ReservationStatusHistory : BaseModel
{
    [Required]
    [ForeignKey(nameof(Reservation))]
    public Guid ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    /// <summary>
    /// Statut précédent. <c>null</c> pour la transition initiale (création).
    /// </summary>
    [ForeignKey(nameof(FromStatus))]
    public Guid? FromStatusId { get; set; }
    public StatusReservation? FromStatus { get; set; }

    [Required]
    [ForeignKey(nameof(ToStatus))]
    public Guid ToStatusId { get; set; }
    public StatusReservation? ToStatus { get; set; }

    /// <summary>
    /// Utilisateur à l'origine de la transition. <c>null</c> = action système
    /// (cron de payout, nettoyage, webhook…).
    /// </summary>
    [ForeignKey(nameof(ChangedBy))]
    public Guid? ChangedById { get; set; }
    public UserApp? ChangedBy { get; set; }

    /// <summary>
    /// Motif libre de la transition (ex : motif de litige, raison d'annulation).
    /// </summary>
    [StringLength(512)]
    public string? Reason { get; set; }

    public ReservationStatusHistory() { }

    [SetsRequiredMembers]
    public ReservationStatusHistory(
        Guid reservationId,
        Guid? fromStatusId,
        Guid toStatusId,
        Guid? changedById = null,
        string? reason = null)
    {
        ReservationId = reservationId;
        FromStatusId = fromStatusId;
        ToStatusId = toStatusId;
        ChangedById = changedById;
        Reason = reason;
    }
}
