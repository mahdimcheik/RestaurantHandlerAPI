using System.ComponentModel.DataAnnotations;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models;

// Montants en centimes TTC. Le front saisit en euros et convertit.

public class ReorderRequest
{
    /// <summary>Tous les identifiants de la liste, dans le nouvel ordre.</summary>
    [Required]
    [MinLength(1)]
    public List<Guid> OrderedIds { get; set; } = [];
}

public class AvailabilityUpdate
{
    public bool IsAvailable { get; set; }
}

// ---------- Catégories ----------

public class CategoryWrite
{
    [Required(ErrorMessage = "Le nom est obligatoire.")]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class CategoryDetails
{
    [Required] public Guid Id { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    [Required] public int Position { get; set; }
    [Required] public bool IsActive { get; set; }
    [Required] public bool IsArchived { get; set; }
    [Required] public int ActiveProductCount { get; set; }
}

// ---------- Produits ----------

public class ProductWrite
{
    [Required]
    public Guid CategoryId { get; set; }

    [Required(ErrorMessage = "Le nom est obligatoire.")]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public ProductKind Kind { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Le prix doit être positif.")]
    public int PriceCents { get; set; }

    [AllowedVatRate]
    public decimal VatRate { get; set; }

    public bool IsActive { get; set; } = true;
}

public class ProductDetails
{
    [Required] public Guid Id { get; set; }
    [Required] public Guid CategoryId { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    [Required] public ProductKind Kind { get; set; }
    [Required] public int PriceCents { get; set; }
    [Required] public decimal VatRate { get; set; }
    [Required] public int Position { get; set; }
    [Required] public bool IsActive { get; set; }
    [Required] public bool IsAvailable { get; set; }
    [Required] public bool IsArchived { get; set; }
    [Required] public CompositionDetails Composition { get; set; } = new();
    [Required] public List<OptionGroupDetails> OptionGroups { get; set; } = [];
}

// ---------- Ingrédients ----------

public class IngredientWrite
{
    [Required(ErrorMessage = "Le nom est obligatoire.")]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;
}

public class IngredientDetails
{
    [Required] public Guid Id { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public bool IsAvailable { get; set; }
    [Required] public bool IsArchived { get; set; }
}

// ---------- Composition (remplacée en un seul appel) ----------

public class CompositionUpdate : IValidatableObject
{
    [Required]
    public List<CompositionIngredientWrite> Ingredients { get; set; } = [];

    [Required]
    public List<SupplementWrite> Supplements { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Ingredients.Select(i => i.IngredientId).Distinct().Count() != Ingredients.Count)
            yield return new("Un ingrédient ne figure qu'une fois dans la composition.", [nameof(Ingredients)]);
        if (Supplements.Select(s => s.IngredientId).Distinct().Count() != Supplements.Count)
            yield return new("Un supplément ne figure qu'une fois.", [nameof(Supplements)]);

        foreach (var ingredient in Ingredients)
        {
            if (ingredient.Substitutions.Any(s => s.SubstituteIngredientId == ingredient.IngredientId))
                yield return new("Un ingrédient ne peut pas se remplacer lui-même.", [nameof(Ingredients)]);
            if (ingredient.Substitutions.Select(s => s.SubstituteIngredientId).Distinct().Count()
                != ingredient.Substitutions.Count)
                yield return new("Un remplaçant ne figure qu'une fois par ingrédient.", [nameof(Ingredients)]);
        }
    }
}

public class CompositionIngredientWrite
{
    [Required] public Guid IngredientId { get; set; }
    public bool IsRemovable { get; set; }
    public List<SubstitutionWrite> Substitutions { get; set; } = [];
}

public class SubstitutionWrite
{
    [Required] public Guid SubstituteIngredientId { get; set; }

    [Range(0, 100_000)]
    public int ExtraPriceCents { get; set; }
}

public class SupplementWrite
{
    [Required] public Guid IngredientId { get; set; }

    [Range(0, 100_000)]
    public int PriceCents { get; set; }

    [Range(1, 20)]
    public int MaxQuantity { get; set; } = 3;
}

public class CompositionDetails
{
    [Required] public List<CompositionIngredientDetails> Ingredients { get; set; } = [];
    [Required] public List<SupplementDetails> Supplements { get; set; } = [];
}

public class CompositionIngredientDetails
{
    [Required] public Guid IngredientId { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public bool IsRemovable { get; set; }
    [Required] public bool IsAvailable { get; set; }
    [Required] public List<SubstitutionDetails> Substitutions { get; set; } = [];
}

public class SubstitutionDetails
{
    [Required] public Guid SubstituteIngredientId { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public int ExtraPriceCents { get; set; }
    [Required] public bool IsAvailable { get; set; }
}

public class SupplementDetails
{
    [Required] public Guid IngredientId { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public int PriceCents { get; set; }
    [Required] public int MaxQuantity { get; set; }
    [Required] public bool IsAvailable { get; set; }
}

// ---------- Groupes d'options (remplacés en un seul appel) ----------

/// <summary>
/// Groupes dans l'ordre d'affichage. Un groupe ou un choix sans <c>Id</c> est créé ; un existant
/// absent de la liste est archivé (les commandes passées y pointent).
/// </summary>
public class OptionGroupsUpdate : IValidatableObject
{
    [Required]
    public List<OptionGroupWrite> Groups { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var groupIds = Groups.Where(g => g.Id is not null).Select(g => g.Id).ToList();
        if (groupIds.Distinct().Count() != groupIds.Count)
            yield return new("Un groupe ne figure qu'une fois.", [nameof(Groups)]);

        var choiceIds = Groups.SelectMany(g => g.Choices).Where(c => c.Id is not null).Select(c => c.Id).ToList();
        if (choiceIds.Distinct().Count() != choiceIds.Count)
            yield return new("Un choix ne figure qu'une fois.", [nameof(Groups)]);
    }
}

public class OptionGroupWrite : IValidatableObject
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Le libellé est obligatoire.")]
    [MaxLength(64)]
    public string Label { get; set; } = string.Empty;

    [Range(0, 20)]
    public int MinChoices { get; set; }

    [Range(1, 20)]
    public int MaxChoices { get; set; } = 1;

    /// <summary>Choix inclus avant de facturer <see cref="ExtraChoicePriceCents"/> ; tous si absent.</summary>
    [Range(0, 20)]
    public int? FreeChoices { get; set; }

    [Range(0, 100_000)]
    public int ExtraChoicePriceCents { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Un groupe propose au moins un choix.")]
    public List<OptionChoiceWrite> Choices { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinChoices > MaxChoices)
            yield return new("Le minimum de choix dépasse le maximum.", [nameof(MinChoices)]);
        if (MinChoices > Choices.Count)
            yield return new($"« {Label} » propose moins de choix que le minimum.", [nameof(Choices)]);
        if (Choices.Select(c => c.Name.Trim().ToLowerInvariant()).Distinct().Count() != Choices.Count)
            yield return new($"« {Label} » : un nom de choix ne figure qu'une fois.", [nameof(Choices)]);
    }
}

public class OptionChoiceWrite
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Le nom est obligatoire.")]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Lie la disponibilité du choix à celle d'un ingrédient (sauce, pain) ; nul pour une taille.</summary>
    public Guid? IngredientId { get; set; }

    [Range(0, 100_000)]
    public int ExtraPriceCents { get; set; }
}

public class OptionGroupDetails
{
    [Required] public Guid Id { get; set; }
    [Required] public string Label { get; set; } = string.Empty;
    [Required] public int Position { get; set; }
    [Required] public int MinChoices { get; set; }
    [Required] public int MaxChoices { get; set; }
    [Required] public int FreeChoices { get; set; }
    [Required] public int ExtraChoicePriceCents { get; set; }
    [Required] public List<OptionChoiceDetails> Choices { get; set; } = [];
}

public class OptionChoiceDetails
{
    [Required] public Guid Id { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    public Guid? IngredientId { get; set; }
    [Required] public int ExtraPriceCents { get; set; }
    [Required] public int Position { get; set; }

    /// <summary>Faux si l'ingrédient lié est en rupture.</summary>
    [Required] public bool IsAvailable { get; set; }
}

// ---------- Menus ----------

public class MenuWrite
{
    [Required(ErrorMessage = "Le nom est obligatoire.")]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Le prix doit être positif.")]
    public int PriceCents { get; set; }

    [AllowedVatRate]
    public decimal VatRate { get; set; }

    public bool IsActive { get; set; } = true;
}

public class MenuSlotWrite : IValidatableObject
{
    [Required(ErrorMessage = "Le libellé est obligatoire.")]
    [MaxLength(64)]
    public string Label { get; set; } = string.Empty;

    [Range(0, 10)]
    public int MinChoices { get; set; } = 1;

    [Range(1, 10)]
    public int MaxChoices { get; set; } = 1;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinChoices > MaxChoices)
            yield return new("Le minimum de choix dépasse le maximum.", [nameof(MinChoices)]);
    }
}

public class SlotOptionsUpdate : IValidatableObject
{
    [Required]
    public List<SlotOptionWrite> Options { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Options.Select(o => o.ProductId).Distinct().Count() != Options.Count)
            yield return new("Un produit ne figure qu'une fois par emplacement.", [nameof(Options)]);
    }
}

public class SlotOptionWrite
{
    [Required] public Guid ProductId { get; set; }

    [Range(0, 100_000)]
    public int ExtraPriceCents { get; set; }
}

public class MenuDetails
{
    [Required] public Guid Id { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    [Required] public int PriceCents { get; set; }
    [Required] public decimal VatRate { get; set; }
    [Required] public int Position { get; set; }
    [Required] public bool IsActive { get; set; }
    [Required] public bool IsArchived { get; set; }
    [Required] public List<MenuSlotDetails> Slots { get; set; } = [];
}

public class MenuSlotDetails
{
    [Required] public Guid Id { get; set; }
    [Required] public string Label { get; set; } = string.Empty;
    [Required] public int Position { get; set; }
    [Required] public int MinChoices { get; set; }
    [Required] public int MaxChoices { get; set; }
    [Required] public List<SlotOptionDetails> Options { get; set; } = [];
}

public class SlotOptionDetails
{
    [Required] public Guid ProductId { get; set; }
    [Required] public string ProductName { get; set; } = string.Empty;
    [Required] public int ExtraPriceCents { get; set; }
}
