using Microsoft.EntityFrameworkCore;
using RestaurantHandler.Contexts;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Models;

namespace RestaurantHandler.Services;

/// <summary>Ingrédients (US-E02-4) et leurs ruptures (US-E03-4).</summary>
public class IngredientService(MainContext db, CatalogImageService images, CatalogChangeNotifier changes)
{
    public async Task<List<IngredientDetails>> ListAsync(bool includeArchived) =>
        (await db.Ingredients.AsNoTracking()
            .Where(i => includeArchived || i.ArchivedAt == null)
            .OrderBy(i => i.Name)
            .ToListAsync())
        .Select(ToDetails)
        .ToList();

    public async Task<IngredientDetails> CreateAsync(IngredientWrite dto)
    {
        var name = dto.Name.Trim();
        await EnsureNameFreeAsync(name, null);

        var ingredient = new Ingredient
        {
            Name = name,
            PriceCents = dto.PriceCents,
            IsOfferedAsReplacement = dto.IsOfferedAsReplacement,
        };
        db.Ingredients.Add(ingredient);
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();

        return ToDetails(ingredient);
    }

    /// <summary>Nom, prix de référence et proposition en remplacement.</summary>
    public async Task<IngredientDetails> UpdateAsync(Guid id, IngredientWrite dto)
    {
        var ingredient = await FindAsync(id);
        var name = dto.Name.Trim();
        await EnsureNameFreeAsync(name, id);

        ingredient.Name = name;
        ingredient.PriceCents = dto.PriceCents;
        ingredient.IsOfferedAsReplacement = dto.IsOfferedAsReplacement;
        ingredient.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();

        return ToDetails(ingredient);
    }

    public async Task<IngredientDetails> SetAvailabilityAsync(Guid id, bool isAvailable)
    {
        var ingredient = await FindAsync(id);
        if (ingredient.IsAvailable != isAvailable)
        {
            ingredient.IsAvailable = isAvailable;
            ingredient.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            await changes.CatalogChangedAsync();
        }

        return ToDetails(ingredient);
    }

    public async Task<IngredientDetails> SetImageAsync(Guid id, IFormFile file, CancellationToken ct)
    {
        var ingredient = await FindAsync(id);
        var key = await images.StoreAsync(file, ct);
        var previous = ingredient.ImageUrl;

        ingredient.ImageUrl = key;
        ingredient.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await images.DeleteAsync(previous);
        await changes.CatalogChangedAsync();

        return ToDetails(ingredient);
    }

    public async Task<IngredientDetails> RemoveImageAsync(Guid id, CancellationToken ct)
    {
        var ingredient = await FindAsync(id);
        var previous = ingredient.ImageUrl;
        if (previous is not null)
        {
            ingredient.ImageUrl = null;
            ingredient.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            await images.DeleteAsync(previous);
            await changes.CatalogChangedAsync();
        }

        return ToDetails(ingredient);
    }

    /// <summary>
    /// Refusé tant qu'un produit non archivé l'a dans sa composition ou
    /// qu'un choix d'un groupe d'options non archivé y est lié, même si le groupe ne sert à aucun produit.
    /// </summary>
    public async Task ArchiveAsync(Guid id)
    {
        var ingredient = await FindAsync(id);

        var users = await db.Products.AsNoTracking()
            .Where(p => p.ArchivedAt == null
                && p.Ingredients.Any(pi => pi.IngredientId == id))
            .OrderBy(p => p.Name)
            .Select(p => p.Name)
            .Take(5)
            .ToListAsync();
        if (users.Count > 0)
            throw new BusinessConflictException(
                $"Ingrédient utilisé par : {string.Join(", ", users)}. Retirez-le de ces produits avant de l'archiver.");

        var groups = await db.OptionGroups.AsNoTracking()
            .Where(g => g.ArchivedAt == null && g.Choices.Any(c => c.ArchivedAt == null && c.IngredientId == id))
            .OrderBy(g => g.Label)
            .Select(g => g.Label)
            .Take(5)
            .ToListAsync();
        if (groups.Count > 0)
            throw new BusinessConflictException(
                $"Ingrédient lié à un choix des groupes d'options : {string.Join(", ", groups)}. Retirez ces choix avant de l'archiver.");

        ingredient.ArchivedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();
    }

    private async Task<Ingredient> FindAsync(Guid id) =>
        await db.Ingredients.FirstOrDefaultAsync(i => i.Id == id && i.ArchivedAt == null)
        ?? throw new ResourceNotFoundException("Ingrédient introuvable.");

    private async Task EnsureNameFreeAsync(string name, Guid? exceptId)
    {
        var lowered = name.ToLower();
        if (await db.Ingredients.AnyAsync(i => i.ArchivedAt == null && i.Id != exceptId && i.Name.ToLower() == lowered))
            throw new BusinessConflictException($"L'ingrédient « {name} » existe déjà.");
    }

    private IngredientDetails ToDetails(Ingredient i) =>
        new()
        {
            Id = i.Id,
            Name = i.Name,
            PriceCents = i.PriceCents,
            IsOfferedAsReplacement = i.IsOfferedAsReplacement,
            IsAvailable = i.IsAvailable,
            IsArchived = i.ArchivedAt != null,
            ImageUrl = images.Url(i.ImageUrl),
        };
}
