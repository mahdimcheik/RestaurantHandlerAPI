using Microsoft.Extensions.Caching.Hybrid;

namespace RestaurantHandler.Utilities;

/// <summary>
/// Clés et tags du cache mémoire (cf. ADR-0004). Seules des copies jetables y vivent : le vider ne
/// doit jamais rien casser, d'où l'absence de tout ce qui touche aux sessions.
/// </summary>
public static class CacheKeys
{
    private static HybridCacheEntryOptions Expire(TimeSpan duration) =>
        new() { Expiration = duration, LocalCacheExpiration = duration };
}
