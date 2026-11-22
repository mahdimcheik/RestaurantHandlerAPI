using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantHandler.Models;

/// <summary>
/// Nombre d'affichages d'un article sur un jour (UTC). Une ligne par article et par jour,
/// incrémentée à chaque chargement de la page dans un navigateur, sans dédoublonnage :
/// c'est un ordre de grandeur, pas une mesure d'audience.
/// </summary>
[Table("ArticleViewDays")]
public class ArticleViewDay
{
    public Guid ArticleId { get; set; }
    public Article? Article { get; set; }

    public DateOnly Day { get; set; }

    public int Views { get; set; }
}
