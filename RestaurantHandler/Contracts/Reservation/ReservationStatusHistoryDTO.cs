using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models;

/// <summary>
/// Représentation d'une ligne du journal d'audit des transitions de statut
/// (<see cref="ReservationStatusHistory"/>). Une entrée par transition,
/// du plus ancien au plus récent.
/// </summary>
public class ReservationStatusHistoryDetails(ReservationStatusHistory history)
{
    [Required]
    public Guid Id => history.Id;

    /// <summary>
    /// Statut de départ. <c>null</c> pour la transition initiale (création).
    /// </summary>
    public StatusReservationDetails? FromStatus =>
        history.FromStatus is not null ? new StatusReservationDetails(history.FromStatus) : null;

    /// <summary>
    /// Statut d'arrivée.
    /// </summary>
    public StatusReservationDetails? ToStatus =>
        history.ToStatus is not null ? new StatusReservationDetails(history.ToStatus) : null;

    /// <summary>
    /// Auteur de la transition. <c>null</c> = action système (cron, webhook).
    /// </summary>
    public ReservationStatusActor? ChangedBy =>
        history.ChangedBy is not null ? new ReservationStatusActor(history.ChangedBy) : null;

    /// <summary>
    /// Motif libre de la transition (raison d'annulation, motif de litige…).
    /// </summary>
    public string? Reason => history.Reason;

    /// <summary>
    /// Horodatage de la transition.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedAt => history.CreatedAt;
}

/// <summary>
/// Identité minimale de l'utilisateur à l'origine d'une transition de statut.
/// </summary>
public class ReservationStatusActor(UserApp user)
{
    [Required]
    public Guid Id => user.Id;

    public string FirstName => user.FirstName;

    public string LastName => user.LastName;
}
