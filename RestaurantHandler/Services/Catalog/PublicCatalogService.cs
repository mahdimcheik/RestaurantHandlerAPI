using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using RestaurantHandler.Contexts;
using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Services;

/// <summary>
/// Carte publique en une requête (US-E02-1). La carte (disponibilité comprise) est en cache et
/// invalidée par chaque écriture Manager ; l'état d'ouverture est calculé à chaque appel.
/// </summary>
public class PublicCatalogService(MainContext db, HybridCache cache, CatalogImageService images, RestaurantService restaurant)
{
    public async Task<PublicCard> GetCardAsync()
    {
        var card = await cache.GetOrCreateAsync(
            CacheKeys.PublicCard,
            async ct => await BuildAsync(ct),
            CacheKeys.Catalog,
            [CacheKeys.CatalogTag]
        );

        return new PublicCard
        {
            Categories = card.Categories,
            Menus = card.Menus,
            Opening = await restaurant.GetOpeningStateAsync(),
        };
    }

    private async Task<PublicCard> BuildAsync(CancellationToken ct)
    {
        // Requêtes suivies dans le même contexte : EF relie les produits des options de menu aux
        // produits déjà chargés, avec leur composition. Rien n'est enregistré.
        var categories = await db.Categories
            .Where(c => c.ArchivedAt == null && c.IsActive)
            .OrderBy(c => c.Position).ThenBy(c => c.Name)
            .ToListAsync(ct);

        var products = await db.Products
            .Where(p => p.ArchivedAt == null && p.IsActive)
            .Include(p => p.Ingredients).ThenInclude(pi => pi.Ingredient)
            .Include(p => p.Ingredients).ThenInclude(pi => pi.Substitutions).ThenInclude(s => s.SubstituteIngredient)
            .Include(p => p.Supplements).ThenInclude(s => s.Ingredient)
            .Include(p => p.OptionGroups.Where(g => g.ArchivedAt == null))
            .ThenInclude(g => g.Choices.Where(c => c.ArchivedAt == null)).ThenInclude(c => c.Ingredient)
            .AsSplitQuery()
            .OrderBy(p => p.Position).ThenBy(p => p.Name)
            .ToListAsync(ct);

        var menus = await db.Menus
            .Where(m => m.ArchivedAt == null && m.IsActive)
            .Include(m => m.Slots.Where(s => s.ArchivedAt == null).OrderBy(s => s.Position))
            .ThenInclude(s => s.Options)
            .OrderBy(m => m.Position).ThenBy(m => m.Name)
            .ToListAsync(ct);

        var listed = products.Where(AvailabilityPolicy.IsListed).ToList();

        return new PublicCard
        {
            Categories = categories
                .Select(c => new PublicCategory
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    ImageUrl = images.Url(c.ImageUrl),
                    Products = listed.Where(p => p.CategoryId == c.Id).Select(ToPublic).ToList(),
                })
                .Where(c => c.Products.Count > 0)
                .ToList(),
            Menus = menus.Select(ToPublic).ToList(),
        };
    }

    private PublicProduct ToPublic(Product p)
    {
        var availability = AvailabilityPolicy.ForProduct(p);
        return new PublicProduct
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            ImageUrl = images.Url(p.ImageUrl),
            Kind = p.Kind,
            PriceCents = p.PriceCents,
            IsAvailable = availability.IsOrderable,
            Ingredients = p.Ingredients
                .Where(pi => pi.Ingredient is { ArchivedAt: null })
                .OrderBy(pi => pi.Ingredient!.Name)
                .Select(pi => new PublicIngredient
                {
                    IngredientId = pi.IngredientId,
                    Name = pi.Ingredient!.Name,
                    IsRemovable = pi.IsRemovable,
                    IsForcedRemoved = availability.ForcedRemovalIngredientIds.Contains(pi.IngredientId),
                    Substitutions = pi.Substitutions
                        .Where(AvailabilityPolicy.IsSubstitutionOffered)
                        .OrderBy(s => s.SubstituteIngredient!.Name)
                        .Select(s => new PublicSubstitution
                        {
                            IngredientId = s.SubstituteIngredientId,
                            Name = s.SubstituteIngredient!.Name,
                            ExtraPriceCents = s.ExtraPriceCents,
                        })
                        .ToList(),
                })
                .ToList(),
            Supplements = p.Supplements
                .Where(AvailabilityPolicy.IsSupplementOffered)
                .OrderBy(s => s.Ingredient!.Name)
                .Select(s => new PublicSupplement
                {
                    IngredientId = s.IngredientId,
                    Name = s.Ingredient!.Name,
                    PriceCents = s.PriceCents,
                    MaxQuantity = s.MaxQuantity,
                })
                .ToList(),
            OptionGroups = AvailabilityPolicy.ActiveOptionGroups(p)
                .Select(g => new PublicOptionGroup
                {
                    Id = g.Id,
                    Label = g.Label,
                    ImageUrl = images.Url(g.ImageUrl),
                    MinChoices = g.MinChoices,
                    MaxChoices = g.MaxChoices,
                    FreeChoices = g.FreeChoices,
                    ExtraChoicePriceCents = g.ExtraChoicePriceCents,
                    Choices = g.Choices
                        .Where(c => c.ArchivedAt is null)
                        .OrderBy(c => c.Position).ThenBy(c => c.Name)
                        .Select(c => new PublicOptionChoice
                        {
                            Id = c.Id,
                            Name = c.Name,
                            ImageUrl = images.Url(c.ImageUrl),
                            ExtraPriceCents = c.ExtraPriceCents,
                            IsAvailable = AvailabilityPolicy.IsOptionChoiceOffered(c),
                        })
                        .ToList(),
                })
                .ToList(),
        };
    }

    private PublicMenu ToPublic(Menu m) =>
        new()
        {
            Id = m.Id,
            Name = m.Name,
            Description = m.Description,
            ImageUrl = images.Url(m.ImageUrl),
            PriceCents = m.PriceCents,
            IsAvailable = AvailabilityPolicy.IsMenuOrderable(m),
            Slots = m.Slots
                .Select(s => new PublicMenuSlot
                {
                    Id = s.Id,
                    Label = s.Label,
                    MinChoices = s.MinChoices,
                    MaxChoices = s.MaxChoices,
                    Options = s.Options
                        .Where(o => o.Product is not null && AvailabilityPolicy.IsListed(o.Product))
                        .OrderBy(o => o.Product!.Position).ThenBy(o => o.Product!.Name)
                        .Select(o => new PublicMenuOption
                        {
                            ProductId = o.ProductId,
                            Name = o.Product!.Name,
                            ImageUrl = images.Url(o.Product.ImageUrl),
                            ExtraPriceCents = o.ExtraPriceCents,
                            IsAvailable = AvailabilityPolicy.IsOptionOrderable(o),
                        })
                        .ToList(),
                })
                .ToList(),
        };
}
