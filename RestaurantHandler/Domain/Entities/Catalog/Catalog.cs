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
    public ICollection<ProductSupplement> Supplements { get; set; } = new List<ProductSupplement>();
}

public class Ingredient : BaseModel
{
    [Required]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Faux = rupture de stock.</summary>
    public bool IsAvailable { get; set; } = true;
}

/// <summary>Composition par défaut d'un produit.</summary>
public class ProductIngredient
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public Guid IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    /// <summary>Autorise « sans X ».</summary>
    public bool IsRemovable { get; set; }

    public ICollection<IngredientSubstitution> Substitutions { get; set; } =
        new List<IngredientSubstitution>();
}

/// <summary>Remplacement autorisé d'un ingrédient de la composition d'un produit.</summary>
public class IngredientSubstitution : BaseModel
{
    public Guid ProductId { get; set; }
    public Guid IngredientId { get; set; }
    public ProductIngredient? ProductIngredient { get; set; }

    public Guid SubstituteIngredientId { get; set; }
    public Ingredient? SubstituteIngredient { get; set; }

    public int ExtraPriceCents { get; set; }
}

/// <summary>Supplément proposé sur un produit, toujours facturé.</summary>
public class ProductSupplement
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public Guid IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    public int PriceCents { get; set; }
    public int MaxQuantity { get; set; } = 3;
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
