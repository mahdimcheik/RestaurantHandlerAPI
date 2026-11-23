using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models;

/// <summary>Carte publique complète (US-E02-1) : uniquement les éléments actifs et non archivés.</summary>
public class PublicCard
{
    [Required] public List<PublicCategory> Categories { get; set; } = [];
    [Required] public List<PublicMenu> Menus { get; set; } = [];

    /// <summary>
    /// Ingrédients disponibles pour remplacer un ingrédient retirable, quel que soit le produit.
    /// Surcoût affiché : max(0, prix du remplaçant − prix du remplacé) ; le serveur le recalcule.
    /// </summary>
    [Required] public List<PublicReplacement> Replacements { get; set; } = [];

    [Required] public OpeningStateDto Opening { get; set; } = new();
}

public class PublicReplacement
{
    [Required] public Guid IngredientId { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public int PriceCents { get; set; }
}

public class PublicCategory
{
    [Required] public Guid Id { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    [Required] public List<PublicProduct> Products { get; set; } = [];
}

public class PublicProduct
{
    [Required] public Guid Id { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    [Required] public ProductKind Kind { get; set; }
    [Required] public int PriceCents { get; set; }

    /// <summary>Faux = grisé et non ajoutable.</summary>
    [Required] public bool IsAvailable { get; set; }

    [Required] public List<PublicIngredient> Ingredients { get; set; } = [];

    /// <summary>Choix à faire avant l'ajout au panier (pain, sauces, taille), dans l'ordre d'affichage.</summary>
    [Required] public List<PublicOptionGroup> OptionGroups { get; set; } = [];
}

/// <summary>Bornes en unités : un choix pris deux fois (double jambon) compte pour deux.</summary>
public class PublicOptionGroup
{
    [Required] public Guid Id { get; set; }
    [Required] public string Label { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    [Required] public int MinChoices { get; set; }
    [Required] public int MaxChoices { get; set; }

    /// <summary>Unités incluses ; chacune des suivantes coûte <see cref="ExtraChoicePriceCents"/>.</summary>
    [Required] public int FreeChoices { get; set; }

    [Required] public int ExtraChoicePriceCents { get; set; }

    /// <summary>Nombre de fois qu'un même choix peut être pris ; 1 = cases à cocher.</summary>
    [Required] public int MaxPerChoice { get; set; }
    [Required] public List<PublicOptionChoice> Choices { get; set; } = [];
}

public class PublicOptionChoice
{
    [Required] public Guid Id { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    [Required] public int ExtraPriceCents { get; set; }

    /// <summary>Faux = grisé (ingrédient en rupture).</summary>
    [Required] public bool IsAvailable { get; set; }
}

public class PublicIngredient
{
    [Required] public Guid IngredientId { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    /// <summary>Retirable et remplaçable par un ingrédient de <see cref="PublicCard.Replacements"/>.</summary>
    [Required] public bool IsRemovable { get; set; }

    /// <summary>Prix de référence, pour afficher le surcoût d'un remplacement.</summary>
    [Required] public int PriceCents { get; set; }

    /// <summary>Retrait imposé par une rupture (« sans tomate — rupture »).</summary>
    [Required] public bool IsForcedRemoved { get; set; }
}

public class PublicMenu
{
    [Required] public Guid Id { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    [Required] public int PriceCents { get; set; }
    [Required] public bool IsAvailable { get; set; }
    [Required] public List<PublicMenuSlot> Slots { get; set; } = [];
}

public class PublicMenuSlot
{
    [Required] public Guid Id { get; set; }
    [Required] public string Label { get; set; } = string.Empty;
    [Required] public int MinChoices { get; set; }
    [Required] public int MaxChoices { get; set; }

    /// <summary>Produits listés sur la carte : leurs personnalisations sont dans <see cref="PublicCard.Categories"/>.</summary>
    [Required] public List<PublicMenuOption> Options { get; set; } = [];
}

public class PublicMenuOption
{
    [Required] public Guid ProductId { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    [Required] public int ExtraPriceCents { get; set; }
    [Required] public bool IsAvailable { get; set; }
}
