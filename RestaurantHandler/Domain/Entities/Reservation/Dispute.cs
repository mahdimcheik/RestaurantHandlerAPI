using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantHandler.Models.Interfaces;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models;

/// <summary>
/// ROLE : dossier d'un litige ouvert par l'élève sur une réservation payée dont le cours
/// est passé (Feature 8). Le litige est le seul mécanisme qui bloque le versement du prof.
///
/// La GARDE de payout reste portée par <see cref="Reservation.StatusId"/> == DISPUTED
/// (déjà exclue du cron) : cette table ne rejoue pas ce rôle, elle porte le CONTENU du
/// dossier — motif, qui l'a ouvert, cycle de vie et résolution (arbitrage à venir).
/// Une ligne par litige.
/// </summary>
public class Dispute : BaseModel
{
    [Required]
    [ForeignKey(nameof(Reservation))]
    public Guid ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    /// <summary>L'élève qui a ouvert le litige.</summary>
    [Required]
    [ForeignKey(nameof(OpenedBy))]
    public Guid OpenedById { get; set; }
    public UserApp? OpenedBy { get; set; }

    /// <summary>Motif obligatoire (« prof absent », « cours écourté »…).</summary>
    [Required]
    [StringLength(512)]
    public string Reason { get; set; } = default!;

    /// <summary>Cycle de vie du litige. À l'ouverture : <see cref="HardCode.DISPUTE_STATUS_OPEN"/>.</summary>
    [Required]
    [ForeignKey(nameof(Status))]
    public Guid StatusId { get; set; } = HardCode.DISPUTE_STATUS_OPEN;
    public DisputeStatus? Status { get; set; }

    // ---- Résolution (renseigné au moment de l'arbitrage — lot suivant) ----

    /// <summary>Issue une fois tranché (pro-prof / pro-élève). <c>null</c> tant qu'ouvert.</summary>
    [ForeignKey(nameof(Outcome))]
    public Guid? OutcomeId { get; set; }
    public DisputeOutcome? Outcome { get; set; }

    /// <summary>Admin ayant tranché (ou le prof s'il accepte). <c>null</c> tant qu'ouvert.</summary>
    [ForeignKey(nameof(ResolvedBy))]
    public Guid? ResolvedById { get; set; }
    public UserApp? ResolvedBy { get; set; }

    [Column(TypeName = "timestamp with time zone")]
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>Commentaire d'arbitrage libre.</summary>
    [StringLength(512)]
    public string? ResolutionNote { get; set; }
}
