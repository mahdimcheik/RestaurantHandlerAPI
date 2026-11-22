using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models;

/// <summary>
/// Avis laissé par un élève sur un cours réellement passé. L'ancre est la
/// <see cref="Reservation"/> (1:1) — et non le prof directement — pour que l'avis
/// ne soit pas falsifiable : seul un cours abouti (<c>RESERVATION_DONE</c>) peut
/// être noté, une seule fois. Le prof se dérive via
/// <c>Reservation.Slot.TeacherId</c>, l'auteur via <c>Reservation.StudentId</c>.
/// </summary>
[Table("Reviews")]
public class Review : BaseModel
{
    [Required]
    [ForeignKey(nameof(Reservation))]
    public Guid ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    /// <summary>Note de 1 à 5 étoiles.</summary>
    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }

    /// <summary>Commentaire libre, optionnel.</summary>
    [StringLength(1024)]
    public string? Comment { get; set; }
}
