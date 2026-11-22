namespace RestaurantHandler.Models;

/// <summary>
/// Résultat d'un géocodage via l'API géopf.fr. Contrat fail-open : <see cref="Success"/> à
/// false (timeout, indisponibilité, aucune adresse trouvée) ne doit jamais bloquer la
/// sauvegarde de l'adresse appelante — <see cref="Latitude"/>/<see cref="Longitude"/> restent
/// alors simplement non renseignées.
/// </summary>
public record GeocodingResult(bool Success, float? Latitude = null, float? Longitude = null)
{
    public static readonly GeocodingResult NotFound = new(false);

    public static GeocodingResult Found(float latitude, float longitude) => new(true, latitude, longitude);
}

// DTOs de désérialisation de la réponse GeoJSON de l'API géopf.fr (sous-ensemble utilisé).
internal record GeoPfFeatureCollection(List<GeoPfFeature>? Features);

internal record GeoPfFeature(GeoPfGeometry? Geometry);

internal record GeoPfGeometry(List<double>? Coordinates);
