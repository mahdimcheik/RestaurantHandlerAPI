using System.Net;
using System.Text.Json;
using System.Web;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.Extensions.Caching.Hybrid;

namespace RestaurantHandler.Services;

/// <summary>
/// Géolocalise une ville française via l'API géopf.fr (BAN/IGN, publique, sans clé).
///
/// Volontairement limité à l'échelle de la commune (type=municipality) : on ne géocode pas
/// l'adresse exacte (rue/numéro), seulement la ville, pour renvoyer le centre de la commune.
///
/// Contrat : <see cref="GeocodeAsync"/> ne lève JAMAIS et est fail-open — contrairement à
/// <see cref="InseeSireneService"/> qui bloque l'inscription en cas d'échec, un échec de
/// géocodage (réseau, timeout, JSON, aucune correspondance) renvoie
/// <see cref="GeocodingResult.NotFound"/> : l'appelant doit simplement laisser
/// Latitude/Longitude non renseignées et continuer la sauvegarde.
///
/// Seules les réussites sont mises en cache : un échec passager (panne géopf, timeout) mis en
/// cache priverait la commune de coordonnées pendant toute la durée de l'entrée.
/// </summary>
public class GeocodingService(
    HttpClient httpClient,
    HybridCache cache,
    ILogger<GeocodingService> logger
)
{
    public async Task<GeocodingResult> GeocodeAsync(string city, string zipCode)
    {
        try
        {
            return await cache.GetOrCreateAsync(
                CacheKeys.Geocoding(city, zipCode),
                async ct =>
                    await LookupAsync(city, zipCode, ct) is { Success: true } found
                        ? found
                        : throw new NotFoundException(),
                CacheKeys.GeocodingEntry
            );
        }
        catch (NotFoundException)
        {
            return GeocodingResult.NotFound;
        }
    }

    private async Task<GeocodingResult> LookupAsync(
        string city,
        string zipCode,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var query = HttpUtility.UrlEncode(city);
            var postcode = HttpUtility.UrlEncode(zipCode);
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"search?q={query}&postcode={postcode}&type=municipality&limit=1"
            );

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                logger.LogWarning(
                    "HTTP {StatusCode} inattendu de l'API géopf.fr lors du géocodage",
                    (int)response.StatusCode
                );
                return GeocodingResult.NotFound;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var collection = JsonSerializer.Deserialize<GeoPfFeatureCollection>(content, options);

            // GeoJSON : coordinates = [longitude, latitude], ordre à inverser pour nos champs.
            var coordinates = collection?.Features?.FirstOrDefault()?.Geometry?.Coordinates;
            if (coordinates is not { Count: 2 })
            {
                logger.LogWarning("Aucune correspondance géopf.fr pour la ville fournie");
                return GeocodingResult.NotFound;
            }

            return GeocodingResult.Found(latitude: (float)coordinates[1], longitude: (float)coordinates[0]);
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("Timeout de l'appel à l'API géopf.fr");
            return GeocodingResult.NotFound;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Erreur réseau vers l'API géopf.fr : {Message}", ex.Message);
            return GeocodingResult.NotFound;
        }
        catch (JsonException ex)
        {
            logger.LogWarning("Réponse géopf.fr illisible : {Message}", ex.Message);
            return GeocodingResult.NotFound;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur inattendue lors du géocodage géopf.fr");
            return GeocodingResult.NotFound;
        }
    }

    // Levée par la fabrique pour que HybridCache ne garde pas l'échec : il ne met en cache que ce
    // qu'elle renvoie.
    private sealed class NotFoundException : Exception;
}
