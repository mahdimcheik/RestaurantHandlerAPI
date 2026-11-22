using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace RestaurantHandler.Services;

/// <summary>
/// Service pour la gestion des catégories de cursus
/// </summary>
public class CategoryCursusService(MainContext context, HybridCache cache)
{
    /// <summary>
    /// Récupère toutes les catégories de cursus
    /// </summary>
    /// <returns>Liste des catégories de cursus</returns>
    public async Task<Response<List<CategoryCursusDetails>>> GetAllCategoryCursusAsync(CallerIdentity? user)
    {
        // check if the user is admin to include archived too
        var includeArchived = user?.IsAdmin == true;

        var categories = await cache.GetOrCreateAsync(
            CacheKeys.Categories(includeArchived),
            async ct => await context.CategoryCursuses
                .AsNoTracking()
                .Where(c => includeArchived || c.ArchivedAt == null)
                .OrderBy(c => c.Name)
                .Select(c => new CategoryCursusDetails(c))
                .ToListAsync(ct),
            CacheKeys.Reference,
            [CacheKeys.CATEGORIES_TAG]);

        return new Response<List<CategoryCursusDetails>>
        {
            Status = 200,
            Message = "Catégories de cursus récupérées avec succès",
            Data = categories,
            Count = categories.Count
        };
    }

    /// <summary>
    /// Récupère une catégorie de cursus par son identifiant
    /// </summary>
    /// <param name="id">Identifiant de la catégorie de cursus</param>
    /// <returns>Catégorie de cursus trouvée</returns>
    public async Task<Response<CategoryCursusDetails>> GetCategoryCursusByIdAsync(Guid id)
    {
        var category = await context.CategoryCursuses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.ArchivedAt == null);

        if (category == null)
        {
            return new Response<CategoryCursusDetails>
            {
                Status = 404,
                Message = "Catégorie de cursus non trouvée",
                Data = null
            };
        }

