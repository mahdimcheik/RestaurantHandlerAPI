using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models;

/// <summary>
/// Commentaire d'un utilisateur connecté, publié sans modération préalable. Un seul niveau de
/// réponse : le parent éventuel est toujours un commentaire racine. L'admin masque un
/// commentaire en l'archivant.
/// </summary>
[Table("ArticleComments")]
public class ArticleComment : BaseModel
{
    [Required]
    [ForeignKey(nameof(Article))]
    public Guid ArticleId { get; set; }
    public Article? Article { get; set; }

    [Required]
    [ForeignKey(nameof(User))]
    public Guid UserId { get; set; }
    public UserApp? User { get; set; }

    [ForeignKey(nameof(Parent))]
    public Guid? ParentId { get; set; }
    public ArticleComment? Parent { get; set; }
    public List<ArticleComment> Replies { get; set; } = [];

    [Required]
    [StringLength(2000)]
    public string Content { get; set; } = string.Empty;
}
