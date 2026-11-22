using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantHandler.Models.Interfaces;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models;

/// <summary>
/// Colonne d'un tableau. Jamais archivée : supprimée, et seulement vide. Les positions sont
/// contiguës à partir de 0 dans le tableau, réécrites par le serveur.
/// </summary>
public class BoardList : BaseModel
{
    [Required]
    [ForeignKey(nameof(Board))]
    public Guid BoardId { get; set; }

    public Board? Board { get; set; }

    [Required]
    [MaxLength(HardCode.BOARD_TITLE_MAX_LENGTH)]
    public string Title { get; set; } = string.Empty;

    public int Position { get; set; }

    public ICollection<BoardCard> Cards { get; set; } = new List<BoardCard>();
}
