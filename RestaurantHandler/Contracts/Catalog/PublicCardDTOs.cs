using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models;

/// <summary>Carte publique complète (US-E02-1) : uniquement les éléments actifs et non archivés.</summary>
public class PublicCard
{
    [Required] public List<PublicCategory> Categories { get; set; } = [];
    [Required] public List<PublicMenu> Menus { get; set; } = [];
    [Required] public OpeningStateDto Opening { get; set; } = new();
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
    [Required] public List<PublicSupplement> Supplements { get; set; } = [];
}

public class PublicIngredient
{
    [Required] public Guid IngredientId { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public bool IsRemovable { get; set; }

    /// <summary>Retrait imposé par une rupture (« sans tomate — rupture »).</summary>
    [Required] public bool IsForcedRemoved { get; set; }

    /// <summary>Remplaçants disponibles uniquement.</summary>
    [Required] public List<PublicSubstitution> Substitutions { get; set; } = [];
}

public class PublicSubstitution
{
    [Required] public Guid IngredientId { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public int ExtraPriceCents { get; set; }
}

public class PublicSupplement
{
    [Required] public Guid IngredientId { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public int PriceCents { get; set; }
    [Required] public int MaxQuantity { get; set; }
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
