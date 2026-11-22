using Microsoft.EntityFrameworkCore;
using RestaurantHandler.Contexts;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Models;

namespace RestaurantHandler.Services;

/// <summary>Produits (US-E02-3), composition (US-E02-5) et rupture produit (US-E03-4).</summary>
public class ProductService(MainContext db, CatalogImageService images, CatalogChangeNotifier changes)
{
    public async Task<List<ProductDetails>> ListAsync(Guid? categoryId, bool includeArchived)
    {
        var products = await WithComposition(db.Products.AsNoTracking())
            .Where(p => (includeArchived || p.ArchivedAt == null) && (categoryId == null || p.CategoryId == categoryId))
            .OrderBy(p => p.ArchivedAt != null).ThenBy(p => p.CategoryId).ThenBy(p => p.Position).ThenBy(p => p.Name)
            .ToListAsync();

        return products.Select(ToDetails).ToList();
    }

    public async Task<ProductDetails> GetAsync(Guid id) =>
        ToDetails(
            await WithComposition(db.Products.AsNoTracking()).FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new ResourceNotFoundException("Produit introuvable.")
        );

    public async Task<ProductDetails> CreateAsync(ProductWrite dto)
    {
        await EnsureCategoryAsync(dto.CategoryId);

        var product = new Product { CategoryId = dto.CategoryId, Position = await NextPositionAsync(dto.CategoryId) };
        Apply(product, dto);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();

        return await GetAsync(product.Id);
    }

    /// <summary>Un changement de prix ne touche aucune commande existante (snapshots, R3).</summary>
    public async Task<ProductDetails> UpdateAsync(Guid id, ProductWrite dto)
    {
        var product = await FindAsync(id);
        if (product.CategoryId != dto.CategoryId)
        {
            await EnsureCategoryAsync(dto.CategoryId);
            product.CategoryId = dto.CategoryId;
            product.Position = await NextPositionAsync(dto.CategoryId);
        }

        Apply(product, dto);
        product.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();

        return await GetAsync(id);
    }

    public async Task<List<ProductDetails>> ReorderAsync(Guid categoryId, ReorderRequest dto)
    {
        var products = await db.Products.Where(p => p.CategoryId == categoryId && p.ArchivedAt == null).ToListAsync();
        CatalogOrdering.Apply(products, p => p.Id, dto.OrderedIds, (p, position) => p.Position = position);
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();

        return await ListAsync(categoryId, false);
    }

    public async Task<ProductDetails> SetAvailabilityAsync(Guid id, bool isAvailable)
    {
        var product = await FindAsync(id);
        if (product.IsAvailable != isAvailable)
        {
            product.IsAvailable = isAvailable;
            product.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            await changes.CatalogChangedAsync();
        }

        return await GetAsync(id);
    }

    /// <summary>Jamais supprimé (des commandes y pointent) ; retiré des menus qui le proposaient.</summary>
    public async Task ArchiveAsync(Guid id)
    {
        var product = await FindAsync(id);
        product.ArchivedAt = DateTimeOffset.UtcNow;
        product.IsActive = false;
        db.MenuSlotOptions.RemoveRange(await db.MenuSlotOptions.Where(o => o.ProductId == id).ToListAsync());
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();
    }

    public async Task<ProductDetails> SetImageAsync(Guid id, IFormFile file, CancellationToken ct)
    {
        var product = await FindAsync(id);
        var key = await images.StoreAsync(file, ct);
        var previous = product.ImageUrl;

        product.ImageUrl = key;
        product.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await images.DeleteAsync(previous);
        await changes.CatalogChangedAsync();

        return await GetAsync(id);
    }

