using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Services;

/// <summary>
/// Service pour la gestion des niveaux de cursus
/// </summary>
public class LevelCursusService(MainContext context, HybridCache cache)
{
    /// <summary>
    /// Récupère tous les niveaux de cursus
    /// </summary>
    /// <returns>Liste des niveaux de cursus</returns>
    public async Task<Response<List<LevelCursusDetails>>> GetAllLevelCursusAsync(CallerIdentity? User)
    {
        // check if the user is admin to include archived too
        var includeArchived = User?.IsAdmin == true;

        var levels = await cache.GetOrCreateAsync(
            CacheKeys.Levels(includeArchived),
            async ct => await context.LevelCursuses
                .AsNoTracking()
                .Where(l => includeArchived || l.ArchivedAt == null)
                .OrderBy(l => l.Name)
                .Select(l => new LevelCursusDetails(l))
                .ToListAsync(ct),
            CacheKeys.Reference,
            [CacheKeys.LEVELS_TAG]);

        return new Response<List<LevelCursusDetails>>
        {
            Status = 200,
            Message = "Niveaux de cursus récupérés avec succès",
            Data = levels,
            Count = levels.Count
        };
    }

    /// <summary>
    /// Récupère un niveau de cursus par son identifiant
    /// </summary>
    /// <param name="id">Identifiant du niveau de cursus</param>
    /// <returns>Niveau de cursus trouvé</returns>
    public async Task<Response<LevelCursusDetails>> GetLevelCursusByIdAsync(Guid id)
    {
        var level = await context.LevelCursuses
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id && l.ArchivedAt == null);

        if (level == null)
        {
            return new Response<LevelCursusDetails>
            {
                Status = 404,
                Message = "Niveau de cursus non trouvé",
                Data = null
            };
        }

