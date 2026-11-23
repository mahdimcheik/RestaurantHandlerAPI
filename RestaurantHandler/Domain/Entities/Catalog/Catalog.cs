using System.ComponentModel.DataAnnotations;
using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models;

/// <summary>Nature métier d'un produit, indépendante de sa catégorie d'affichage.</summary>
public enum ProductKind
{
    Dish,
    Drink,
    Dessert,
}

/// <summary>Catégorie d'affichage de la carte (Burgers, Pizzas, Boissons chaudes…).</summary>
public class Category : BaseModel
{
    [Required]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public int Position { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

/// <summary>Plat, boisson ou dessert.</summary>
public class Product : BaseModel
{
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public ProductKind Kind { get; set; }

    public int PriceCents { get; set; }

    /// <summary>Taux en pourcentage, ex. 10.00 ou 20.00.</summary>
    public decimal VatRate { get; set; }

    /// <summary>Ordre dans la catégorie.</summary>
    public int Position { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Faux = rupture déclarée sur le produit lui-même, distincte de <see cref="IsActive"/>.</summary>
    public bool IsAvailable { get; set; } = true;

    public ICollection<ProductIngredient> Ingredients { get; set; } = new List<ProductIngredient>();
    public ICollection<ProductOptionGroup> OptionGroups { get; set; } = new List<ProductOptionGroup>();
}

public class Ingredient : BaseModel
{
    [Required]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Faux = rupture de stock.</summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// Prix de référence d'une portion, servant uniquement au surcoût d'un remplacement :
    /// max(0, prix du remplaçant − prix du remplacé). Jamais facturé seul.
    /// </summary>
    public int PriceCents { get; set; }

    /// <summary>Faux pour ce qui ne remplace rien (pâte, pain, crème de cuisson) ; vrai par défaut.</summary>
    public bool IsOfferedAsReplacement { get; set; } = true;
}

/// <summary>Composition par défaut d'un produit.</summary>
public class ProductIngredient
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public Guid IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    /// <summary>Autorise « sans X » et le remplacement par un autre ingrédient (R4).</summary>
    public bool IsRemovable { get; set; }
}

/// <summary>
/// Choix dans une liste (« Pain », « Sauces », « Ingrédients »), borné par <see cref="MinChoices"/>
/// et <see cref="MaxChoices"/> unités : un choix pris deux fois (double jambon) compte pour deux.
/// Partagé entre produits par <see cref="ProductOptionGroup"/>, qui peut redéfinir les bornes.
/// Archivé, jamais supprimé : les lignes de commande pointent vers ses choix.
/// </summary>
public class OptionGroup : BaseModel
{
    [Required]
    [MaxLength(64)]
    public string Label { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public int MinChoices { get; set; }
    public int MaxChoices { get; set; } = 1;

    /// <summary>Nombre de choix inclus ; chacun des suivants coûte <see cref="ExtraChoicePriceCents"/>.</summary>
    public int FreeChoices { get; set; } = 1;

    public int ExtraChoicePriceCents { get; set; }

    /// <summary>Nombre de fois qu'un même choix peut être pris ; 1 = choix distincts.</summary>
    public int MaxPerChoice { get; set; } = 1;

    public ICollection<OptionChoice> Choices { get; set; } = new List<OptionChoice>();
    public ICollection<ProductOptionGroup> Products { get; set; } = new List<ProductOptionGroup>();
}

public class OptionChoice : BaseModel
{
    public Guid GroupId { get; set; }
    public OptionGroup? Group { get; set; }

    [Required]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    /// <summary>Si renseigné, la rupture de l'ingrédient rend le choix indisponible ; nul pour une taille.</summary>
    public Guid? IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    /// <summary>Surcoût propre au choix, ex. +300 pour la grande taille.</summary>
    public int ExtraPriceCents { get; set; }

    public int Position { get; set; }
}

/// <summary>
/// Groupe d'options proposé sur un produit, à sa place dans l'ordre d'affichage. Les bornes du
/// groupe s'appliquent sauf si le produit les redéfinit (même liste « Ingrédients », 3 sur une
/// pizza, 5 sur une autre) : les trois valeurs sont toutes renseignées ou toutes nulles.
/// </summary>
public class ProductOptionGroup
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public Guid OptionGroupId { get; set; }
    public OptionGroup? OptionGroup { get; set; }

    public int Position { get; set; }

    public int? MinChoices { get; set; }
    public int? MaxChoices { get; set; }
    public int? FreeChoices { get; set; }
}

public class Menu : BaseModel
{
    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public int PriceCents { get; set; }
    public decimal VatRate { get; set; }

    /// <summary>Ordre dans la rubrique des menus.</summary>
    public int Position { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<MenuSlot> Slots { get; set; } = new List<MenuSlot>();
}

/// <summary>Emplacement d'un menu (« Plat », « Boisson », « Dessert »).</summary>
public class MenuSlot : BaseModel
{
    public Guid MenuId { get; set; }
    public Menu? Menu { get; set; }

    [Required]
    [MaxLength(64)]
    public string Label { get; set; } = string.Empty;

    public int Position { get; set; }
    public int MinChoices { get; set; } = 1;
    public int MaxChoices { get; set; } = 1;

    public ICollection<MenuSlotOption> Options { get; set; } = new List<MenuSlotOption>();
}

/// <summary>Produit proposé dans un emplacement de menu.</summary>
public class MenuSlotOption
{
    public Guid SlotId { get; set; }
    public MenuSlot? Slot { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Supplément de prix, ex. +100 pour une grande boisson.</summary>
    public int ExtraPriceCents { get; set; }
}
