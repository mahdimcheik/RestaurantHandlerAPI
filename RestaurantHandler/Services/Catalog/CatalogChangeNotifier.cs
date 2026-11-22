using Microsoft.Extensions.Caching.Hybrid;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Services;

/// <summary>Nom de l'événement temps réel et portées : la PWA relit la carte ou l'état d'ouverture.</summary>
public static class AvailabilityEvents
{
    public const string AvailabilityChanged = "AvailabilityChanged";
    public const string CatalogScope = "catalog";
    public const string OpeningScope = "opening";
}

/// <summary>
/// À appeler après chaque écriture Manager : invalide le cache (ADR-0004) puis prévient tous les
/// clients connectés, anonymes compris (US-E03-4 : visible en moins de 5 secondes).
/// </summary>
public class CatalogChangeNotifier(HybridCache cache, RealtimeService realtime)
{
    public async Task CatalogChangedAsync()
    {
        await cache.RemoveByTagAsync(CacheKeys.CatalogTag);
        await realtime.SendToAllAsync(AvailabilityEvents.AvailabilityChanged, new { scope = AvailabilityEvents.CatalogScope });
    }

    public async Task ScheduleChangedAsync()
    {
        await cache.RemoveByTagAsync(CacheKeys.ScheduleTag);
        await OpeningChangedAsync();
    }

    public Task OpeningChangedAsync() =>
        realtime.SendToAllAsync(AvailabilityEvents.AvailabilityChanged, new { scope = AvailabilityEvents.OpeningScope });
}
