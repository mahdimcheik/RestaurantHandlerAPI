using RestaurantHandler.Domain.Policies;

namespace RestaurantHandler.Tests.Policies;

public class OpeningHoursPolicyTests
{
    [Fact]
    public void Deux_plages_disjointes_sont_valides()
    {
        Assert.Null(OpeningHoursPolicy.Validate([
            new(DayOfWeek.Monday, new(11, 30), new(14, 30)),
            new(DayOfWeek.Monday, new(18, 30), new(22, 0)),
            new(DayOfWeek.Tuesday, new(11, 0), new(23, 0)),
        ]));
    }

    [Fact]
    public void Plages_qui_se_touchent_sont_valides()
    {
        Assert.Null(OpeningHoursPolicy.Validate([
            new(DayOfWeek.Monday, new(11, 0), new(14, 0)),
            new(DayOfWeek.Monday, new(14, 0), new(16, 0)),
        ]));
    }

    [Fact]
    public void Chevauchement_refuse()
    {
        var error = OpeningHoursPolicy.Validate([
            new(DayOfWeek.Friday, new(11, 30), new(14, 30)),
            new(DayOfWeek.Friday, new(14, 0), new(22, 0)),
        ]);

        Assert.Contains("Vendredi", error);
    }

    [Fact]
    public void Plage_passant_minuit_refusee()
    {
        Assert.NotNull(OpeningHoursPolicy.Validate([new(DayOfWeek.Saturday, new(18, 0), new(1, 0))]));
    }
}