        return new Response<CategoryCursusDetails>
        {
            Status = 200,
            Message = "Catégorie de cursus récupérée avec succès",
            Data = new CategoryCursusDetails(category)
        };
    }

    /// <summary>
    /// Crée une nouvelle catégorie de cursus
    /// </summary>
    /// <param name="categoryDto">Données de la catégorie de cursus à créer</param>
    /// <returns>Catégorie de cursus créée</returns>
    public async Task<Response<CategoryCursusDetails>> CreateCategoryCursusAsync(CategoryCursusCreate categoryDto)
    {
        // Vérifier que le nom n'existe pas déjà
        var existingCategory = await context.CategoryCursuses
            .AnyAsync(c => c.Name.ToLower() == categoryDto.Name.ToLower() && c.ArchivedAt == null);

        if (existingCategory)
        {
            return new Response<CategoryCursusDetails>
            {
                Status = 400,
                Message = "Une catégorie de cursus avec ce nom existe déjà",
                Data = null
            };
        }

        var category = new CategoryCursus
        {
            Id = Guid.NewGuid(),
            Name = categoryDto.Name,
            Color = categoryDto.Color,
            Icon = categoryDto.Icon,
            Translations = TranslationsHelper.Normalize(categoryDto.Translations),
            DescriptionTranslations = TranslationsHelper.Normalize(categoryDto.DescriptionTranslations ?? new()),
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.CategoryCursuses.Add(category);
        await context.SaveChangesAsync();
        await cache.RemoveByTagAsync(CacheKeys.CATEGORIES_TAG);

        return new Response<CategoryCursusDetails>
        {
            Status = 201,
            Message = "Catégorie de cursus créée avec succès",
            Data = new CategoryCursusDetails(category)
        };
    }

    /// <summary>
    /// Met à jour une catégorie de cursus existante
    /// </summary>
    /// <param name="id">Identifiant de la catégorie de cursus</param>
    /// <param name="categoryDto">Nouvelles données de la catégorie de cursus</param>
    /// <returns>Catégorie de cursus mise à jour</returns>
    public async Task<Response<CategoryCursusDetails>> UpdateCategoryCursusAsync(Guid id, CategoryCursusUpdate categoryDto)
    {
        var category = await context.CategoryCursuses
            .FirstOrDefaultAsync(c => c.Id == id && c.ArchivedAt == null);

        if (category == null)
        {
            return new Response<CategoryCursusDetails>
            {
                Status = 404,
                Message = "Catégorie de cursus non trouvée",
                Data = null
            };
        }

        // Vérifier que le nom n'existe pas déjà pour une autre catégorie
        var existingCategory = await context.CategoryCursuses
            .AnyAsync(c => c.Name.ToLower() == categoryDto.Name.ToLower() && c.Id != id && c.ArchivedAt == null);

        if (existingCategory)
        {
            return new Response<CategoryCursusDetails>
            {
                Status = 400,
                Message = "Une autre catégorie de cursus avec ce nom existe déjà",
                Data = null
            };
        }

        category.Name = categoryDto.Name;
        category.Color = categoryDto.Color;
        category.Icon = categoryDto.Icon;
        category.Translations = TranslationsHelper.Normalize(categoryDto.Translations);
        category.DescriptionTranslations = TranslationsHelper.Normalize(categoryDto.DescriptionTranslations ?? new());
        category.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync();
        await cache.RemoveByTagAsync(CacheKeys.CATEGORIES_TAG);

        return new Response<CategoryCursusDetails>
        {
            Status = 200,
            Message = "Catégorie de cursus mise à jour avec succès",
            Data = new CategoryCursusDetails(category)
        };
    }

    /// <summary>
    /// Archive une catégorie de cursus (suppression logique)
    /// </summary>
    /// <param name="id">Identifiant de la catégorie de cursus</param>
    /// <returns>Résultat de l'opération</returns>
    public async Task<Response<object>> DeleteCategoryCursusAsync(Guid id)
    {
        var category = await context.CategoryCursuses
            .FirstOrDefaultAsync(c => c.Id == id && c.ArchivedAt == null);

        if (category == null)
        {
            return new Response<object>
            {
                Status = 404,
                Message = "Catégorie de cursus non trouvée",
                Data = null
            };
        }

        // Vérifier si la catégorie est utilisée par des cursus
        var isUsedByCursus = await context.Cursuses
            .AnyAsync(p => p.Categories.Any(cat => cat.Id == id) && p.ArchivedAt == null);

        if (isUsedByCursus)
        {
            return new Response<object>
            {
                Status = 400,
                Message = "Cette catégorie est utilisée par des cursus et ne peut pas être supprimée",
                Data = null
            };
        }

        category.ArchivedAt = DateTimeOffset.UtcNow;
        category.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync();
        await cache.RemoveByTagAsync(CacheKeys.CATEGORIES_TAG);

        return new Response<object>
        {
            Status = 200,
            Message = "Catégorie de cursus supprimée avec succès",
            Data = null
        };
    }

    /// <summary>
    /// Restaure une catégorie de cursus archivée
    /// </summary>
    /// <param name="id">Identifiant de la catégorie de cursus</param>
    /// <returns>Catégorie de cursus restaurée</returns>
    public async Task<Response<CategoryCursusDetails>> UnarchiveCategoryCursusAsync(Guid id)
    {
        var category = await context.CategoryCursuses
            .FirstOrDefaultAsync(c => c.Id == id && c.ArchivedAt != null);

        if (category == null)
        {
            return new Response<CategoryCursusDetails>
            {
                Status = 404,
                Message = "Catégorie de cursus archivée non trouvée",
                Data = null
            };
        }

        // Vérifier qu'une catégorie active avec ce nom n'existe pas déjà
        var existingCategory = await context.CategoryCursuses
            .AnyAsync(c => c.Name.ToLower() == category.Name.ToLower() && c.Id != id && c.ArchivedAt == null);

        if (existingCategory)
        {
            return new Response<CategoryCursusDetails>
            {
                Status = 400,
                Message = "Une catégorie de cursus active avec ce nom existe déjà",
                Data = null
            };
        }

        category.ArchivedAt = null;
        category.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync();
        await cache.RemoveByTagAsync(CacheKeys.CATEGORIES_TAG);

        return new Response<CategoryCursusDetails>
        {
            Status = 200,
            Message = "Catégorie de cursus restaurée avec succès",
            Data = new CategoryCursusDetails(category)
        };
    }
}
