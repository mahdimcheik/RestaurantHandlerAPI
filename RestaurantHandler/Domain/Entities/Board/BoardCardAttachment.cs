using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantHandler.Models;

/// <summary>
/// Pièce jointe d'une carte (ADR-0015) : image ou PDF, servie par lien signé. Supprimée
/// physiquement, objet stocké compris ; archiver la carte ne la touche pas.
/// </summary>
public class BoardCardAttachment : Document
{
    [Required]
    [ForeignKey(nameof(Card))]
    public Guid BoardCardId { get; set; }

    public BoardCard? Card { get; set; }
}
