using Microsoft.EntityFrameworkCore;
using RestaurantHandler.Contexts;
using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Models;

namespace RestaurantHandler.Services;

/// <summary>Produits (US-E02-3), composition (US-E02-5), groupes d'options proposés (US-E02-9) et rupture produit (US-E03-4).</summary>
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

    public async Task<ProductDetails> RemoveImageAsync(Guid id, CancellationToken ct)
    {
        var product = await FindAsync(id);
        var previous = product.ImageUrl;
        if (previous is not null)
        {
            product.ImageUrl = null;
            product.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            await images.DeleteAsync(previous);
            await changes.CatalogChangedAsync();
        }

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

        var referenced = dto.Ingredients.Select(i => i.IngredientId).ToHashSet();
        var known = await db.Ingredients.Where(i => referenced.Contains(i.Id) && i.ArchivedAt == null).CountAsync();
        if (known != referenced.Count)
            throw new BusinessRuleException("Un ingrédient de la composition est introuvable ou archivé.");

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
        }

        product.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();

        return await GetAsync(id);
    }

    /// <summary>
    /// Groupes d'options proposés sur le produit, dans l'ordre d'affichage. Seules les liaisons
    /// changent : les groupes eux-mêmes se gèrent dans <see cref="OptionGroupService"/>.
    /// </summary>
    public async Task<ProductDetails> UpdateOptionGroupsAsync(Guid id, OptionGroupsUpdate dto)
    {
        var product = await db.Products.Include(p => p.OptionGroups)
            .FirstOrDefaultAsync(p => p.Id == id && p.ArchivedAt == null)
            ?? throw new ResourceNotFoundException("Produit introuvable.");

        var wanted = dto.OptionGroups.Select(g => g.OptionGroupId).ToList();
        var groups = await db.OptionGroups
            .Where(g => wanted.Contains(g.Id) && g.ArchivedAt == null)
            .Select(g => new { g.Id, g.Label, Capacity = g.Choices.Count(c => c.ArchivedAt == null) * g.MaxPerChoice })
            .ToDictionaryAsync(g => g.Id);
        if (groups.Count != wanted.Count)
            throw new BusinessRuleException("Un groupe d'options est introuvable ou archivé.");
        foreach (var write in dto.OptionGroups.Where(g => g.Rules is not null))
        {
            var group = groups[write.OptionGroupId];
            if (write.Rules!.MinChoices > group.Capacity)
                throw new BusinessRuleException($"« {group.Label} » propose moins de choix que le minimum.");
        }

        var links = product.OptionGroups.ToDictionary(l => l.OptionGroupId);
        foreach (var removed in links.Values.Where(l => !wanted.Contains(l.OptionGroupId)))
            db.ProductOptionGroups.Remove(removed);
        for (var position = 0; position < dto.OptionGroups.Count; position++)
        {
            var write = dto.OptionGroups[position];
            if (!links.TryGetValue(write.OptionGroupId, out var link))
            {
                link = new ProductOptionGroup { ProductId = id, OptionGroupId = write.OptionGroupId };
                product.OptionGroups.Add(link);
            }

            link.Position = position;
            link.MinChoices = write.Rules?.MinChoices;
            link.MaxChoices = write.Rules?.MaxChoices;
        }

        product.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();

        return await GetAsync(id);
    }

    private static IQueryable<Product> WithComposition(IQueryable<Product> query) =>
        query
            .Include(p => p.Ingredients).ThenInclude(pi => pi.Ingredient)
            .Include(p => p.OptionGroups).ThenInclude(l => l.OptionGroup)
            .ThenInclude(g => g!.Choices.Where(c => c.ArchivedAt == null)).ThenInclude(c => c.Ingredient)
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
                    })
                    .ToList(),
            },
            OptionGroups = p.OptionGroups
                .Where(l => l.OptionGroup is { ArchivedAt: null })
                .OrderBy(l => l.Position)
                .Select((l, position) => OptionGroupService.ToDetails(l.OptionGroup!, images, position, productRules: RulesOf(l)))
                .ToList(),
        };

    private static OptionGroupRules? RulesOf(ProductOptionGroup link) =>
        link is { MinChoices: { } min, MaxChoices: { } max }
            ? new OptionGroupRules { MinChoices = min, MaxChoices = max }
            : null;
}
