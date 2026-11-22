using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Services;

public class TypeAddressService(MainContext context, HybridCache cache)
{
    /// <summary>
    /// Récupère toutes les types  adresses
    /// </summary>
    /// <returns>Liste des adresses</returns>
    public async Task<Response<List<TypeAddressDetails>>> GetAllAddresseTypesAsync()
    {
        var addressTypes = await cache.GetOrCreateAsync(
            CacheKeys.TYPE_ADDRESSES,
            async ct => await context.TypeAddresses
                .AsNoTracking()
                .Where(a => a.ArchivedAt == null)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new TypeAddressDetails(a))
                .ToListAsync(ct),
            CacheKeys.Reference
        );

        return new Response<List<TypeAddressDetails>>
        {
            Status = 200,
            Message = "Adresses récupérées avec succès",
            Data = addressTypes,
            Count = addressTypes.Count
        };
    }
}
