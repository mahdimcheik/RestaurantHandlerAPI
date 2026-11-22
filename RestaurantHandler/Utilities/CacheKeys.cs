using Microsoft.Extensions.Caching.Hybrid;

namespace RestaurantHandler.Utilities;

/// <summary>
/// Clés et tags du cache mémoire (cf. ADR-0017). Seules des copies jetables y vivent : le vider ne
/// doit jamais rien casser, d'où l'absence de tout ce qui touche à l'argent, aux réservations ou
/// aux sessions.
/// </summary>
public static class CacheKeys
{
    // Référentiels gérés par l'admin. La liste admin inclut les archivés, la liste publique non :
    // deux clés, un seul tag, que chaque écriture invalide après son SaveChanges.
    public const string CATEGORIES_TAG = "ref:category";
    public const string LEVELS_TAG = "ref:level";
    public const string LANGUAGES_TAG = "ref:language";
    public const string TYPE_SLOTS_TAG = "ref:type-slot";
    public const string DOCUMENT_TYPES_TAG = "ref:document-type";

    // Référentiels sans CRUD (seed uniquement) : jamais invalidés, l'expiration suffit.
    public const string GENDERS = "ref:gender";
    public const string TYPE_ADDRESSES = "ref:type-address";
    public const string STATUS_RESERVATIONS = "ref:status-reservation";

    public const string LANGUAGES = "ref:language:all";
    public const string TYPE_SLOTS = "ref:type-slot:all";
    public const string DOCUMENT_TYPES = "ref:document-type:active";

    // Articles publiés (liste, détail, tags, sitemap) : chaque écriture d'article ou de
    // commentaire invalide le tag entier.
    public const string ARTICLES_TAG = "articles";

    // Fiches d'aide publiées (liste, détail, sitemap) : chaque écriture invalide le tag entier.
    public const string HELP_TAG = "help";

    public const string FEATURED_TEACHERS_POOL = "teachers:featured-pool";

    // Lu par le middleware de maintenance à chaque requête. Invalidé à l'écriture ; l'expiration
    // courte borne le décalage d'une autre instance ou d'une modification faite en SQL.
    public const string MAINTENANCE = "settings:maintenance";

    public static string Categories(bool includeArchived) =>
        $"{CATEGORIES_TAG}:{(includeArchived ? "admin" : "public")}";

    public static string Levels(bool includeArchived) =>
        $"{LEVELS_TAG}:{(includeArchived ? "admin" : "public")}";

    public static string ArticleList(string? language, string? tag, int first, int rows) =>
        $"{ARTICLES_TAG}:list:{language}:{tag?.ToLowerInvariant()}:{first}:{rows}";

    public static string ArticleBySlug(string slug) => $"{ARTICLES_TAG}:slug:{slug}";

    public static string ArticleTags(string? language) => $"{ARTICLES_TAG}:tags:{language}";

    public static string ArticleRelated(Guid id, int count) => $"{ARTICLES_TAG}:related:{id}:{count}";

    public const string ARTICLE_SITEMAP = ARTICLES_TAG + ":sitemap";

    public static string HelpList(string language) => $"{HELP_TAG}:list:{language}";

    public static string HelpBySlug(string slug) => $"{HELP_TAG}:slug:{slug}";

    public const string HELP_SITEMAP = HELP_TAG + ":sitemap";

    public static string Geocoding(string city, string zipCode) =>
        $"geo:{zipCode.Trim().ToLowerInvariant()}:{city.Trim().ToLowerInvariant()}";

    // Filet de sécurité si une ligne est modifiée hors de l'API (SQL à la main, migration).
    public static readonly HybridCacheEntryOptions Reference = Expire(TimeSpan.FromHours(1));

    public static readonly HybridCacheEntryOptions GeocodingEntry = Expire(TimeSpan.FromDays(30));

    // Pas d'invalidation : un nouveau prof ou une note qui change apparaît au plus 5 min après.
    public static readonly HybridCacheEntryOptions FeaturedTeachersPool = Expire(
        TimeSpan.FromMinutes(5)
    );

    public static readonly HybridCacheEntryOptions Articles = Expire(TimeSpan.FromMinutes(10));

    public static readonly HybridCacheEntryOptions Maintenance = Expire(TimeSpan.FromSeconds(15));

    private static HybridCacheEntryOptions Expire(TimeSpan duration) =>
        new() { Expiration = duration, LocalCacheExpiration = duration };
}
