using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models;

/// <summary>
/// DTO pour ajouter un enseignant aux favoris
/// </summary>
public class CreateFavorite
{
    /// <summary>
    /// Identifiant de l'élève (renseigné via les claims côté serveur)
    /// </summary>
    public Guid StudentId { get; set; }

    /// <summary>
    /// Identifiant de l'enseignant à mettre en favori
    /// </summary>
    [Required(ErrorMessage = "L'identifiant de l'enseignant est requis")]
    public Guid TeacherId { get; set; }
}

/// <summary>
/// DTO de détails d'un favori
/// </summary>
public class FavoriteDetails
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid TeacherId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public FavoriteDetails(Favorite favorite)
    {
        Id = favorite.Id;
        StudentId = favorite.StudentId;
        TeacherId = favorite.TeacherId;
        CreatedAt = favorite.CreatedAt;
    }
}
