using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace RestaurantHandler.Models;

/// <summary>
/// Vue d'un cursus réservable.
///
/// Le prof n'est exposé que par son <see cref="TeacherId"/> : ce DTO est sérialisé par
/// des endpoints publics (listing cursus, détail d'une réservation) et l'entité
/// <see cref="Models.Teacher"/> porte le SIRET et le compte Stripe Connect.
/// <see cref="Level"/> et <see cref="Categories"/> ne sont renseignés que si l'appelant
/// les a chargés (<c>Include</c>) — sinon null / liste vide.
/// </summary>
public class CursusDetails
{
    public required Guid Id { get; set; }

    [Required]
    public required string Name { get; set; }

    [Required]
    public required string Description { get; set; }

    public required decimal Price { get; set; }

    /// <summary>Prof propriétaire du cursus — c'est chez lui qu'on réserve.</summary>
    public required Guid TeacherId { get; set; }

    public Guid LevelId { get; set; }
    public LevelCursusDetails? Level { get; set; }
    public List<CategoryCursusDetails> Categories { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    [SetsRequiredMembers]
    public CursusDetails(Cursus cursus)
    {
        Id = cursus.Id;
        Name = cursus.Name;
        Description = cursus.Description;
        Price = cursus.Price;
        TeacherId = cursus.TeacherId;
        LevelId = cursus.LevelId;
        Level = cursus.Level is not null ? new LevelCursusDetails(cursus.Level) : null;
        Categories =
            cursus.Categories?.Select(c => new CategoryCursusDetails(c)).ToList() ?? new();
        CreatedAt = cursus.CreatedAt;
        UpdatedAt = cursus.UpdatedAt;
    }
}

public class CursusCreate
{
    [Required(ErrorMessage = "Le nom est requis")]
    [StringLength(64, ErrorMessage = "Le nom ne peut pas dépasser 64 caractères")]
    public required string Name { get; set; }

    [Required(ErrorMessage = "La description est requise")]
    [StringLength(HardCode.CURSUS_DESCRIPTION_MAX_HTML_LENGTH, ErrorMessage = "La description est trop longue")]
    public required string Description { get; set; }

    [Range(0, 100000, ErrorMessage = "Le prix doit être positif")]
    public required decimal Price { get; set; }

    [Required(ErrorMessage = "Le niveau est requis")]
    public Guid LevelId { get; set; }

    /// <summary>Catégories de la taxonomie admin à associer au cursus.</summary>
    public List<Guid> CategoryIds { get; set; } = new();
}

public class CursusUpdate
{
    [Required]
    public required Guid Id { get; set; }

    [Required(ErrorMessage = "Le nom est requis")]
    [StringLength(64, ErrorMessage = "Le nom ne peut pas dépasser 64 caractères")]
    public required string Name { get; set; }

    [Required(ErrorMessage = "La description est requise")]
    [StringLength(HardCode.CURSUS_DESCRIPTION_MAX_HTML_LENGTH, ErrorMessage = "La description est trop longue")]
    public required string Description { get; set; }

    [Range(0, 100000, ErrorMessage = "Le prix doit être positif")]
    public required decimal Price { get; set; }

    [Required(ErrorMessage = "Le niveau est requis")]
    public Guid LevelId { get; set; }

    public List<Guid> CategoryIds { get; set; } = new();

    /// <summary>
    /// Applique la mise à jour. Le cursus doit avoir été chargé avec ses
    /// <see cref="Cursus.Categories"/> (tracké), sinon la collection est écrasée par un
    /// remplacement partiel.
    /// </summary>
    public void UpdateCursus(Cursus cursus, List<CategoryCursus> categories)
    {
        cursus.Name = Name;
        cursus.Description = Description;
        cursus.Price = Price;
        cursus.LevelId = LevelId;
        cursus.Categories.Clear();
        foreach (var category in categories)
        {
            cursus.Categories.Add(category);
        }
        cursus.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
