using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Domain.Policies;

/// <summary>Créneau de retrait [Start, End[ et places restantes (en commandes).</summary>
public sealed record PickupSlot(DateTimeOffset Start, DateTimeOffset End, int Remaining);

/// <summary>
/// Créneaux de retrait du jour (US-E03-6, Q5 : capacité en commandes, jour même uniquement).
/// Les créneaux sont alignés sur le début de chaque plage ouverte et tiennent entièrement dans
/// la plage. La pause n'est pas prise en compte ici : c'est au service de ne rien proposer.
/// </summary>
public static class SlotPolicy
{
    public static IReadOnlyList<PickupSlot> ForToday(
        DateTimeOffset nowUtc,
        IReadOnlyCollection<OpeningHours> hours,
        IReadOnlyCollection<Closure> closures,
        int slotDurationMinutes,
        int capacityOrders,
        int minLeadMinutes,
        IReadOnlyDictionary<DateTimeOffset, int>? occupancyByStart = null
    )
    {
        if (slotDurationMinutes <= 0 || capacityOrders <= 0)
            return [];

        var nowLocal = RestaurantClock.ToLocal(nowUtc);
        var earliest = nowUtc.AddMinutes(minLeadMinutes);
        var duration = TimeSpan.FromMinutes(slotDurationMinutes);
        var slots = new List<PickupSlot>();

        foreach (var interval in OpeningPolicy.OpenIntervals(DateOnly.FromDateTime(nowLocal), hours, closures))
        {
            for (var start = interval.Start; start + duration <= interval.End; start += duration)
            {
                var startUtc = RestaurantClock.ToUtc(start);
                if (startUtc < earliest)
                    continue;

                var taken = occupancyByStart?.GetValueOrDefault(startUtc) ?? 0;
                var remaining = capacityOrders - taken;
                if (remaining > 0)
                    slots.Add(new PickupSlot(startUtc, RestaurantClock.ToUtc(start + duration), remaining));
            }
        }

        return slots;
    }
}
