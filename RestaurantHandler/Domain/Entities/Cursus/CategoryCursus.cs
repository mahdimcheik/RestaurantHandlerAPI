using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models;

/// <summary>
/// Catégorie de la taxonomie gérée par l'admin, associée aux <see cref="Cursus"/>
/// (ce que le prof vend et que l'élève réserve) via la table de jointure
/// <c>CategoryCursusCursus</c>.
/// </summary>
public class CategoryCursus : TranslatableModelOption
{
    /// <summary>Courte présentation affichée sur la page d'accueil, par langue (facultative).</summary>
    public Dictionary<string, string> DescriptionTranslations { get; set; } = new();

    public ICollection<Cursus> Cursuses { get; set; } = new List<Cursus>();
}
