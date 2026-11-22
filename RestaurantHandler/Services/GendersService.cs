using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Services;

/// <summary>
/// Service pour la gestion des genres
/// </summary>
public class GendersService(MainContext context, HybridCache cache)
{
    /// <summary>
    /// Récupère tous les genres
    /// </summary>
    /// <returns>Liste des genres</returns>
    public async Task<Response<List<GenderDetails>>> GetAllGendersAsync()
    {
        var genders = await cache.GetOrCreateAsync(
            CacheKeys.GENDERS,
            async ct => await context
                .Genders.AsNoTracking()
                .Where(g => g.ArchivedAt == null)
                .OrderBy(g => g.Name)
                .Select(g => new GenderDetails(g))
                .ToListAsync(ct),
            CacheKeys.Reference
        );

        return new Response<List<GenderDetails>>
        {
            Status = 200,
            Message = "Genres récupérés avec succès",
            Data = genders,
            Count = genders.Count,
        };
    }
}
