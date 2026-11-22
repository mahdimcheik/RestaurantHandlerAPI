using Microsoft.EntityFrameworkCore;
using RestaurantHandler.Contexts;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Models;

namespace RestaurantHandler.Services;

/// <summary>Catégories de la carte (US-E02-2). Archivées, jamais supprimées.</summary>
public class CategoryService(MainContext db, CatalogImageService images, CatalogChangeNotifier changes)
{
    public async Task<List<CategoryDetails>> ListAsync(bool includeArchived)
    {
        var rows = await db.Categories.AsNoTracking()
            .Where(c => includeArchived || c.ArchivedAt == null)
            .OrderBy(c => c.ArchivedAt != null).ThenBy(c => c.Position).ThenBy(c => c.Name)
            .Select(c => new
            {
                Category = c,
                ActiveProducts = c.Products.Count(p => p.ArchivedAt == null && p.IsActive),
            })
            .ToListAsync();

        return rows.Select(r => ToDetails(r.Category, r.ActiveProducts)).ToList();
    }

    public async Task<CategoryDetails> CreateAsync(CategoryWrite dto)
    {
        var name = dto.Name.Trim();
        await EnsureNameFreeAsync(name, null);

        var position = await db.Categories.Where(c => c.ArchivedAt == null).MaxAsync(c => (int?)c.Position) ?? -1;
        var category = new Category
        {
            Name = name,
            Description = dto.Description?.Trim(),
            IsActive = dto.IsActive,
            Position = position + 1,
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();

        return ToDetails(category, 0);
    }

    public async Task<CategoryDetails> UpdateAsync(Guid id, CategoryWrite dto)
    {
        var category = await FindAsync(id);
        var name = dto.Name.Trim();
        await EnsureNameFreeAsync(name, id);

        category.Name = name;
        category.Description = dto.Description?.Trim();
        category.IsActive = dto.IsActive;
        category.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();

        return ToDetails(category, await CountActiveProductsAsync(id));
    }

    /// <summary>Enregistre toutes les positions d'un coup (glisser-déposer).</summary>
    public async Task<List<CategoryDetails>> ReorderAsync(ReorderRequest dto)
    {
        var categories = await db.Categories.Where(c => c.ArchivedAt == null).ToListAsync();
        CatalogOrdering.Apply(categories, c => c.Id, dto.OrderedIds, (c, position) => c.Position = position);
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();

        return await ListAsync(false);
    }

    public async Task ArchiveAsync(Guid id)
    {
        var category = await FindAsync(id);
        if (await CountActiveProductsAsync(id) > 0)
            throw new BusinessConflictException(
                "Cette catégorie contient des produits actifs : désactivez-les ou déplacez-les avant de l'archiver.");

        category.ArchivedAt = DateTimeOffset.UtcNow;
        category.IsActive = false;
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();
    }

    public async Task<CategoryDetails> SetImageAsync(Guid id, IFormFile file, CancellationToken ct)
    {
        var category = await FindAsync(id);
        var key = await images.StoreAsync(file, ct);
        var previous = category.ImageUrl;

        category.ImageUrl = key;
        category.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await images.DeleteAsync(previous);
        await changes.CatalogChangedAsync();

        return ToDetails(category, await CountActiveProductsAsync(id));
    }

    private async Task<Category> FindAsync(Guid id) =>
        await db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.ArchivedAt == null)
        ?? throw new ResourceNotFoundException("Catégorie introuvable.");

    private Task<int> CountActiveProductsAsync(Guid id) =>
        db.Products.CountAsync(p => p.CategoryId == id && p.ArchivedAt == null && p.IsActive);

    private async Task EnsureNameFreeAsync(string name, Guid? exceptId)
    {
        var lowered = name.ToLower();
        if (await db.Categories.AnyAsync(c => c.ArchivedAt == null && c.Id != exceptId && c.Name.ToLower() == lowered))
            throw new BusinessConflictException($"Une catégorie « {name} » existe déjà.");
    }

    private CategoryDetails ToDetails(Category c, int activeProducts) =>
        new()
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            ImageUrl = images.Url(c.ImageUrl),
            Position = c.Position,
            IsActive = c.IsActive,
            IsArchived = c.ArchivedAt != null,
            ActiveProductCount = activeProducts,
        };
}
