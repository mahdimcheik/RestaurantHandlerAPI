using System.ComponentModel;
using RestaurantHandler.Models;
using RestaurantHandler.Models.Filters;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;
using ModelContextProtocol.Server;

namespace RestaurantHandler.Mcp;

/// <summary>
/// Outils MCP exposés anonymement sur <c>/mcp</c>. Même périmètre que <c>POST /teachers/all</c>,
/// mais la sortie est réduite aux champs publics : jamais d'email ni de téléphone.
/// </summary>
[McpServerToolType]
public sealed class TeacherSearchTools
{
    public const string FastSearchName = "fast_search";
    public const string FastSearchDescription =
        "Recherche des professeurs particuliers sur BonProf par matière, ville, code postal, "
        + "prix horaire et disponibilités. Renvoie les profs triés par prochaine disponibilité, "
        + "avec un lien vers leur profil public. "
        + "Searches BonProf private tutors by subject, city, postal code, hourly price and availability.";

    private const int DefaultLimit = 10;
    private const int MaxLimit = 20;

    // FullName n'est volontairement pas exposé : le filtre compare aussi l'email, ce qui
    // permettrait à un appelant anonyme de tester si une adresse appartient à un prof.
    [McpServerTool(
        Name = FastSearchName,
        Title = "Recherche rapide de professeurs",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true
    )]
    [Description(FastSearchDescription)]
    public static async Task<FastSearchResponse> FastSearch(
        TeacherSearchService searchService,
        [Description("Matière ou intitulé de cours, ex. « maths », « piano ». Subject or course name.")]
            string? subject = null,
        [Description("Ville (correspondance partielle). City.")] string? city = null,
        [Description("Code postal exact, ex. « 75011 ». Postal code.")] string? postalCode = null,
        [Description("Prix horaire minimum en euros. Minimum hourly price (EUR).")]
            decimal? priceMin = null,
        [Description("Prix horaire maximum en euros. Maximum hourly price (EUR).")]
            decimal? priceMax = null,
        [Description(
            "Début de la période de disponibilité souhaitée (ISO 8601). Si fourni avec ou sans "
                + "availableTo, seuls les profs ayant un créneau libre dans la période sont renvoyés."
        )]
            DateTimeOffset? availableFrom = null,
        [Description("Fin de la période de disponibilité souhaitée (ISO 8601).")]
            DateTimeOffset? availableTo = null,
        [Description("Nombre maximum de résultats (1 à 20, 10 par défaut).")] int? limit = null
    )
    {
        var filter = new FilterTeacher
        {
            CursusName = Blank(subject),
            City = Blank(city),
            PostalCode = Blank(postalCode),
            PriceMin = priceMin,
            PriceMax = priceMax,
            DateFrom = availableFrom,
            DateTo = availableTo,
            AvailableOnly = availableFrom is not null || availableTo is not null,
            CategoryIds = [],
            First = 0,
            Row = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit),
        };

        var response = await searchService.GetAllTeacherProfilesAsync(filter, principal: null);

        return new FastSearchResponse(
            response.Count ?? 0,
            (response.Data ?? []).Select(ToResult).ToList()
        );
    }

    private static FastSearchResult ToResult(UserDetails user)
    {
        var address =
            user.Addresses.FirstOrDefault(a => a.TypeId == HardCode.TYPE_ADDRESS_MAIN)
            ?? user.Addresses.FirstOrDefault();
        var cursuses = user.Teacher?.Cursuses ?? [];

        return new FastSearchResult(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Title,
            address?.City,
            address?.ZipCode,
            cursuses.Select(p => new FastSearchCourse(p.Name, p.Price)).ToList(),
            user.Teacher?.PriceIndicative,
            user.Teacher?.AverageRating,
            user.Teacher?.ReviewsCount,
            user.NextAvailableSlotAt,
            user.AvailableSlotsCount,
            user.ImgUrl,
            McpPublicLinks.ProfileUrl(user.Id)
        );
    }

    private static string? Blank(string? value) => McpPublicLinks.Blank(value);
}

public sealed record FastSearchResponse(
    [property: Description("Nombre total de profs correspondant aux critères.")] long Total,
    List<FastSearchResult> Results
);

public sealed record FastSearchResult(
    Guid Id,
    string FirstName,
    string LastName,
    string? Title,
    string? City,
    string? PostalCode,
    List<FastSearchCourse> Courses,
    [property: Description("Prix horaire indicatif en euros.")] decimal? PriceIndicative,
    double? AverageRating,
    int? ReviewsCount,
    [property: Description("Début du prochain créneau libre, null si aucun.")]
        DateTimeOffset? NextAvailableSlotAt,
    [property: Description("Créneaux libres dans la période demandée (null sans période).")]
        int? AvailableSlotsCount,
    string? AvatarUrl,
    [property: Description("Lien vers le profil public du prof sur BonProf.")] string ProfileUrl
);

public sealed record FastSearchCourse(string Name, decimal Price);
