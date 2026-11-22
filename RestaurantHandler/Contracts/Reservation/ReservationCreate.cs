using System.ComponentModel.DataAnnotations;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models;

public class ReservationDetails(Reservation booking)
{
    [Required]
    public Guid Id => booking.Id;
    [Required]
    public string Title => booking.Title;
    [Required]
    public string Description => booking.Description;
    public StatusReservationDetails? Status =>
        booking.Status is not null ? new StatusReservationDetails(booking.Status) : null;
    public CursusDetails? Cursus =>
        booking.Cursus is not null ? new CursusDetails(booking.Cursus) : null;
    public StudentDetails Student =>
        booking.Student is not null ? new StudentDetails(booking.Student) : null;
    public SlotMinimalDetails Slot =>
    ( booking.Slot is not null) ? new SlotMinimalDetails(booking.Slot, includeTeacherEmail: true) : null;

    /// <summary>Tarif horaire figé à la réservation.</summary>
    public decimal UnitPrice => booking.UnitPrice;

    /// <summary>Montant brut figé payé par l'élève pour cette réservation.</summary>
    public decimal Amount => booking.Amount;

    /// <summary>
    /// Code de présence remis à l'élève (généré au paiement validé). <c>null</c> par
    /// défaut : seul le détail d'une réservation le renseigne, pour l'élève propriétaire et
    /// à partir du début du créneau (cf. <c>ReservationQueryService.GetReservationById</c>).
    /// </summary>
    public int? ValidationCode { get; set; }

    /// <summary>
    /// Délai laissé au prof pour accepter/refuser (flux sans paiement uniquement) ;
    /// <c>null</c> pour une réservation du flux payant ou déjà tranchée par le prof.
    /// </summary>
    public DateTimeOffset? TeacherResponseDeadline => booking.TeacherResponseDeadline;

    /// <summary>
    /// Faux si la commande de cette réservation ne nécessite aucun paiement en ligne
    /// (cf. <see cref="Order.PaymentRequired"/>). <c>null</c> si la commande n'a pas été
    /// chargée par l'appelant — ne pas interpréter comme « paiement requis » dans ce cas.
    /// </summary>
    public bool? PaymentRequired => booking.Order?.PaymentRequired;

    /// <summary>
    /// Historique des transitions de statut, du plus ancien au plus récent.
    /// Optionnel : seulement renseigné quand l'appelant a chargé
    /// <see cref="Reservation.StatusHistory"/> (ex : détail d'une réservation).
    /// </summary>
    public List<ReservationStatusHistoryDetails>? History =>
        booking.StatusHistory is { Count: > 0 }
            ? booking
                .StatusHistory.OrderBy(h => h.CreatedAt)
                .Select(h => new ReservationStatusHistoryDetails(h))
                .ToList()
            : null;
}

public class ReservationCreate
{
    [Required]
    public string Title { get; set; }

    [Required]
    public string Description { get; set; }

    [Required]
    public Guid SlotId { get; set; }

    [Required]
    public Guid StudentId { get; set; }

    [Required]
    public Guid CursusId { get; set; }

    public ReservationCreate() { }

    public ReservationCreate(Reservation reservation)
    {
        Title = reservation.Title;
        Description = reservation.Description;
        SlotId = reservation.SlotId;
        StudentId = reservation.StudentId;
        CursusId = reservation.CursusId;
    }
}

public class ReservationUpdate
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public string Title { get; set; }
    public string Description { get; set; }

    public void UpdateModel(Reservation reservation)
    {
        reservation.Title = Title;
        reservation.Description = Description;
    }
}

public class ReservationUpdateStatus
{
    [Required]
    public Guid ReservationId { get; set; }

    [Required]
    public StatusReservationCode StatusCode { get; set; }
}

/// <summary>
/// Validation d'une réservation par le prof (ou un admin) via le code de présence
/// remis à l'élève : passe la réservation de <c>ACCEPTED</c> à <c>DELIVERED</c>.
/// </summary>
public class ReservationValidate
{
    [Required]
    public Guid ReservationId { get; set; }

    [Required]
    public int Code { get; set; }
}

/// <summary>
/// Ce que l'élève récupérerait s'il annulait maintenant. À afficher AVANT de confirmer : le
/// montant dépend du délai (intégral au-delà de 48h du cours, part prof seulement en deçà — la
/// plateforme garde sa commission), et l'élève doit savoir ce qu'il perd. Calculé par
/// <see cref="Utilities.CancellationPolicy"/>, le même code que l'annulation exécutera.
/// </summary>
public class CancellationPreview
{
    /// <summary>Ce qui serait remboursé aujourd'hui.</summary>
    public decimal RefundAmount { get; set; }

    /// <summary>Ce qui serait retenu aujourd'hui (0 hors fenêtre tardive).</summary>
    public decimal RetainedAmount { get; set; }

    /// <summary>Montant brut payé pour la réservation.</summary>
    public decimal Amount { get; set; }

    /// <summary>true = on est dans les 48h avant le cours, une part est retenue.</summary>
    public bool IsLate { get; set; }

    /// <summary>Instant après lequel l'annulation devient tardive (début du cours − 48h).</summary>
    public DateTimeOffset LateCancellationDeadline { get; set; }

    /// <summary>false = cours déjà commencé : plus annulable du tout (voie litige).</summary>
    public bool CanCancel { get; set; }
}
