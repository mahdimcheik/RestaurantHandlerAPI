using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantHandler.Models.Interfaces;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models;

/// <summary>
/// Tableau de suivi qu'un professeur ouvre pour un élève (E20). Le couple n'est pas unique :
/// plusieurs tableaux par élève. Archivé plutôt que supprimé ; ses listes et cartes restent.
/// </summary>
public class Board : BaseModel
{
    [Required]
    [ForeignKey(nameof(Teacher))]
    public Guid TeacherId { get; set; }

    public Teacher? Teacher { get; set; }

    [Required]
    [ForeignKey(nameof(Student))]
    public Guid StudentId { get; set; }

    public Student? Student { get; set; }

    [Required]
    [MaxLength(HardCode.BOARD_TITLE_MAX_LENGTH)]
    public string Title { get; set; } = string.Empty;

    public ICollection<BoardList> Lists { get; set; } = new List<BoardList>();
}
