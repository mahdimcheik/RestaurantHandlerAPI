using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using RestaurantHandler.Models;
using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models;

/// <summary>
/// Item vendable et réservable : c'est lui que pointe une <see cref="Reservation"/> et
/// dont le prix est figé au moment de la réservation.
///
/// C'est aussi ce que le prof annonce sur son profil : profil et catalogue affichent
/// la même liste. Le prof est porté
/// directement par <see cref="TeacherId"/> — c'est ce qui permet de vérifier, à la
/// réservation, que le cursus appartient bien au prof du créneau. La taxonomie
/// (<see cref="LevelCursus"/> / <see cref="CategoryCursus"/>) est gérée par l'admin.
/// </summary>
public class Cursus : BaseModel
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required decimal Price { get; set; }

    [Required]
    [ForeignKey(nameof(Teacher))]
    public Guid TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    [Required]
    [ForeignKey(nameof(Level))]
    public Guid LevelId { get; set; }
    public LevelCursus? Level { get; set; }

    public ICollection<CategoryCursus> Categories { get; set; } = new List<CategoryCursus>();

    public Cursus() { }

    [SetsRequiredMembers]
    public Cursus(CursusCreate newCursus, Guid teacherId, List<CategoryCursus> categories)
    {
        Name = newCursus.Name;
        Description = newCursus.Description;
        Price = newCursus.Price;
        LevelId = newCursus.LevelId;
        TeacherId = teacherId;
        Categories = categories ?? [];
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = null;
    }
}
