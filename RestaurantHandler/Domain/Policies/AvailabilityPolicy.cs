using RestaurantHandler.Models;

namespace RestaurantHandler.Domain.Policies;

/// <summary>Disponibilité d'un produit : commandable ou non, et retraits imposés par une rupture.</summary>
public sealed record ProductAvailability(bool IsOrderable, IReadOnlyList<Guid> ForcedRemovalIngredientIds)
{
    public static readonly ProductAvailability Unavailable = new(false, []);
}

/// <summary>
/// R6 (docs/20-regles-metier/01-regles-commande.md) sur un graphe de carte déjà chargé :
/// <c>Product.Category</c>, <c>Product.Ingredients[].Ingredient</c>, substitutions et suppléments
/// avec leurs ingrédients, <c>Menu.Slots[].Options[].Product</c> (et le graphe produit ci-dessus).
/// </summary>
public static class AvailabilityPolicy
{
    public static bool IsIngredientAvailable(Ingredient? ingredient) =>
        ingredient is { IsAvailable: true, ArchivedAt: null };

    /// <summary>Visible côté client : actif, non archivé, dans une catégorie active non archivée.</summary>
    public static bool IsListed(Product product) =>
        product is { IsActive: true, ArchivedAt: null }
        && product.Category is { IsActive: true, ArchivedAt: null };

    public static ProductAvailability ForProduct(Product product)
    {
        if (!IsListed(product) || !product.IsAvailable)
            return ProductAvailability.Unavailable;

        var forcedRemovals = new List<Guid>();
        foreach (var component in product.Ingredients)
        {
            if (IsIngredientAvailable(component.Ingredient))
                continue;
            if (!component.IsRemovable)
                return ProductAvailability.Unavailable;
            forcedRemovals.Add(component.IngredientId);
        }

        return new ProductAvailability(true, forcedRemovals);
    }

    /// <summary>
    /// Une substitution est proposée si le remplaçant est disponible. Remplacer un ingrédient en
    /// rupture reste possible : c'est l'alternative naturelle au retrait imposé.
    /// </summary>
    public static bool IsSubstitutionOffered(IngredientSubstitution substitution) =>
        IsIngredientAvailable(substitution.SubstituteIngredient);

    public static bool IsSupplementOffered(ProductSupplement supplement) =>
        IsIngredientAvailable(supplement.Ingredient);

    public static bool IsMenuListed(Menu menu) => menu is { IsActive: true, ArchivedAt: null };

    public static bool IsOptionOrderable(MenuSlotOption option) =>
        option.Product is not null && ForProduct(option.Product).IsOrderable;

    /// <summary>Commandable si listé et si chaque emplacement à minimum > 0 garde au moins une option commandable.</summary>
    public static bool IsMenuOrderable(Menu menu) =>
        IsMenuListed(menu)
        && menu.Slots
            .Where(s => s.ArchivedAt is null && s.MinChoices > 0)
            .All(s => s.Options.Any(IsOptionOrderable));
}
