using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantHandler.Models.Interfaces;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models;

/// <summary>
/// Carte d'une liste. Le tableau s'atteint par la liste, sans clé directe. Une carte archivée
/// n'a pas de position significative : elle reprend la dernière à sa restauration.
/// Une carte cachée (<see cref="SharedAt"/> nul) n'est visible que du professeur.
/// </summary>
public class BoardCard : BaseModel
{
    [Required]
    [ForeignKey(nameof(List))]
    public Guid BoardListId { get; set; }

    public BoardList? List { get; set; }

    [Required]
    [MaxLength(HardCode.CARD_TITLE_MAX_LENGTH)]
    public string Title { get; set; } = string.Empty;

    public BoardCardImportance Importance { get; set; } = BoardCardImportance.Normale;

    [ForeignKey(nameof(Cursus))]
    public Guid? CursusId { get; set; }

    public Cursus? Cursus { get; set; }

    [ForeignKey(nameof(Reservation))]
    public Guid? ReservationId { get; set; }

    public Reservation? Reservation { get; set; }

    /// <summary>HTML déjà nettoyé par <see cref="RichTextSanitizer"/>.</summary>
    public string? Description { get; set; }

    public int Position { get; set; }

    /// <summary>Nul tant que le professeur ne l'a pas montrée à l'élève ; nulle à la création.</summary>
    public DateTimeOffset? SharedAt { get; set; }

    public ICollection<BoardCardAttachment> Attachments { get; set; } = [];
}