        return new Response<LevelCursusDetails>
        {
            Status = 200,
            Message = "Niveau de cursus récupéré avec succès",
            Data = new LevelCursusDetails(level)
        };
    }

    /// <summary>
    /// Crée un nouveau niveau de cursus
    /// </summary>
    /// <param name="levelDto">Données du niveau de cursus à créer</param>
    /// <returns>Niveau de cursus créé</returns>
    public async Task<Response<LevelCursusDetails>> CreateLevelCursusAsync(LevelCursusCreate levelDto)
    {
        // Vérifier que le nom n'existe pas déjà
        var existingLevel = await context.LevelCursuses
            .AnyAsync(l => l.Name.ToLower() == levelDto.Name.ToLower() && l.ArchivedAt == null);

        if (existingLevel)
        {
            return new Response<LevelCursusDetails>
            {
                Status = 400,
                Message = "Un niveau de cursus avec ce nom existe déjà",
                Data = null
            };
        }

        var level = new LevelCursus
        {
            Id = Guid.NewGuid(),
            Name = levelDto.Name,
            Color = levelDto.Color,
            Icon = levelDto.Icon,
            Translations = TranslationsHelper.Normalize(levelDto.Translations),
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.LevelCursuses.Add(level);
        await context.SaveChangesAsync();
        await cache.RemoveByTagAsync(CacheKeys.LEVELS_TAG);

        return new Response<LevelCursusDetails>
        {
            Status = 201,
            Message = "Niveau de cursus créé avec succès",
            Data = new LevelCursusDetails(level)
        };
    }

    /// <summary>
    /// Met à jour un niveau de cursus existant
    /// </summary>
    /// <param name="id">Identifiant du niveau de cursus</param>
    /// <param name="levelDto">Nouvelles données du niveau de cursus</param>
    /// <returns>Niveau de cursus mis à jour</returns>
    public async Task<Response<LevelCursusDetails>> UpdateLevelCursusAsync( LevelCursusUpdate levelDto)
    {
        var level = await context.LevelCursuses
            .FirstOrDefaultAsync(l => l.Id == levelDto.Id && l.ArchivedAt == null);

        if (level == null)
        {
            return new Response<LevelCursusDetails>
            {
                Status = 404,
                Message = "Niveau de cursus non trouvé",
                Data = null
            };
        }

        // Vérifier que le nom n'existe pas déjà pour un autre niveau
        var existingLevel = await context.LevelCursuses
            .AnyAsync(l => l.Name.ToLower() == levelDto.Name.ToLower() && l.Id != levelDto.Id && l.ArchivedAt == null);

        if (existingLevel)
        {
            return new Response<LevelCursusDetails>
            {
                Status = 400,
                Message = "Un autre niveau de cursus avec ce nom existe déjà",
                Data = null
            };
        }

        level.Name = levelDto.Name;
        level.Color = levelDto.Color;
        level.Icon = levelDto.Icon;
        level.Translations = TranslationsHelper.Normalize(levelDto.Translations);
        level.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync();
        await cache.RemoveByTagAsync(CacheKeys.LEVELS_TAG);

        return new Response<LevelCursusDetails>
        {
            Status = 200,
            Message = "Niveau de cursus mis à jour avec succès",
            Data = new LevelCursusDetails(level)
        };
    }

    /// <summary>
    /// Archive un niveau de cursus (suppression logique)
    /// </summary>
    /// <param name="id">Identifiant du niveau de cursus</param>
    /// <returns>Résultat de l'opération</returns>
    public async Task<Response<object>> DeleteLevelCursusAsync(Guid id)
    {
        var level = await context.LevelCursuses
            .FirstOrDefaultAsync(l => l.Id == id && l.ArchivedAt == null);

        if (level == null)
        {
            return new Response<object>
            {
                Status = 404,
                Message = "Niveau de cursus non trouvé",
                Data = null
            };
        }

        // Vérifier si le niveau est utilisé par des cursus
        var isUsedByCursus = await context.Cursuses
            .AnyAsync(p => p.LevelId == id && p.ArchivedAt == null);

        if (isUsedByCursus)
        {
            return new Response<object>
            {
                Status = 400,
                Message = "Ce niveau est utilisé par des cursus et ne peut pas être supprimé",
                Data = null
            };
        }

        level.ArchivedAt = DateTimeOffset.UtcNow;
        level.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync();
        await cache.RemoveByTagAsync(CacheKeys.LEVELS_TAG);

        return new Response<object>
        {
            Status = 200,
            Message = "Niveau de cursus supprimé avec succès",
            Data = null
        };
    }

    /// <summary>
    /// Restaure un niveau de cursus archivé
    /// </summary>
    /// <param name="id">Identifiant du niveau de cursus</param>
    /// <returns>Niveau de cursus restauré</returns>
    public async Task<Response<LevelCursusDetails>> UnarchiveLevelCursusAsync(Guid id)
    {
        var level = await context.LevelCursuses
            .FirstOrDefaultAsync(l => l.Id == id && l.ArchivedAt != null);

        if (level == null)
        {
            return new Response<LevelCursusDetails>
            {
                Status = 404,
                Message = "Niveau de cursus archivé non trouvé",
                Data = null
            };
        }

        // Vérifier qu'un niveau actif avec ce nom n'existe pas déjà
        var existingLevel = await context.LevelCursuses
            .AnyAsync(l => l.Name.ToLower() == level.Name.ToLower() && l.Id != id && l.ArchivedAt == null);

        if (existingLevel)
        {
            return new Response<LevelCursusDetails>
            {
                Status = 400,
                Message = "Un niveau de cursus actif avec ce nom existe déjà",
                Data = null
            };
        }

        level.ArchivedAt = null;
        level.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync();
        await cache.RemoveByTagAsync(CacheKeys.LEVELS_TAG);

        return new Response<LevelCursusDetails>
        {
            Status = 200,
            Message = "Niveau de cursus restauré avec succès",
            Data = new LevelCursusDetails(level)
        };
    }
}
