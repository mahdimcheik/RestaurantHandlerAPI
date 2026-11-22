using Microsoft.Extensions.Caching.Hybrid;

namespace RestaurantHandler.Utilities;

/// <summary>
/// Clés et tags du cache mémoire (cf. ADR-0017). Seules des copies jetables y vivent : le vider ne
/// doit jamais rien casser, d'où l'absence de tout ce qui touche aux sessions.
/// </summary>
public static class CacheKeys
{
    // Fiches d'aide publiées (liste, détail, sitemap) : chaque écriture invalide le tag entier.
    public const string HELP_TAG = "help";

    // Lu par le middleware de maintenance à chaque requête. Invalidé à l'écriture ; l'expiration
    // courte borne le décalage d'une autre instance ou d'une modification faite en SQL.
    public const string MAINTENANCE = "settings:maintenance";

    public static string HelpList(string language) => $"{HELP_TAG}:list:{language}";

    public static string HelpBySlug(string slug) => $"{HELP_TAG}:slug:{slug}";

    public const string HELP_SITEMAP = HELP_TAG + ":sitemap";

    public static readonly HybridCacheEntryOptions Help = Expire(TimeSpan.FromMinutes(10));

    public static readonly HybridCacheEntryOptions Maintenance = Expire(TimeSpan.FromSeconds(15));

    private static HybridCacheEntryOptions Expire(TimeSpan duration) =>
        new() { Expiration = duration, LocalCacheExpiration = duration };
}
