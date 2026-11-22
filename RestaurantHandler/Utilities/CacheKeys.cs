using Microsoft.Extensions.Caching.Hybrid;

namespace RestaurantHandler.Utilities;

/// <summary>
/// Clés et tags du cache mémoire (cf. ADR-0017). Seules des copies jetables y vivent : le vider ne
/// doit jamais rien casser, d'où l'absence de tout ce qui touche aux sessions.
/// </summary>
public static class CacheKeys
{
    // Référentiels gérés par l'admin : un tag que chaque écriture invalide après son SaveChanges.
    public const string DOCUMENT_TYPES_TAG = "ref:document-type";

    // Référentiels sans CRUD (seed uniquement) : jamais invalidés, l'expiration suffit.
    public const string GENDERS = "ref:gender";

    public const string DOCUMENT_TYPES = "ref:document-type:active";

    // Fiches d'aide publiées (liste, détail, sitemap) : chaque écriture invalide le tag entier.
    public const string HELP_TAG = "help";

    // Lu par le middleware de maintenance à chaque requête. Invalidé à l'écriture ; l'expiration
    // courte borne le décalage d'une autre instance ou d'une modification faite en SQL.
    public const string MAINTENANCE = "settings:maintenance";

    public static string HelpList(string language) => $"{HELP_TAG}:list:{language}";

    public static string HelpBySlug(string slug) => $"{HELP_TAG}:slug:{slug}";

    public const string HELP_SITEMAP = HELP_TAG + ":sitemap";

    // Filet de sécurité si une ligne est modifiée hors de l'API (SQL à la main, migration).
    public static readonly HybridCacheEntryOptions Reference = Expire(TimeSpan.FromHours(1));

    public static readonly HybridCacheEntryOptions Help = Expire(TimeSpan.FromMinutes(10));

    public static readonly HybridCacheEntryOptions Maintenance = Expire(TimeSpan.FromSeconds(15));

    private static HybridCacheEntryOptions Expire(TimeSpan duration) =>
        new() { Expiration = duration, LocalCacheExpiration = duration };
}
