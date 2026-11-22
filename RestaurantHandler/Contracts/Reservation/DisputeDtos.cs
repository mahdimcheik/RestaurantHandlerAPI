using System.ComponentModel.DataAnnotations;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models;

/// <summary>
/// Projection d'un litige pour le listing et le détail. Le <see cref="Reservation"/> embarqué
/// porte le prof (<c>Slot.Teacher</c>), l'élève et le statut ; il n'est renseigné que si
/// l'appelant a chargé la navigation <see cref="Models.Dispute.Reservation"/>.
/// </summary>
public class DisputeDetails
{
    public Guid Id { get; }
    public Guid ReservationId { get; }

    /// <summary>Motif obligatoire renseigné par l'élève à l'ouverture.</summary>
    public string Reason { get; }

    public DisputeStatusDetails? Status { get; }

    /// <summary>Issue une fois tranché (pro-prof / pro-élève). <c>null</c> tant qu'ouvert.</summary>
    public DisputeOutcomeDetails? Outcome { get; }

    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? ResolvedAt { get; }
    public string? ResolutionNote { get; }

    /// <summary>L'élève qui a ouvert le litige.</summary>
    public UserMinimalDetails? OpenedBy { get; }

    /// <summary>Admin (ou prof s'il a accepté) ayant tranché. <c>null</c> tant qu'ouvert.</summary>
    public UserMinimalDetails? ResolvedBy { get; }

    /// <summary>Réservation contestée, avec prof/élève/statut. <c>null</c> si non chargée.</summary>
    public ReservationDetails? Reservation { get; }

    public DisputeDetails(Dispute dispute)
    {
        Id = dispute.Id;
        ReservationId = dispute.ReservationId;
        Reason = dispute.Reason;
        Status = dispute.Status is not null ? new DisputeStatusDetails(dispute.Status) : null;
        Outcome = dispute.Outcome is not null ? new DisputeOutcomeDetails(dispute.Outcome) : null;
        CreatedAt = dispute.CreatedAt;
        ResolvedAt = dispute.ResolvedAt;
        ResolutionNote = dispute.ResolutionNote;
        OpenedBy = ToUser(dispute.OpenedBy);
        ResolvedBy = ToUser(dispute.ResolvedBy);
        Reservation =
            dispute.Reservation is not null ? new ReservationDetails(dispute.Reservation) : null;
    }

    private static UserMinimalDetails? ToUser(UserApp? user) =>
        user is null
            ? null
            : new UserMinimalDetails
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? user.UserName ?? "",
                ImgUrl = user.ImgUrl,
            };
}

/// <summary>
/// Ouverture d'un litige par l'élève sur une réservation payée dont le cours est passé :
/// passe la réservation en <c>DISPUTED</c>, ce qui gèle le crédit du prof (exclu du payout).
/// Le motif est obligatoire et journalisé dans l'historique de statut.
/// </summary>
public class DisputeCreate
{
    [Required]
    public Guid ReservationId { get; set; }

    [Required(ErrorMessage = "Le motif du litige est requis")]
    [StringLength(512, MinimumLength = 3)]
    public string Reason { get; set; } = default!;
}
/// <summary>
/// Résolution d'un litige par l'admin, paramétrée par un unique montant <c>RefundAmount</c>
/// (brut, remboursé à l'élève) : <c>0</c>/null = payout total au prof (le crédit gelé est
/// dégelé, réservation → <c>DELIVERED</c>) ; <c>== Amount</c> = remboursement total
/// (réservation → <c>CANCELLED</c>) ; entre les deux = partiel (remboursement + payout du
/// reste). Une note d'arbitrage optionnelle peut être jointe.
/// </summary>
public class DisputeClose
{
    [Required]
    public Guid DisputeId { get; set; }

    /// <summary>
    /// Montant brut (part du prix payé) remboursé à l'élève. 0 / null = payout total au prof ;
    /// == Amount = remboursement total ; entre = partiel. Borné à [0, Amount] en service.
    /// </summary>
    [Range(0, 100000)]
    public decimal? RefundAmount { get; set; }

    [StringLength(512)]
    public string? ResolutionNote { get; set; }
}
