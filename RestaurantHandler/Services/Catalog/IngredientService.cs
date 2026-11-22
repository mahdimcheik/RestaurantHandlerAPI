using Microsoft.EntityFrameworkCore;
using RestaurantHandler.Contexts;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Models;

namespace RestaurantHandler.Services;

/// <summary>Ingrédients (US-E02-4) et leurs ruptures (US-E03-4).</summary>
public class IngredientService(MainContext db, CatalogChangeNotifier changes)
{
    public Task<List<IngredientDetails>> ListAsync(bool includeArchived) =>
        db.Ingredients.AsNoTracking()
            .Where(i => includeArchived || i.ArchivedAt == null)
            .OrderBy(i => i.Name)
            .Select(i => new IngredientDetails
            {
                Id = i.Id,
                Name = i.Name,
                IsAvailable = i.IsAvailable,
                IsArchived = i.ArchivedAt != null,
            })
            .ToListAsync();

    public async Task<IngredientDetails> CreateAsync(IngredientWrite dto)
    {
        var name = dto.Name.Trim();
        await EnsureNameFreeAsync(name, null);

        var ingredient = new Ingredient { Name = name };
        db.Ingredients.Add(ingredient);
        await db.SaveChangesAsync();

        return ToDetails(ingredient);
    }

    public async Task<IngredientDetails> RenameAsync(Guid id, IngredientWrite dto)
    {
        var ingredient = await FindAsync(id);
        var name = dto.Name.Trim();
        await EnsureNameFreeAsync(name, id);

        ingredient.Name = name;
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

    /// <summary>Refusé tant qu'un produit non archivé l'utilise (composition, remplacement, supplément, choix d'option).</summary>
    public async Task ArchiveAsync(Guid id)
    {
        var ingredient = await FindAsync(id);

        var users = await db.Products.AsNoTracking()
            .Where(p => p.ArchivedAt == null
                && (p.Ingredients.Any(pi => pi.IngredientId == id || pi.Substitutions.Any(s => s.SubstituteIngredientId == id))
                    || p.Supplements.Any(s => s.IngredientId == id)
                    || p.OptionGroups.Any(g => g.ArchivedAt == null
                        && g.Choices.Any(c => c.ArchivedAt == null && c.IngredientId == id))))
            .OrderBy(p => p.Name)
            .Select(p => p.Name)
            .Take(5)
            .ToListAsync();
        if (users.Count > 0)
            throw new BusinessConflictException(
                $"Ingrédient utilisé par : {string.Join(", ", users)}. Retirez-le de ces produits avant de l'archiver.");

        ingredient.ArchivedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
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

    private static IngredientDetails ToDetails(Ingredient i) =>
        new() { Id = i.Id, Name = i.Name, IsAvailable = i.IsAvailable, IsArchived = i.ArchivedAt != null };
}
