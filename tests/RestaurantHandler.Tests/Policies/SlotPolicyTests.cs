using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Models;

namespace RestaurantHandler.Tests.Policies;

public class SlotPolicyTests
{
    // Lundi 23/11/2026, Paris = UTC+1 ; plage 11h30–12h30 locale = 10h30–11h30 UTC.
    private static readonly OpeningHours[] Monday =
    [
        new() { DayOfWeek = DayOfWeek.Monday, Start = new(11, 30), End = new(12, 30) },
    ];

    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso + "Z");

    [Fact]
    public void Decoupe_la_plage_en_creneaux_qui_tiennent_entierement()
    {
        var slots = SlotPolicy.ForToday(Utc("2026-11-23T08:00"), Monday, [], 20, 5, 0);

        Assert.Equal(
            [Utc("2026-11-23T10:30"), Utc("2026-11-23T10:50"), Utc("2026-11-23T11:10")],
            slots.Select(s => s.Start)
        );
        Assert.All(slots, s => Assert.Equal(5, s.Remaining));
        Assert.Equal(Utc("2026-11-23T11:30"), slots[^1].End);
    }

    [Fact]
    public void Respecte_le_delai_minimal()
    {
        // 11h40 locale + 20 min = 12h00 : le créneau de 11h45 n'est plus proposé.
        var slots = SlotPolicy.ForToday(Utc("2026-11-23T10:40"), Monday, [], 15, 5, 20);

        Assert.Equal([Utc("2026-11-23T11:00"), Utc("2026-11-23T11:15")], slots.Select(s => s.Start));
    }

    [Fact]
    public void Un_creneau_plein_n_est_plus_propose()
    {
        var occupancy = new Dictionary<DateTimeOffset, int>
        {
            [Utc("2026-11-23T10:30")] = 5,
            [Utc("2026-11-23T10:45")] = 3,
        };

        var slots = SlotPolicy.ForToday(Utc("2026-11-23T08:00"), Monday, [], 15, 5, 0, occupancy);

        Assert.DoesNotContain(slots, s => s.Start == Utc("2026-11-23T10:30"));
        Assert.Equal(2, slots.Single(s => s.Start == Utc("2026-11-23T10:45")).Remaining);
    }

    [Fact]
    public void Jour_meme_uniquement_et_fermetures_deduites()
    {
        var closure = new Closure
        {
            StartLocal = new DateTime(2026, 11, 23, 12, 0, 0),
            EndLocal = new DateTime(2026, 11, 23, 13, 0, 0),
        };

        var slots = SlotPolicy.ForToday(Utc("2026-11-23T08:00"), Monday, [closure], 15, 5, 0);
        Assert.Equal(2, slots.Count); // 11h30 et 11h45

        Assert.Empty(SlotPolicy.ForToday(Utc("2026-11-23T12:00"), Monday, [], 15, 5, 0)); // après la plage
        Assert.Empty(SlotPolicy.ForToday(Utc("2026-11-24T08:00"), Monday, [], 15, 5, 0)); // mardi
    }
}
