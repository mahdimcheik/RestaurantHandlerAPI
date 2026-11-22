using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Models;

namespace RestaurantHandler.Tests.Policies;

public class OpeningPolicyTests
{
    // Lundi 23/11/2026, Paris = UTC+1.
    private static readonly OpeningHours[] Week = Enum.GetValues<DayOfWeek>()
        .SelectMany(d => new[]
        {
            new OpeningHours { DayOfWeek = d, Start = new(11, 30), End = new(14, 30) },
            new OpeningHours { DayOfWeek = d, Start = new(18, 30), End = new(22, 0) },
        })
        .ToArray();

    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso + "Z");

    private static OpeningState At(string isoUtc, Closure[]? closures = null, bool paused = false, DateTimeOffset? until = null) =>
        OpeningPolicy.Compute(Utc(isoUtc), Week, closures ?? [], paused, until);

    [Fact]
    public void Ouvert_pendant_une_plage_avec_heure_de_fermeture()
    {
        var state = At("2026-11-23T11:00"); // 12h00 locale

        Assert.Equal(OpeningStatus.Open, state.Status);
        Assert.Equal(Utc("2026-11-23T13:30"), state.ClosesAt);
    }

    [Fact]
    public void Ferme_avant_ouverture_indique_la_prochaine_ouverture()
    {
        var state = At("2026-11-23T08:00"); // 9h00 locale

        Assert.Equal(OpeningStatus.Closed, state.Status);
        Assert.Equal(Utc("2026-11-23T10:30"), state.NextOpeningAt);
    }

    [Fact]
    public void Ferme_entre_deux_plages_rouvre_le_soir()
    {
        var state = At("2026-11-23T15:00"); // 16h00 locale

        Assert.Equal(OpeningStatus.Closed, state.Status);
        Assert.Equal(Utc("2026-11-23T17:30"), state.NextOpeningAt);
    }

    [Fact]
    public void La_fin_de_plage_est_exclue()
    {
        Assert.Equal(OpeningStatus.Closed, At("2026-11-23T13:30").Status); // 14h30 pile
        Assert.Equal(OpeningStatus.Open, At("2026-11-23T10:30").Status); // 11h30 pile
    }

    [Fact]
    public void Fermeture_exceptionnelle_avec_motif_et_reouverture_apres()
    {
        var closure = new Closure
        {
            StartLocal = new DateTime(2026, 11, 23, 0, 0, 0),
            EndLocal = new DateTime(2026, 11, 25, 0, 0, 0),
            Reason = "Congés",
        };

        var state = At("2026-11-23T11:00", [closure]);

        Assert.Equal(OpeningStatus.Closed, state.Status);
        Assert.Equal("Congés", state.ClosureReason);
        Assert.Equal(Utc("2026-11-25T10:30"), state.NextOpeningAt);
    }

    [Fact]
    public void Fermeture_partielle_ferme_plus_tot()
    {
        var closure = new Closure
        {
            StartLocal = new DateTime(2026, 11, 23, 13, 0, 0),
            EndLocal = new DateTime(2026, 11, 23, 23, 59, 0),
        };

        var open = At("2026-11-23T11:00", [closure]);
        Assert.Equal(OpeningStatus.Open, open.Status);
        Assert.Equal(Utc("2026-11-23T12:00"), open.ClosesAt);

        var closed = At("2026-11-23T12:30", [closure]);
        Assert.Equal(OpeningStatus.Closed, closed.Status);
        Assert.Equal(Utc("2026-11-24T10:30"), closed.NextOpeningAt);
    }

    [Fact]
    public void Pause_active_pendant_l_ouverture()
    {
        var until = Utc("2026-11-23T11:15");
        var state = At("2026-11-23T11:00", paused: true, until: until);

        Assert.Equal(OpeningStatus.Paused, state.Status);
        Assert.Equal(until, state.PausedUntil);
    }

    [Fact]
    public void Pause_expiree_redevient_ouvert()
    {
        var state = At("2026-11-23T11:30", paused: true, until: Utc("2026-11-23T11:15"));

        Assert.Equal(OpeningStatus.Open, state.Status);
    }

    [Fact]
    public void Pause_sans_fin_reste_active()
    {
        Assert.Equal(OpeningStatus.Paused, At("2026-11-23T11:00", paused: true).Status);
    }

    [Fact]
    public void Ferme_l_emporte_sur_la_pause()
    {
        Assert.Equal(OpeningStatus.Closed, At("2026-11-23T08:00", paused: true).Status);
    }

    [Fact]
    public void Passage_a_l_heure_d_ete()
    {
        // Samedi 27/03/2027 23h00 locale (UTC+1) ; dimanche 28/03 11h30 locale = 9h30 UTC (UTC+2).
        var state = At("2027-03-27T22:00");

        Assert.Equal(Utc("2027-03-28T09:30"), state.NextOpeningAt);
        Assert.Equal(OpeningStatus.Open, At("2027-03-28T09:30").Status);
        Assert.Equal(OpeningStatus.Closed, At("2027-03-28T09:29").Status);
    }

    [Fact]
    public void Passage_a_l_heure_d_hiver()
    {
        // Dimanche 25/10/2026 : 11h30 locale = 10h30 UTC (UTC+1 depuis 3h).
        Assert.Equal(Utc("2026-10-25T10:30"), At("2026-10-25T06:00").NextOpeningAt);
        Assert.Equal(OpeningStatus.Open, At("2026-10-25T10:30").Status);
    }

    [Fact]
    public void Sans_horaires_aucune_reouverture()
    {
        var state = OpeningPolicy.Compute(Utc("2026-11-23T11:00"), [], [], false, null);

        Assert.Equal(OpeningStatus.Closed, state.Status);
        Assert.Null(state.NextOpeningAt);
    }
}
