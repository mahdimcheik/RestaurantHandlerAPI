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
            Replacements = card.Replacements,
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
            .Include(p => p.OptionGroups).ThenInclude(l => l.OptionGroup)
            .ThenInclude(g => g!.Choices.Where(c => c.ArchivedAt == null)).ThenInclude(c => c.Ingredient)
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
        var replacements = await db.Ingredients.AsNoTracking()
            .Where(i => i.ArchivedAt == null && i.IsAvailable && i.IsOfferedAsReplacement)
            .OrderBy(i => i.Name)
            .Select(i => new PublicReplacement { IngredientId = i.Id, Name = i.Name, PriceCents = i.PriceCents, ImageUrl = i.ImageUrl })
            .ToListAsync(ct);
        foreach (var replacement in replacements)
            replacement.ImageUrl = images.Url(replacement.ImageUrl);

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
            Replacements = replacements,
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
                    PriceCents = pi.Ingredient!.PriceCents,
                    IsForcedRemoved = availability.ForcedRemovalIngredientIds.Contains(pi.IngredientId),
                    ImageUrl = images.Url(pi.Ingredient!.ImageUrl),
                })
                .ToList(),
            OptionGroups = AvailabilityPolicy.ActiveOptionGroups(p)
                .Select(o => new PublicOptionGroup
                {
                    Id = o.Group.Id,
                    Label = o.Group.Label,
                    ImageUrl = images.Url(o.Group.ImageUrl),
                    MinChoices = o.MinChoices,
                    MaxChoices = o.MaxChoices,
                    FreeChoices = o.FreeChoices,
                    ExtraChoicePriceCents = o.Group.ExtraChoicePriceCents,
                    MaxPerChoice = o.Group.MaxPerChoice,
                    Choices = o.Group.Choices
                        .Where(c => c.ArchivedAt is null)
                        .OrderBy(c => c.Position).ThenBy(c => c.Name)
                        .Select(c => new PublicOptionChoice
                        {
                            Id = c.Id,
                            Name = c.Name,
                            ImageUrl = images.Url(c.ImageUrl ?? c.Ingredient?.ImageUrl),
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