    /// <summary>
    /// Remplace la composition entière. Les lignes existantes sont mises à jour sur place plutôt
    /// que supprimées puis recréées, pour ne jamais suivre deux entités de même clé.
    /// </summary>
    public async Task<ProductDetails> UpdateCompositionAsync(Guid id, CompositionUpdate dto)
    {
        var product = await WithComposition(db.Products).FirstOrDefaultAsync(p => p.Id == id && p.ArchivedAt == null)
            ?? throw new ResourceNotFoundException("Produit introuvable.");

        var referenced = dto.Ingredients.Select(i => i.IngredientId)
            .Concat(dto.Ingredients.SelectMany(i => i.Substitutions).Select(s => s.SubstituteIngredientId))
            .Concat(dto.Supplements.Select(s => s.IngredientId))
            .ToHashSet();
        var known = await db.Ingredients.Where(i => referenced.Contains(i.Id) && i.ArchivedAt == null).CountAsync();
        if (known != referenced.Count)
            throw new BusinessRuleException("Un ingrédient de la composition est introuvable ou archivé.");

        // Composition
        var wanted = dto.Ingredients.ToDictionary(i => i.IngredientId);
        foreach (var removed in product.Ingredients.Where(pi => !wanted.ContainsKey(pi.IngredientId)).ToList())
            db.ProductIngredients.Remove(removed);

        foreach (var item in dto.Ingredients)
        {
            var component = product.Ingredients.FirstOrDefault(pi => pi.IngredientId == item.IngredientId);
            if (component is null)
            {
                component = new ProductIngredient { ProductId = id, IngredientId = item.IngredientId };
                product.Ingredients.Add(component);
            }
            component.IsRemovable = item.IsRemovable;

            var wantedSubs = item.Substitutions.ToDictionary(s => s.SubstituteIngredientId);
            foreach (var removed in component.Substitutions.Where(s => !wantedSubs.ContainsKey(s.SubstituteIngredientId)).ToList())
                db.IngredientSubstitutions.Remove(removed);
            foreach (var sub in item.Substitutions)
            {
                var existing = component.Substitutions.FirstOrDefault(s => s.SubstituteIngredientId == sub.SubstituteIngredientId);
                if (existing is null)
                {
                    component.Substitutions.Add(new IngredientSubstitution
                    {
                        ProductId = id,
                        IngredientId = item.IngredientId,
                        SubstituteIngredientId = sub.SubstituteIngredientId,
                        ExtraPriceCents = sub.ExtraPriceCents,
                    });
                }
                else
                {
                    existing.ExtraPriceCents = sub.ExtraPriceCents;
                    existing.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }
        }

        // Suppléments
        var wantedSupplements = dto.Supplements.ToDictionary(s => s.IngredientId);
        foreach (var removed in product.Supplements.Where(s => !wantedSupplements.ContainsKey(s.IngredientId)).ToList())
            db.ProductSupplements.Remove(removed);
        foreach (var item in dto.Supplements)
        {
            var supplement = product.Supplements.FirstOrDefault(s => s.IngredientId == item.IngredientId);
            if (supplement is null)
            {
                supplement = new ProductSupplement { ProductId = id, IngredientId = item.IngredientId };
                product.Supplements.Add(supplement);
            }
            supplement.PriceCents = item.PriceCents;
            supplement.MaxQuantity = item.MaxQuantity;
        }

        product.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();

        return await GetAsync(id);
    }

    private static IQueryable<Product> WithComposition(IQueryable<Product> query) =>
        query
            .Include(p => p.Ingredients).ThenInclude(pi => pi.Ingredient)
            .Include(p => p.Ingredients).ThenInclude(pi => pi.Substitutions).ThenInclude(s => s.SubstituteIngredient)
            .Include(p => p.Supplements).ThenInclude(s => s.Ingredient)
            .AsSplitQuery();

    private async Task<Product> FindAsync(Guid id) =>
        await db.Products.FirstOrDefaultAsync(p => p.Id == id && p.ArchivedAt == null)
        ?? throw new ResourceNotFoundException("Produit introuvable.");

    private async Task EnsureCategoryAsync(Guid categoryId)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == categoryId && c.ArchivedAt == null))
            throw new BusinessRuleException("Catégorie introuvable ou archivée.");
    }

    private async Task<int> NextPositionAsync(Guid categoryId) =>
        (await db.Products.Where(p => p.CategoryId == categoryId && p.ArchivedAt == null).MaxAsync(p => (int?)p.Position) ?? -1) + 1;

    private static void Apply(Product product, ProductWrite dto)
    {
        product.Name = dto.Name.Trim();
        product.Description = dto.Description?.Trim();
        product.Kind = dto.Kind;
        product.PriceCents = dto.PriceCents;
        product.VatRate = dto.VatRate;
        product.IsActive = dto.IsActive;
    }

    private ProductDetails ToDetails(Product p) =>
        new()
        {
            Id = p.Id,
            CategoryId = p.CategoryId,
            Name = p.Name,
            Description = p.Description,
            ImageUrl = images.Url(p.ImageUrl),
            Kind = p.Kind,
            PriceCents = p.PriceCents,
            VatRate = p.VatRate,
            Position = p.Position,
            IsActive = p.IsActive,
            IsAvailable = p.IsAvailable,
            IsArchived = p.ArchivedAt != null,
            Composition = new CompositionDetails
            {
                Ingredients = p.Ingredients
                    .OrderBy(pi => pi.Ingredient?.Name)
                    .Select(pi => new CompositionIngredientDetails
                    {
                        IngredientId = pi.IngredientId,
                        Name = pi.Ingredient?.Name ?? string.Empty,
                        IsRemovable = pi.IsRemovable,
                        IsAvailable = pi.Ingredient?.IsAvailable ?? false,
                        Substitutions = pi.Substitutions
                            .OrderBy(s => s.SubstituteIngredient?.Name)
                            .Select(s => new SubstitutionDetails
                            {
                                SubstituteIngredientId = s.SubstituteIngredientId,
                                Name = s.SubstituteIngredient?.Name ?? string.Empty,
                                ExtraPriceCents = s.ExtraPriceCents,
                                IsAvailable = s.SubstituteIngredient?.IsAvailable ?? false,
                            })
                            .ToList(),
                    })
                    .ToList(),
                Supplements = p.Supplements
                    .OrderBy(s => s.Ingredient?.Name)
                    .Select(s => new SupplementDetails
                    {
                        IngredientId = s.IngredientId,
                        Name = s.Ingredient?.Name ?? string.Empty,
                        PriceCents = s.PriceCents,
                        MaxQuantity = s.MaxQuantity,
                        IsAvailable = s.Ingredient?.IsAvailable ?? false,
                    })
                    .ToList(),
            },
        };
}
