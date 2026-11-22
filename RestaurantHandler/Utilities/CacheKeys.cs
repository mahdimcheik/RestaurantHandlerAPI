using Microsoft.Extensions.Caching.Hybrid;

namespace RestaurantHandler.Utilities;

/// <summary>
/// Clés et tags du cache mémoire (cf. ADR-0004). Seules des copies jetables y vivent : le vider ne
/// doit jamais rien casser, d'où l'absence de tout ce qui touche aux sessions.
/// </summary>
public static class CacheKeys
{
    /// <summary>Carte publique, sans l'état d'ouverture (la pause n'est jamais en cache).</summary>
    public const string PublicCard = "catalog:public-card";
    public const string CatalogTag = "catalog";

    /// <summary>Horaires et fermetures à venir.</summary>
    public const string Schedule = "restaurant:schedule";
    public const string ScheduleTag = "schedule";

    public static readonly HybridCacheEntryOptions Catalog = Expire(TimeSpan.FromHours(6));

    private static HybridCacheEntryOptions Expire(TimeSpan duration) =>
        new() { Expiration = duration, LocalCacheExpiration = duration };
}
