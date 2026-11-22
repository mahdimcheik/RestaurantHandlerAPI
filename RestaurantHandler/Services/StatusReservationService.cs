using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Services;

public class StatusReservationService(MainContext context, HybridCache cache)
{
    public async Task<Response<List<StatusReservationDetails>>> GetAllStatusReservationsAsync()
    {
        var statuses = await cache.GetOrCreateAsync(
            CacheKeys.STATUS_RESERVATIONS,
            async ct => await context
                .StatusReservations.AsNoTracking()
                .Where(s => s.ArchivedAt == null)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new StatusReservationDetails(s))
                .ToListAsync(ct),
            CacheKeys.Reference
        );

        return new Response<List<StatusReservationDetails>>
        {
            Status = 200,
            Message = "Statuts de réservation récupérés avec succès",
            Data = statuses,
            Count = statuses.Count,
        };
    }
}
